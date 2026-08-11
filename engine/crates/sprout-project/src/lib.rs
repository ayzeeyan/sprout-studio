//! Shared Sprout project manifest, validation, loading, and template APIs.

use serde::Deserialize;
use sprout_core::SyntaxProfile;
use std::collections::BTreeMap;
use std::fmt;
use std::fs;
use std::path::{Component, Path, PathBuf};
use std::str::FromStr;

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
    if manifest.build.target.trim().is_empty() {
        issues.push(ProjectIssue {
            code: "SPP013".to_owned(),
            message: "Build target cannot be empty.".to_owned(),
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
            "Project name must start with a letter and contain only letters, numbers, '-' or '_'."
                .to_owned(),
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
        let valid_name = !name.is_empty()
            && name.chars().all(|character| {
                character.is_ascii_alphanumeric() || matches!(character, '-' | '_' | '.')
            });
        if !valid_name || version.trim().is_empty() {
            issues.push(ProjectIssue {
                code: "SPP014".to_owned(),
                message: format!(
                    "Invalid `{section}` entry `{name}`; names and version requirements must be non-empty."
                ),
                path: Some(manifest_path.to_path_buf()),
            });
        }
    }
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
    if !directory.exists() {
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
        .and_then(|()| fs::create_dir_all(directory.join(".sprout")))
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
            assert!(destination.join(".sprout").is_dir());
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
}
