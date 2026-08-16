//! Embedded CPython host for explicit Sprout interoperability.

use pyo3::IntoPyObjectExt;
use pyo3::prelude::*;
use pyo3::types::{PyBool, PyDict, PyFloat, PyInt, PyList, PyString, PyTuple};
use sprout_core::interop::{ForeignError, ForeignObject, ForeignValue, PythonHost};
use std::collections::{BTreeMap, HashSet};
use std::fmt;
use std::path::{Path, PathBuf};
use std::process::Command;
use std::rc::Rc;
use std::time::{Duration, Instant, SystemTime, UNIX_EPOCH};

const MAX_EAGER_CONVERSION_DEPTH: usize = 16;
const MAX_EAGER_COLLECTION_ITEMS: usize = 1_024;

pub struct EmbeddedPythonHost {
    project_packages: Option<PathBuf>,
    configured: bool,
    baseline_modules: HashSet<String>,
}

impl EmbeddedPythonHost {
    pub fn new(project_packages: Option<PathBuf>) -> Result<Self, ForeignError> {
        if let Some(path) = &project_packages {
            std::fs::create_dir_all(path).map_err(|error| {
                ForeignError::new(
                    "ProjectEnvironmentError",
                    format!("Could not create {}: {error}", path.display()),
                )
            })?;
        }
        Ok(Self {
            project_packages,
            configured: false,
            baseline_modules: HashSet::new(),
        })
    }

    fn configure(&mut self, py: Python<'_>) -> PyResult<()> {
        if self.configured {
            return Ok(());
        }
        let sys = py.import("sys")?;
        let modules = sys.getattr("modules")?.cast_into::<PyDict>()?;
        self.baseline_modules = modules
            .keys()
            .iter()
            .filter_map(|name| name.extract::<String>().ok())
            .collect();
        if let Some(path) = &self.project_packages {
            let path = path.to_string_lossy();
            sys.getattr("path")?
                .call_method1("insert", (0, path.as_ref()))?;
        }
        sys.setattr("dont_write_bytecode", true)?;
        self.configured = true;
        Ok(())
    }

    fn clean_project_modules(&mut self, py: Python<'_>) -> PyResult<()> {
        if !self.configured {
            return Ok(());
        }
        let Some(project_packages) = &self.project_packages else {
            return Ok(());
        };
        let sys = py.import("sys")?;
        let path = sys.getattr("path")?;
        let project_text = project_packages.to_string_lossy();
        let _ = path.call_method1("remove", (project_text.as_ref(),));

        let modules = sys.getattr("modules")?.cast_into::<PyDict>()?;
        let mut remove = Vec::new();
        let module_names: Vec<String> = modules
            .keys()
            .iter()
            .filter_map(|name| name.extract::<String>().ok())
            .collect();
        for name in module_names {
            if self.baseline_modules.contains(&name) {
                continue;
            }
            let Some(module) = modules.get_item(&name)? else {
                continue;
            };
            let Ok(file) = module
                .getattr("__file__")
                .and_then(|file| file.extract::<String>())
            else {
                continue;
            };
            if path_is_within(Path::new(&file), project_packages) {
                remove.push(name);
            }
        }
        for name in remove {
            modules.del_item(name)?;
        }
        Ok(())
    }
}

impl PythonHost for EmbeddedPythonHost {
    fn import_module(&mut self, module: &str) -> Result<ForeignValue, ForeignError> {
        Python::attach(|py| {
            self.configure(py)
                .map_err(|error| foreign_error(py, error))?;
            let module = py
                .import(module)
                .map_err(|error| foreign_error(py, error))?;
            from_python(module.into_any(), 0).map_err(|error| foreign_error(py, error))
        })
    }
}

impl Drop for EmbeddedPythonHost {
    fn drop(&mut self) {
        Python::attach(|py| {
            let _ = self.clean_project_modules(py);
        });
    }
}

struct PythonObjectProxy {
    object: Py<PyAny>,
    type_name: String,
}

impl fmt::Debug for PythonObjectProxy {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(formatter, "PythonObject<{}>", self.type_name)
    }
}

impl ForeignObject for PythonObjectProxy {
    fn as_any(&self) -> &dyn std::any::Any {
        self
    }

    fn type_name(&self) -> String {
        self.type_name.clone()
    }

    fn get_member(&self, name: &str) -> Result<ForeignValue, ForeignError> {
        Python::attach(|py| {
            let value = self
                .object
                .bind(py)
                .getattr(name)
                .map_err(|error| foreign_error(py, error))?;
            from_python(value, 0).map_err(|error| foreign_error(py, error))
        })
    }

    fn get_index(&self, index: &ForeignValue) -> Result<ForeignValue, ForeignError> {
        Python::attach(|py| {
            let index = to_python(py, index).map_err(|error| foreign_error(py, error))?;
            let value = self
                .object
                .bind(py)
                .get_item(index)
                .map_err(|error| foreign_error(py, error))?;
            from_python(value, 0).map_err(|error| foreign_error(py, error))
        })
    }

    fn call(&self, arguments: &[ForeignValue]) -> Result<ForeignValue, ForeignError> {
        Python::attach(|py| {
            let values = arguments
                .iter()
                .map(|value| to_python(py, value))
                .collect::<PyResult<Vec<_>>>()?;
            let arguments = PyTuple::new(py, values)?;
            let result = self.object.bind(py).call1(arguments)?;
            from_python(result, 0)
        })
        .map_err(|error| Python::attach(|py| foreign_error(py, error)))
    }

    fn display(&self) -> Result<String, ForeignError> {
        Python::attach(|py| {
            self.object
                .bind(py)
                .str()
                .and_then(|value| value.to_str().map(str::to_owned))
                .map_err(|error| foreign_error(py, error))
        })
    }
}

fn from_python(value: Bound<'_, PyAny>, depth: usize) -> PyResult<ForeignValue> {
    if value.is_none() {
        return Ok(ForeignValue::Nil);
    }
    if value.is_instance_of::<PyBool>() {
        return value.extract().map(ForeignValue::Bool);
    }
    if value.is_instance_of::<PyInt>() {
        return value.extract().map(ForeignValue::Int);
    }
    if value.is_instance_of::<PyFloat>() {
        return value.extract().map(ForeignValue::Float);
    }
    if value.is_instance_of::<PyString>() {
        return value.extract().map(ForeignValue::String);
    }
    if depth < MAX_EAGER_CONVERSION_DEPTH
        && let Ok(list) = value.clone().cast_into::<PyList>()
        && list.len() <= MAX_EAGER_COLLECTION_ITEMS
    {
        return list
            .iter()
            .map(|item| from_python(item, depth + 1))
            .collect::<PyResult<Vec<_>>>()
            .map(ForeignValue::List);
    }
    if depth < MAX_EAGER_CONVERSION_DEPTH
        && let Ok(dictionary) = value.clone().cast_into::<PyDict>()
        && dictionary.len() <= MAX_EAGER_COLLECTION_ITEMS
    {
        let mut converted = BTreeMap::new();
        let mut all_string_keys = true;
        for (key, item) in dictionary.iter() {
            let Ok(key) = key.extract::<String>() else {
                all_string_keys = false;
                break;
            };
            converted.insert(key, from_python(item, depth + 1)?);
        }
        if all_string_keys {
            return Ok(ForeignValue::Map(converted));
        }
    }
    let type_name = value.get_type().name()?.to_str()?.to_owned();
    Ok(ForeignValue::Object(Rc::new(PythonObjectProxy {
        object: value.unbind(),
        type_name,
    })))
}

fn to_python(py: Python<'_>, value: &ForeignValue) -> PyResult<Py<PyAny>> {
    match value {
        ForeignValue::Int(value) => value.into_py_any(py),
        ForeignValue::Float(value) => value.into_py_any(py),
        ForeignValue::Bool(value) => value.into_py_any(py),
        ForeignValue::String(value) => value.into_py_any(py),
        ForeignValue::Nil => Ok(py.None()),
        ForeignValue::List(values) => {
            let values = values
                .iter()
                .map(|value| to_python(py, value))
                .collect::<PyResult<Vec<_>>>()?;
            Ok(PyList::new(py, values)?.into_any().unbind())
        }
        ForeignValue::Map(values) => {
            let dictionary = PyDict::new(py);
            for (key, value) in values {
                dictionary.set_item(key, to_python(py, value)?)?;
            }
            Ok(dictionary.into_any().unbind())
        }
        ForeignValue::Object(object) => object
            .as_any()
            .downcast_ref::<PythonObjectProxy>()
            .map(|proxy| proxy.object.clone_ref(py))
            .ok_or_else(|| {
                pyo3::exceptions::PyTypeError::new_err(
                    "A foreign object from another host cannot be passed to Python.",
                )
            }),
    }
}

fn foreign_error(py: Python<'_>, error: PyErr) -> ForeignError {
    let exception_type = error
        .get_type(py)
        .name()
        .and_then(|name| name.to_str().map(str::to_owned))
        .unwrap_or_else(|_| "PythonError".to_owned());
    let message = error
        .value(py)
        .str()
        .and_then(|value| value.to_str().map(str::to_owned))
        .unwrap_or_else(|_| "Python did not provide an exception message.".to_owned());
    ForeignError::new(exception_type, message)
}

fn path_is_within(candidate: &Path, parent: &Path) -> bool {
    let candidate = candidate
        .canonicalize()
        .unwrap_or_else(|_| candidate.to_path_buf());
    let parent = parent
        .canonicalize()
        .unwrap_or_else(|_| parent.to_path_buf());
    candidate.starts_with(parent)
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct PackageSyncResult {
    pub exit_code: Option<i32>,
    pub stdout: String,
    pub stderr: String,
    pub duration: Duration,
}

pub fn sync_project_packages(
    python_executable: &Path,
    project_root: &Path,
    requirements: &BTreeMap<String, String>,
) -> Result<PackageSyncResult, ForeignError> {
    let environment_root = project_root.join(".sprout");
    std::fs::create_dir_all(&environment_root).map_err(|error| {
        ForeignError::new(
            "ProjectEnvironmentError",
            format!("Could not create {}: {error}", environment_root.display()),
        )
    })?;
    let target = environment_root.join("python");
    reject_linked_environment(&target)?;
    let unique = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .unwrap_or(Duration::ZERO)
        .as_nanos();
    let staging = environment_root.join(format!(".python-sync-{}-{unique}", std::process::id()));
    std::fs::create_dir(&staging).map_err(|error| {
        ForeignError::new(
            "ProjectEnvironmentError",
            format!("Could not create {}: {error}", staging.display()),
        )
    })?;

    let mut result = PackageSyncResult {
        exit_code: Some(0),
        stdout: String::new(),
        stderr: String::new(),
        duration: Duration::ZERO,
    };
    if !requirements.is_empty() {
        result = match run_pip(python_executable, &staging, requirements) {
            Ok(result) => result,
            Err(error) => {
                return match std::fs::remove_dir_all(&staging) {
                    Ok(()) => Err(error),
                    Err(cleanup_error) => Err(ForeignError::new(
                        error.exception_type,
                        format!(
                            "{} Cleanup of {} also failed: {cleanup_error}",
                            error.message,
                            staging.display()
                        ),
                    )),
                };
            }
        };
        if result.exit_code != Some(0) {
            if let Err(error) = std::fs::remove_dir_all(&staging) {
                result.stderr.push_str(&format!(
                    "\nSprout could not remove the failed staging environment {}: {error}\n",
                    staging.display()
                ));
            }
            return Ok(result);
        }
    }
    if let Err(error) = replace_environment(&staging, &target) {
        if !staging.exists() {
            return Err(error);
        }
        return match std::fs::remove_dir_all(&staging) {
            Ok(()) => Err(error),
            Err(cleanup_error) => Err(ForeignError::new(
                error.exception_type,
                format!(
                    "{} Cleanup of {} also failed: {cleanup_error}",
                    error.message,
                    staging.display()
                ),
            )),
        };
    }
    Ok(result)
}

fn run_pip(
    python_executable: &Path,
    target: &Path,
    requirements: &BTreeMap<String, String>,
) -> Result<PackageSyncResult, ForeignError> {
    let mut command = Command::new(python_executable);
    command.args([
        "-s",
        "-m",
        "pip",
        "install",
        "--disable-pip-version-check",
        "--no-input",
        "--upgrade",
        "--target",
    ]);
    command.arg(target);
    for (name, requirement) in requirements {
        command.arg(format_requirement(name, requirement));
    }
    let started = Instant::now();
    let output = command.output().map_err(|error| {
        ForeignError::new(
            "PackageProcessError",
            format!("Could not start {}: {error}", python_executable.display()),
        )
    })?;
    Ok(PackageSyncResult {
        exit_code: output.status.code(),
        stdout: String::from_utf8_lossy(&output.stdout).into_owned(),
        stderr: String::from_utf8_lossy(&output.stderr).into_owned(),
        duration: started.elapsed(),
    })
}

fn reject_linked_environment(target: &Path) -> Result<(), ForeignError> {
    match std::fs::symlink_metadata(target) {
        Ok(metadata) if metadata.file_type().is_symlink() => Err(ForeignError::new(
            "ProjectEnvironmentError",
            format!(
                "Refusing to synchronize {} because it is a filesystem link.",
                target.display()
            ),
        )),
        Ok(metadata) if !metadata.is_dir() => Err(ForeignError::new(
            "ProjectEnvironmentError",
            format!("{} is not a directory.", target.display()),
        )),
        Ok(_) => Ok(()),
        Err(error) if error.kind() == std::io::ErrorKind::NotFound => Ok(()),
        Err(error) => Err(ForeignError::new(
            "ProjectEnvironmentError",
            format!("Could not inspect {}: {error}", target.display()),
        )),
    }
}

fn replace_environment(staging: &Path, target: &Path) -> Result<(), ForeignError> {
    if !target.exists() {
        return std::fs::rename(staging, target).map_err(|error| {
            ForeignError::new(
                "ProjectEnvironmentError",
                format!("Could not activate {}: {error}", target.display()),
            )
        });
    }

    let backup = target.with_file_name(format!(".python-backup-{}", std::process::id()));
    if backup.exists() {
        return Err(ForeignError::new(
            "ProjectEnvironmentError",
            format!(
                "The recovery directory {} already exists; inspect it before retrying.",
                backup.display()
            ),
        ));
    }
    std::fs::rename(target, &backup).map_err(|error| {
        ForeignError::new(
            "ProjectEnvironmentError",
            format!(
                "Could not prepare {} for replacement: {error}",
                target.display()
            ),
        )
    })?;
    if let Err(activation_error) = std::fs::rename(staging, target) {
        return match std::fs::rename(&backup, target) {
            Ok(()) => Err(ForeignError::new(
                "ProjectEnvironmentError",
                format!(
                    "Could not activate {}: {activation_error}. The previous environment was restored.",
                    target.display()
                ),
            )),
            Err(restore_error) => Err(ForeignError::new(
                "ProjectEnvironmentError",
                format!(
                    "Could not activate {}: {activation_error}. Restoring {} also failed: {restore_error}.",
                    target.display(),
                    backup.display()
                ),
            )),
        };
    }
    std::fs::remove_dir_all(&backup).map_err(|error| {
        ForeignError::new(
            "ProjectEnvironmentError",
            format!(
                "The new environment is active, but the old environment at {} could not be removed: {error}",
                backup.display()
            ),
        )
    })?;
    Ok(())
}

fn format_requirement(name: &str, requirement: &str) -> String {
    if requirement.starts_with(['=', '<', '>', '!', '~']) {
        format!("{name}{requirement}")
    } else {
        format!("{name}=={requirement}")
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use sprout_core::source::SourceFile;
    use sprout_core::{SyntaxProfile, execute_profile_with_python};
    use std::time::{SystemTime, UNIX_EPOCH};

    struct TestDirectory(PathBuf);

    impl TestDirectory {
        fn new(name: &str) -> Self {
            let unique = SystemTime::now()
                .duration_since(UNIX_EPOCH)
                .expect("clock is after the Unix epoch")
                .as_nanos();
            let path = std::env::temp_dir().join(format!(
                "sprout-python-{}-{name}-{unique}",
                std::process::id()
            ));
            std::fs::create_dir_all(&path).expect("test directory can be created");
            Self(path)
        }
    }

    impl Drop for TestDirectory {
        fn drop(&mut self) {
            let _ = std::fs::remove_dir_all(&self.0);
        }
    }

    fn execute(source: &str, packages: Option<PathBuf>) -> sprout_core::ExecutionResult {
        let mut python = EmbeddedPythonHost::new(packages).expect("Python host can start");
        execute_profile_with_python(
            &SourceFile::new("python.spr", source),
            SyntaxProfile::Guided,
            &mut python,
        )
    }

    #[test]
    fn imports_cpython_and_converts_primitives() {
        let result = execute(
            "import python \"math\" as math\nprint(math.sqrt(81))\nprint(math.isfinite(1))\n",
            None,
        );
        assert!(result.diagnostics.is_empty(), "{:?}", result.diagnostics);
        assert_eq!(result.output, ["9", "true"]);
    }

    #[test]
    fn proxies_python_objects_and_methods() {
        let result = execute(
            "import python \"datetime\" as datetime\nhas day = datetime.date(2026, 8, 11)\nprint(day.isoformat())\n",
            None,
        );
        assert!(result.diagnostics.is_empty(), "{:?}", result.diagnostics);
        assert_eq!(result.output, ["2026-08-11"]);
    }

    #[test]
    fn converts_safe_python_collections() {
        let result = execute(
            "import python \"json\" as json\nhas data = json.loads(\"{\\\"items\\\": [1, 2, true]}\")\nprint(data[\"items\"][1])\n",
            None,
        );
        assert!(result.diagnostics.is_empty(), "{:?}", result.diagnostics);
        assert_eq!(result.output, ["2"]);
    }

    #[test]
    fn keeps_large_python_collections_behind_an_indexable_proxy() {
        let result = execute(
            "import python \"builtins\" as builtins\nhas values = builtins.list(builtins.range(2000))\nprint(values[1999])\n",
            None,
        );
        assert!(result.diagnostics.is_empty(), "{:?}", result.diagnostics);
        assert_eq!(result.output, ["1999"]);
    }

    #[test]
    fn translates_python_exceptions_to_sprout_diagnostics() {
        let result = execute(
            "import python \"math\" as math\nprint(math.sqrt(-1))\n",
            None,
        );
        assert_eq!(result.diagnostics.len(), 1);
        let diagnostic = result.diagnostics.first().expect("one diagnostic");
        assert_eq!(diagnostic.code, "SPT301");
        assert!(
            diagnostic.message.contains("ValueError"),
            "{}",
            diagnostic.message
        );
    }

    #[test]
    fn project_paths_and_module_cache_are_isolated() {
        let first = TestDirectory::new("first");
        let second = TestDirectory::new("second");
        std::fs::write(
            first.0.join("project_module.py"),
            "def value():\n    return 'one'\n",
        )
        .expect("first module can be written");
        std::fs::write(
            second.0.join("project_module.py"),
            "def value():\n    return 'two'\n",
        )
        .expect("second module can be written");

        let first_result = execute(
            "import python \"project_module\" as module\nprint(module.value())\n",
            Some(first.0.clone()),
        );
        assert_eq!(first_result.output, ["one"]);
        drop(first_result);

        let second_result = execute(
            "import python \"project_module\" as module\nprint(module.value())\n",
            Some(second.0.clone()),
        );
        assert!(
            second_result.diagnostics.is_empty(),
            "{:?}",
            second_result.diagnostics
        );
        assert_eq!(second_result.output, ["two"]);
    }

    #[test]
    fn formats_manifest_requirements_for_pip_without_shell_interpolation() {
        assert_eq!(format_requirement("pygame", "2.6.*"), "pygame==2.6.*");
        assert_eq!(format_requirement("numpy", ">=2.0"), "numpy>=2.0");
    }

    #[test]
    fn empty_sync_replaces_stale_project_packages() {
        let project = TestDirectory::new("empty-sync");
        let packages = project.0.join(".sprout/python");
        std::fs::create_dir_all(&packages).expect("package directory can be created");
        std::fs::write(packages.join("stale.py"), "value = 1\n")
            .expect("stale package can be written");

        let result = sync_project_packages(
            Path::new("unused-python-executable"),
            &project.0,
            &BTreeMap::new(),
        )
        .expect("empty environment can be synchronized");
        assert_eq!(result.exit_code, Some(0));
        assert!(packages.is_dir());
        assert!(!packages.join("stale.py").exists());
    }
}
