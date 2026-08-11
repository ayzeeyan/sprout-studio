//! Shared Sprout project manifest, validation, loading, and template APIs.

use serde::Deserialize;
use sprout_core::SyntaxProfile;
use std::collections::BTreeMap;
use std::fmt;
use std::fs;
use std::path::{Component, Path, PathBuf};
use std::str::FromStr;
use std::time::{SystemTime, UNIX_EPOCH};
use toml_edit::{DocumentMut, Item, Table, value};

pub const MANIFEST_FILE_NAME: &str = "sprout.toml";
pub const MANIFEST_FORMAT_VERSION: u32 = 1;

#[derive(Clone, Debug, Deserialize, PartialEq, Eq)]
#[serde(deny_unknown_fields)]
pub struct ProjectManifest {
    pub manifest_version: u32,
    pub project: ProjectSection,
    #[serde(default)]
    pub sprout: SproutSection,
    #[serde(default)]
    pub dependencies: BTreeMap<String, String>,
    #[serde(default)]
    pub python: BTreeMap<String, String>,
    #[serde(default)]
    pub build: BuildSection,
}

#[derive(Clone, Debug, Deserialize, PartialEq, Eq)]
#[serde(deny_unknown_fields)]
pub struct ProjectSection {
    pub name: String,
    pub version: String,
    pub entry: String,
}

#[derive(Clone, Debug, Deserialize, PartialEq, Eq)]
#[serde(default, deny_unknown_fields)]
pub struct SproutSection {
    pub syntax: String,
}

impl Default for SproutSection {
    fn default() -> Self {
        Self {
            syntax: "guided".to_owned(),
        }
    }
}

#[derive(Clone, Debug, Deserialize, PartialEq, Eq)]
#[serde(default, deny_unknown_fields)]
pub struct BuildSection {
    pub target: String,
}

impl Default for BuildSection {
    fn default() -> Self {
        Self {
            target: "windows-x86_64".to_owned(),
        }
    }
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct SproutProject {
    pub root: PathBuf,
    pub manifest_path: PathBuf,
    pub manifest: ProjectManifest,
    pub entry_path: PathBuf,
    pub profile: SyntaxProfile,
    pub source_files: Vec<PathBuf>,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum DependencyKind {
    Sprout,
    Python,
}

impl DependencyKind {
    const fn section(self) -> &'static str {
        match self {
            Self::Sprout => "dependencies",
            Self::Python => "python",
        }
    }
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct BuildArtifact {
    pub path: PathBuf,
    pub target: String,
    pub source_count: usize,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum ProjectTemplate {
    Guided,
    Structured,
    Advanced,
}

impl ProjectTemplate {
    pub const ALL: [Self; 3] = [Self::Guided, Self::Structured, Self::Advanced];

    pub const fn id(self) -> &'static str {
        match self {
            Self::Guided => "guided",
            Self::Structured => "structured",
            Self::Advanced => "advanced",
        }
    }

    pub const fn profile(self) -> SyntaxProfile {
        match self {
            Self::Guided => SyntaxProfile::Guided,
            Self::Structured => SyntaxProfile::Structured,
            Self::Advanced => SyntaxProfile::Advanced,
        }
    }

    fn source(self, project_name: &str) -> String {
        match self {
            Self::Guided => {
                format!("has project = \"{project_name}\"\nprint(\"Welcome to \" + project)\n")
            }
            Self::Structured => {
                format!("string project = \"{project_name}\";\nprint(\"Welcome to \" + project);\n")
            }
            Self::Advanced => {
                format!("project := \"{project_name}\"\nprint(\"Welcome to \" + project)\n")
            }
        }
    }
}

impl FromStr for ProjectTemplate {
    type Err = ProjectError;

    fn from_str(value: &str) -> Result<Self, Self::Err> {
        match value {
            "guided" => Ok(Self::Guided),
            "structured" => Ok(Self::Structured),
            "advanced" => Ok(Self::Advanced),
            _ => Err(ProjectError::one(
                "SPP011",
                format!(
                    "Unknown project template `{value}`; expected guided, structured, or advanced."
                ),
                None,
            )),
        }
    }
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct ProjectIssue {
    pub code: String,
    pub message: String,
    pub path: Option<PathBuf>,
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct ProjectError {
    pub issues: Vec<ProjectIssue>,
}

impl ProjectError {
    fn one(code: &str, message: String, path: Option<PathBuf>) -> Self {
        Self {
            issues: vec![ProjectIssue {
                code: code.to_owned(),
                message,
                path,
            }],
        }
    }
}

impl fmt::Display for ProjectError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        for (index, issue) in self.issues.iter().enumerate() {
            if index > 0 {
                formatter.write_str("; ")?;
            }
            write!(formatter, "{}: {}", issue.code, issue.message)?;
        }
        Ok(())
    }
}

impl std::error::Error for ProjectError {}

pub fn load_project(path: impl AsRef<Path>) -> Result<SproutProject, ProjectError> {
    let requested_path = path.as_ref();
    let manifest_path =
        if requested_path.file_name().and_then(|name| name.to_str()) == Some(MANIFEST_FILE_NAME) {
            requested_path.to_path_buf()
        } else {
            requested_path.join(MANIFEST_FILE_NAME)
        };

    let manifest_path = manifest_path.canonicalize().map_err(|error| {
        ProjectError::one(
            "SPP001",
            format!("Could not find {}: {error}", manifest_path.display()),
            Some(manifest_path.clone()),
        )
    })?;
    if !manifest_path.is_file() {
        return Err(ProjectError::one(
            "SPP001",
            format!("{} is not a project manifest.", manifest_path.display()),
            Some(manifest_path),
        ));
    }

    let text = fs::read_to_string(&manifest_path).map_err(|error| {
        ProjectError::one(
            "SPP001",
            format!("Could not read {}: {error}", manifest_path.display()),
            Some(manifest_path.clone()),
        )
    })?;
    let manifest: ProjectManifest = toml::from_str(&text).map_err(|error| {
        ProjectError::one(
            "SPP002",
            format!("Invalid project manifest: {error}"),
            Some(manifest_path.clone()),
        )
    })?;
    let root = manifest_path
        .parent()
        .expect("a canonical manifest path always has a parent")
        .to_path_buf();
    let (profile, entry_relative) = validate_manifest(&manifest, &manifest_path)?;
    let entry_path = root.join(&entry_relative);
    let entry_path = entry_path.canonicalize().map_err(|error| {
        ProjectError::one(
            "SPP008",
            format!(
                "Could not load project entry {}: {error}",
                entry_path.display()
            ),
            Some(entry_path.clone()),
        )
    })?;
    if !entry_path.starts_with(&root) {
        return Err(ProjectError::one(
            "SPP009",
            "The project entry resolves outside the project directory.".to_owned(),
            Some(entry_path),
        ));
    }
    if !entry_path.is_file() {
        return Err(ProjectError::one(
            "SPP008",
            "The project entry must be a file.".to_owned(),
            Some(entry_path),
        ));
    }

    let mut source_files = Vec::new();
    collect_source_files(&root.join("src"), &mut source_files).map_err(|error| {
        ProjectError::one(
            "SPP010",
            format!("Could not scan project sources: {error}"),
            Some(root.join("src")),
        )
    })?;
    if !source_files.contains(&entry_path) {
        source_files.push(entry_path.clone());
    }
    source_files.sort();

    Ok(SproutProject {
        root,
        manifest_path,
        manifest,
        entry_path,
        profile,
        source_files,
    })
}

pub fn create_project(
    destination: impl AsRef<Path>,
    name: &str,
    template: ProjectTemplate,
) -> Result<SproutProject, ProjectError> {
    validate_project_name(name, None)?;
    let destination = destination.as_ref();
    if destination.exists() {
        return Err(ProjectError::one(
            "SPP012",
            format!("The destination {} already exists.", destination.display()),
            Some(destination.to_path_buf()),
        ));
    }
    let parent = destination.parent().ok_or_else(|| {
        ProjectError::one(
            "SPP012",
            "The project destination must have a parent directory.".to_owned(),
            Some(destination.to_path_buf()),
        )
    })?;
    fs::create_dir_all(parent).map_err(|error| {
        ProjectError::one(
            "SPP012",
            format!("Could not create the project parent directory: {error}"),
            Some(parent.to_path_buf()),
        )
    })?;

    let temporary = parent.join(format!(".sprout-create-{}-{}", std::process::id(), name));
    if temporary.exists() {
        return Err(ProjectError::one(
            "SPP012",
            "A temporary project directory already exists; retry the operation.".to_owned(),
            Some(temporary),
        ));
    }

    let result = write_template(&temporary, name, template).and_then(|()| {
        fs::rename(&temporary, destination).map_err(|error| {
            ProjectError::one(
                "SPP012",
                format!("Could not finalize the project: {error}"),
                Some(destination.to_path_buf()),
            )
        })
    });
    if let Err(error) = result {
        let _ = fs::remove_dir_all(&temporary);
        return Err(error);
    }
    load_project(destination)
}

pub fn update_dependency(
    project_path: impl AsRef<Path>,
    kind: DependencyKind,
    name: &str,
    requirement: Option<&str>,
) -> Result<SproutProject, ProjectError> {
    let project = load_project(project_path)?;
    if !is_valid_dependency_name(name) {
        return Err(ProjectError::one(
            "SPP014",
            format!("Invalid dependency name `{name}`."),
            Some(project.manifest_path.clone()),
        ));
    }
    if requirement.is_some_and(|value| value.trim().is_empty()) {
        return Err(ProjectError::one(
            "SPP014",
            "A dependency version requirement cannot be empty.".to_owned(),
            Some(project.manifest_path.clone()),
        ));
    }

    let text = fs::read_to_string(&project.manifest_path).map_err(|error| {
        ProjectError::one(
            "SPP015",
            format!("Could not read the project manifest for editing: {error}"),
            Some(project.manifest_path.clone()),
        )
    })?;
    let mut document = text.parse::<DocumentMut>().map_err(|error| {
        ProjectError::one(
            "SPP002",
            format!("Invalid project manifest: {error}"),
            Some(project.manifest_path.clone()),
        )
    })?;
    let section_name = kind.section();
    if !document.as_table().contains_key(section_name) {
        document[section_name] = Item::Table(Table::new());
    }
    let section = document[section_name].as_table_mut().ok_or_else(|| {
        ProjectError::one(
            "SPP002",
            format!("Manifest section `{section_name}` must be a table."),
            Some(project.manifest_path.clone()),
        )
    })?;
    match requirement {
        Some(requirement) => {
            section.insert(name, value(requirement.trim()));
        }
        None => {
            section.remove(name);
        }
    }
    replace_manifest(&project.manifest_path, &document.to_string())?;
    load_project(&project.root)
}

pub fn build_project_bundle(project_path: impl AsRef<Path>) -> Result<BuildArtifact, ProjectError> {
    let project = load_project(project_path)?;
    let source = fs::read_to_string(&project.entry_path).map_err(|error| {
        ProjectError::one(
            "SPP020",
            format!("Could not read the project entry before building: {error}"),
            Some(project.entry_path.clone()),
        )
    })?;
    let checked = sprout_core::check_profile(
        &sprout_core::source::SourceFile::new(project.entry_path.to_string_lossy(), source),
        project.profile,
    );
    if !checked.diagnostics.is_empty() {
        let details = checked
            .diagnostics
            .iter()
            .take(3)
            .map(|diagnostic| format!("{}: {}", diagnostic.code, diagnostic.message))
            .collect::<Vec<_>>()
            .join("; ");
        return Err(ProjectError::one(
            "SPP020",
            format!("Build stopped because the entry source is not valid: {details}"),
            Some(project.entry_path.clone()),
        ));
    }

    let build_root = project.root.join("build");
    reject_linked_directory(&build_root)?;
    fs::create_dir_all(&build_root).map_err(|error| {
        ProjectError::one(
            "SPP021",
            format!("Could not create the build directory: {error}"),
            Some(build_root.clone()),
        )
    })?;
    let unique = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .unwrap_or_default()
        .as_nanos();
    let artifact_path = build_root.join(format!(
        "{}-{}-{unique}.sproutbundle",
        project.manifest.project.name, project.manifest.build.target
    ));
    fs::create_dir(&artifact_path).map_err(|error| {
        ProjectError::one(
            "SPP021",
            format!("Could not create the build artifact: {error}"),
            Some(artifact_path.clone()),
        )
    })?;

    let result = write_bundle(&project, &artifact_path);
    if let Err(error) = result {
        let _ = fs::remove_dir_all(&artifact_path);
        return Err(error);
    }
    Ok(BuildArtifact {
        path: artifact_path,
        target: project.manifest.build.target,
        source_count: project.source_files.len(),
    })
}

fn replace_manifest(path: &Path, text: &str) -> Result<(), ProjectError> {
    let directory = path.parent().expect("a manifest always has a parent");
    let unique = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .unwrap_or_default()
        .as_nanos();
    let temporary = directory.join(format!(
        ".sprout-manifest-{}-{unique}.tmp",
        std::process::id()
    ));
    let backup = directory.join(format!(
        ".sprout-manifest-{}-{unique}.backup",
        std::process::id()
    ));
    fs::write(&temporary, text).map_err(|error| {
        ProjectError::one(
            "SPP015",
            format!("Could not stage the manifest update: {error}"),
            Some(temporary.clone()),
        )
    })?;
    if let Err(error) = fs::rename(path, &backup) {
        let _ = fs::remove_file(&temporary);
        return Err(ProjectError::one(
            "SPP015",
            format!("Could not prepare the manifest update: {error}"),
            Some(path.to_path_buf()),
        ));
    }
    if let Err(error) = fs::rename(&temporary, path) {
        let restored = fs::rename(&backup, path);
        return Err(ProjectError::one(
            "SPP015",
            match restored {
                Ok(()) => format!("Could not activate the manifest update: {error}"),
                Err(restore_error) => format!(
                    "Could not activate the manifest update: {error}. Recovery also failed: {restore_error}. The original remains at {}.",
                    backup.display()
                ),
            },
            Some(path.to_path_buf()),
        ));
    }
    let _ = fs::remove_file(backup);
    Ok(())
}

fn reject_linked_directory(path: &Path) -> Result<(), ProjectError> {
    match fs::symlink_metadata(path) {
        Ok(metadata) if metadata.file_type().is_symlink() => Err(ProjectError::one(
            "SPP021",
            "The build directory cannot be a filesystem link.".to_owned(),
            Some(path.to_path_buf()),
        )),
        Ok(metadata) if !metadata.is_dir() => Err(ProjectError::one(
            "SPP021",
            "The build path exists but is not a directory.".to_owned(),
            Some(path.to_path_buf()),
        )),
        Ok(_) => Ok(()),
        Err(error) if error.kind() == std::io::ErrorKind::NotFound => Ok(()),
        Err(error) => Err(ProjectError::one(
            "SPP021",
            format!("Could not inspect the build directory: {error}"),
            Some(path.to_path_buf()),
        )),
    }
}

fn write_bundle(project: &SproutProject, artifact_path: &Path) -> Result<(), ProjectError> {
    for source in &project.source_files {
        let relative = source.strip_prefix(&project.root).map_err(|_| {
            ProjectError::one(
                "SPP021",
                "A project source resolved outside the project root.".to_owned(),
                Some(source.clone()),
            )
        })?;
        let destination = artifact_path.join(relative);
        if let Some(parent) = destination.parent() {
            fs::create_dir_all(parent).map_err(|error| {
                ProjectError::one(
                    "SPP021",
                    format!("Could not create an artifact directory: {error}"),
                    Some(parent.to_path_buf()),
                )
            })?;
        }
        fs::copy(source, &destination).map_err(|error| {
            ProjectError::one(
                "SPP021",
                format!("Could not copy a project source: {error}"),
                Some(source.clone()),
            )
        })?;
    }
    fs::copy(
        &project.manifest_path,
        artifact_path.join(MANIFEST_FILE_NAME),
    )
    .and_then(|_| {
        fs::write(
            artifact_path.join("README.txt"),
            "Sprout validated interpreted bundle\n\nRun this bundle with a compatible Sprout runtime. This is not a native executable.\n",
        )
    })
    .map_err(|error| {
        ProjectError::one(
            "SPP021",
            format!("Could not finalize the build artifact: {error}"),
            Some(artifact_path.to_path_buf()),
        )
    })
}

fn validate_manifest(
    manifest: &ProjectManifest,
    manifest_path: &Path,
) -> Result<(SyntaxProfile, PathBuf), ProjectError> {
    let mut issues = Vec::new();
    if manifest.manifest_version != MANIFEST_FORMAT_VERSION {
        issues.push(ProjectIssue {
            code: "SPP003".to_owned(),
            message: format!(
                "Unsupported manifest version {}; expected {}.",
                manifest.manifest_version, MANIFEST_FORMAT_VERSION
            ),
            path: Some(manifest_path.to_path_buf()),
        });
    }
    if let Err(error) = validate_project_name(&manifest.project.name, Some(manifest_path)) {
        issues.extend(error.issues);
    }
    if !is_semantic_version(&manifest.project.version) {
        issues.push(ProjectIssue {
            code: "SPP005".to_owned(),
            message: "Project version must use major.minor.patch numeric form.".to_owned(),
            path: Some(manifest_path.to_path_buf()),
        });
    }
    let entry = PathBuf::from(&manifest.project.entry);
    if !is_safe_relative_source(&entry) {
        issues.push(ProjectIssue {
            code: "SPP006".to_owned(),
            message: "Project entry must be a relative .spr path without parent traversal."
                .to_owned(),
            path: Some(manifest_path.to_path_buf()),
        });
    }
    let profile = match manifest.sprout.syntax.as_str() {
        "guided" => Some(SyntaxProfile::Guided),
        "structured" => Some(SyntaxProfile::Structured),
        "advanced" => Some(SyntaxProfile::Advanced),
        _ => {
            issues.push(ProjectIssue {
                code: "SPP007".to_owned(),
                message: "Sprout syntax must be guided, structured, or advanced.".to_owned(),
                path: Some(manifest_path.to_path_buf()),
            });
            None
        }
    };
    validate_dependency_map(
        "dependencies",
        &manifest.dependencies,
        manifest_path,
        &mut issues,
    );
    validate_dependency_map("python", &manifest.python, manifest_path, &mut issues);
    if !is_valid_dependency_name(&manifest.build.target) {
        issues.push(ProjectIssue {
            code: "SPP013".to_owned(),
            message: "Build target must be a safe non-empty artifact name.".to_owned(),
            path: Some(manifest_path.to_path_buf()),
        });
    }

    if issues.is_empty() {
        Ok((profile.expect("validated profile exists"), entry))
    } else {
        Err(ProjectError { issues })
    }
}

fn validate_project_name(name: &str, path: Option<&Path>) -> Result<(), ProjectError> {
    let mut characters = name.chars();
    let valid = characters
        .next()
        .is_some_and(|character| character.is_ascii_alphabetic())
        && characters.all(|character| {
            character.is_ascii_alphanumeric() || character == '-' || character == '_'
        });
    if valid && name.len() <= 64 {
        Ok(())
    } else {
        Err(ProjectError::one(
            "SPP004",
            "Project name must be at most 64 characters, start with a letter, and contain only letters, numbers, '-' or '_'.".to_owned(),
            path.map(Path::to_path_buf),
        ))
    }
}

fn validate_dependency_map(
    section: &str,
    values: &BTreeMap<String, String>,
    manifest_path: &Path,
    issues: &mut Vec<ProjectIssue>,
) {
    for (name, version) in values {
        let valid_name = is_valid_dependency_name(name);
        if !valid_name || version.trim().is_empty() {
            issues.push(ProjectIssue {
                code: "SPP014".to_owned(),
                message: format!(
                    "Invalid `{section}` entry `{name}`; names must start and end with a letter or number, use only letters, numbers, '-', '_' or '.', and have a non-empty version requirement."
                ),
                path: Some(manifest_path.to_path_buf()),
            });
        }
    }
}

fn is_valid_dependency_name(name: &str) -> bool {
    name.chars()
        .next()
        .is_some_and(|character| character.is_ascii_alphanumeric())
        && name
            .chars()
            .last()
            .is_some_and(|character| character.is_ascii_alphanumeric())
        && name.chars().all(|character| {
            character.is_ascii_alphanumeric() || matches!(character, '-' | '_' | '.')
        })
}

fn is_semantic_version(version: &str) -> bool {
    let parts: Vec<&str> = version.split('.').collect();
    parts.len() == 3
        && parts.iter().all(|part| {
            !part.is_empty() && part.chars().all(|character| character.is_ascii_digit())
        })
}

fn is_safe_relative_source(path: &Path) -> bool {
    !path.as_os_str().is_empty()
        && !path.is_absolute()
        && path.extension().and_then(|extension| extension.to_str()) == Some("spr")
        && path
            .components()
            .all(|component| matches!(component, Component::Normal(_) | Component::CurDir))
}

fn collect_source_files(directory: &Path, output: &mut Vec<PathBuf>) -> std::io::Result<()> {
    let metadata = match fs::symlink_metadata(directory) {
        Ok(metadata) => metadata,
        Err(error) if error.kind() == std::io::ErrorKind::NotFound => return Ok(()),
        Err(error) => return Err(error),
    };
    if metadata.file_type().is_symlink() || !metadata.is_dir() {
        return Ok(());
    }
    let mut entries: Vec<_> = fs::read_dir(directory)?.collect::<Result<_, _>>()?;
    entries.sort_by_key(std::fs::DirEntry::path);
    for entry in entries {
        let file_type = entry.file_type()?;
        if file_type.is_symlink() {
            continue;
        }
        let path = entry.path();
        if file_type.is_dir() {
            collect_source_files(&path, output)?;
        } else if file_type.is_file()
            && path.extension().and_then(|extension| extension.to_str()) == Some("spr")
        {
            output.push(path.canonicalize()?);
        }
    }
    Ok(())
}

fn write_template(
    directory: &Path,
    name: &str,
    template: ProjectTemplate,
) -> Result<(), ProjectError> {
    fs::create_dir_all(directory.join("src"))
        .and_then(|()| fs::create_dir_all(directory.join(".sprout/python")))
        .and_then(|()| {
            fs::write(
                directory.join(MANIFEST_FILE_NAME),
                manifest_text(name, template),
            )
        })
        .and_then(|()| fs::write(directory.join("src/main.spr"), template.source(name)))
        .and_then(|()| fs::write(directory.join(".gitignore"), ".sprout/\n"))
        .map_err(|error| {
            ProjectError::one(
                "SPP012",
                format!("Could not write the project template: {error}"),
                Some(directory.to_path_buf()),
            )
        })
}

fn manifest_text(name: &str, template: ProjectTemplate) -> String {
    format!(
        "manifest_version = 1\n\n[project]\nname = \"{name}\"\nversion = \"0.1.0\"\nentry = \"src/main.spr\"\n\n[sprout]\nsyntax = \"{}\"\n\n[dependencies]\n\n[python]\n\n[build]\ntarget = \"windows-x86_64\"\n",
        template.id()
    )
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::time::{SystemTime, UNIX_EPOCH};

    struct TestDirectory(PathBuf);

    impl TestDirectory {
        fn new(name: &str) -> Self {
            let unique = SystemTime::now()
                .duration_since(UNIX_EPOCH)
                .unwrap()
                .as_nanos();
            Self(std::env::temp_dir().join(format!(
                "sprout-project-{}-{name}-{unique}",
                std::process::id()
            )))
        }
    }

    impl Drop for TestDirectory {
        fn drop(&mut self) {
            let _ = fs::remove_dir_all(&self.0);
        }
    }

    #[test]
    fn every_template_creates_a_loadable_project() {
        for template in ProjectTemplate::ALL {
            let parent = TestDirectory::new(template.id());
            let destination = parent.0.join("HelloSprout");
            let project = create_project(&destination, "HelloSprout", template).unwrap();
            assert_eq!(project.profile, template.profile());
            assert_eq!(
                project.entry_path,
                destination.join("src/main.spr").canonicalize().unwrap()
            );
            assert_eq!(project.source_files, vec![project.entry_path.clone()]);
            assert!(destination.join(".sprout/python").is_dir());
            assert!(destination.join(".gitignore").is_file());
        }
    }

    #[test]
    fn rejects_unknown_manifest_versions() {
        let parent = TestDirectory::new("version");
        let destination = parent.0.join("Versioned");
        create_project(&destination, "Versioned", ProjectTemplate::Guided).unwrap();
        let path = destination.join(MANIFEST_FILE_NAME);
        let text = fs::read_to_string(&path).unwrap();
        fs::write(
            &path,
            text.replace("manifest_version = 1", "manifest_version = 99"),
        )
        .unwrap();
        let error = load_project(&destination).unwrap_err();
        assert_eq!(error.issues[0].code, "SPP003");
    }

    #[test]
    fn rejects_entry_parent_traversal() {
        let parent = TestDirectory::new("traversal");
        let destination = parent.0.join("SafeProject");
        create_project(&destination, "SafeProject", ProjectTemplate::Guided).unwrap();
        let path = destination.join(MANIFEST_FILE_NAME);
        let text = fs::read_to_string(&path).unwrap();
        fs::write(&path, text.replace("src/main.spr", "../outside.spr")).unwrap();
        let error = load_project(&destination).unwrap_err();
        assert!(error.issues.iter().any(|issue| issue.code == "SPP006"));
    }

    #[test]
    fn reports_multiple_manifest_validation_errors() {
        let parent = TestDirectory::new("multiple");
        fs::create_dir_all(&parent.0).unwrap();
        fs::write(
            parent.0.join(MANIFEST_FILE_NAME),
            "manifest_version = 2\n[project]\nname = \"bad name\"\nversion = \"one\"\nentry = \"main.txt\"\n[sprout]\nsyntax = \"mystery\"\n",
        )
        .unwrap();
        let error = load_project(&parent.0).unwrap_err();
        let codes: Vec<&str> = error
            .issues
            .iter()
            .map(|issue| issue.code.as_str())
            .collect();
        assert_eq!(codes, ["SPP003", "SPP004", "SPP005", "SPP006", "SPP007"]);
    }

    #[test]
    fn refuses_to_overwrite_an_existing_destination() {
        let parent = TestDirectory::new("existing");
        fs::create_dir_all(&parent.0).unwrap();
        let error = create_project(&parent.0, "Existing", ProjectTemplate::Guided).unwrap_err();
        assert_eq!(error.issues[0].code, "SPP012");
    }

    #[test]
    fn rejects_unsafe_dependency_names_and_empty_requirements() {
        let parent = TestDirectory::new("dependencies");
        let destination = parent.0.join("Dependencies");
        create_project(&destination, "Dependencies", ProjectTemplate::Guided).unwrap();
        let path = destination.join(MANIFEST_FILE_NAME);
        let text = fs::read_to_string(&path).unwrap();
        fs::write(
            &path,
            text.replace(
                "[dependencies]\n\n[python]",
                "[dependencies]\n\"..\" = \"1.0\"\n\"-escape\" = \"1.0\"\n\"valid.package\" = \"1.0\"\n\n[python]\nnumpy = \"\"",
            ),
        )
        .unwrap();
        let error = load_project(&destination).unwrap_err();
        assert_eq!(
            error
                .issues
                .iter()
                .filter(|issue| issue.code == "SPP014")
                .count(),
            3
        );
    }

    #[test]
    fn package_updates_preserve_manifest_comments_and_reload() {
        let parent = TestDirectory::new("package-update");
        let destination = parent.0.join("Packages");
        let project = create_project(&destination, "Packages", ProjectTemplate::Guided).unwrap();
        let manifest = fs::read_to_string(&project.manifest_path).unwrap();
        fs::write(
            &project.manifest_path,
            manifest.replace("[python]", "# kept by the package hub\n[python]"),
        )
        .unwrap();

        let updated = update_dependency(
            &destination,
            DependencyKind::Python,
            "pygame",
            Some("2.6.*"),
        )
        .unwrap();
        assert_eq!(
            updated.manifest.python.get("pygame"),
            Some(&"2.6.*".to_owned())
        );
        assert!(
            fs::read_to_string(&updated.manifest_path)
                .unwrap()
                .contains("# kept by the package hub")
        );

        let removed =
            update_dependency(&destination, DependencyKind::Python, "pygame", None).unwrap();
        assert!(!removed.manifest.python.contains_key("pygame"));
    }

    #[test]
    fn build_creates_a_validated_interpreted_bundle() {
        let parent = TestDirectory::new("build");
        let destination = parent.0.join("Buildable");
        create_project(&destination, "Buildable", ProjectTemplate::Guided).unwrap();

        let artifact = build_project_bundle(&destination).unwrap();
        assert_eq!(artifact.target, "windows-x86_64");
        assert_eq!(artifact.source_count, 1);
        assert!(artifact.path.join("sprout.toml").is_file());
        assert!(artifact.path.join("src/main.spr").is_file());
        let readme = fs::read_to_string(artifact.path.join("README.txt")).unwrap();
        assert!(readme.contains("not a native executable"));
    }

    #[test]
    fn build_stops_when_the_entry_has_diagnostics() {
        let parent = TestDirectory::new("broken-build");
        let destination = parent.0.join("BrokenBuild");
        let project = create_project(&destination, "BrokenBuild", ProjectTemplate::Guided).unwrap();
        fs::write(&project.entry_path, "print(missing)\n").unwrap();

        let error = build_project_bundle(&destination).unwrap_err();
        assert_eq!(error.issues[0].code, "SPP020");
        assert!(!destination.join("build").exists());
    }

    #[test]
    fn source_discovery_does_not_follow_a_symlinked_src_directory() {
        let parent = TestDirectory::new("source-symlink");
        let destination = parent.0.join("Contained");
        create_project(&destination, "Contained", ProjectTemplate::Guided).unwrap();

        let manifest_path = destination.join(MANIFEST_FILE_NAME);
        let manifest = fs::read_to_string(&manifest_path).unwrap();
        fs::write(&manifest_path, manifest.replace("src/main.spr", "main.spr")).unwrap();
        fs::write(destination.join("main.spr"), "print(\"contained\")\n").unwrap();
        fs::remove_dir_all(destination.join("src")).unwrap();

        let outside = parent.0.join("outside");
        fs::create_dir_all(&outside).unwrap();
        fs::write(outside.join("private.spr"), "print(\"outside\")\n").unwrap();
        if let Err(error) = create_directory_symlink(&outside, &destination.join("src")) {
            #[cfg(windows)]
            if error.kind() == std::io::ErrorKind::PermissionDenied {
                return;
            }
            panic!("could not create test directory symlink: {error}");
        }

        let project = load_project(&destination).unwrap();
        assert_eq!(project.source_files, vec![project.entry_path.clone()]);
        assert!(
            !project
                .source_files
                .iter()
                .any(|path| path.starts_with(&outside))
        );
    }

    #[cfg(unix)]
    fn create_directory_symlink(target: &Path, link: &Path) -> std::io::Result<()> {
        std::os::unix::fs::symlink(target, link)
    }

    #[cfg(windows)]
    fn create_directory_symlink(target: &Path, link: &Path) -> std::io::Result<()> {
        std::os::windows::fs::symlink_dir(target, link)
    }
}
