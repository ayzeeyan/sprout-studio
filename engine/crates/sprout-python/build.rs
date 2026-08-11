use std::fs;
use std::path::{Path, PathBuf};

const PYTHON_RUNTIME_DIRECTORY: &str = "runtime/python-3.13.14-win-x64";

fn main() {
    println!(
        "cargo:rerun-if-changed=../../../{PYTHON_RUNTIME_DIRECTORY}/sprout-python-runtime.json"
    );
    if std::env::var_os("CARGO_CFG_WINDOWS").is_none() {
        return;
    }

    let manifest_directory = PathBuf::from(
        std::env::var_os("CARGO_MANIFEST_DIR").expect("Cargo provides CARGO_MANIFEST_DIR"),
    );
    let repository_root = manifest_directory
        .ancestors()
        .nth(3)
        .expect("sprout-python remains inside engine/crates")
        .to_path_buf();
    let runtime = repository_root.join(PYTHON_RUNTIME_DIRECTORY);
    if !runtime.join("sprout-python-runtime.json").is_file() {
        return;
    }

    let output_directory = PathBuf::from(
        std::env::var_os("OUT_DIR").expect("Cargo provides OUT_DIR for build scripts"),
    );
    let profile_directory = output_directory
        .ancestors()
        .nth(3)
        .expect("Cargo OUT_DIR has a profile ancestor");
    copy_runtime(&runtime, profile_directory);
    copy_runtime(&runtime, &profile_directory.join("deps"));
}

fn copy_runtime(source: &Path, destination: &Path) {
    fs::create_dir_all(destination).expect("Cargo profile directory can be created");
    for entry in fs::read_dir(source).expect("prepared Python runtime can be read") {
        let entry = entry.expect("Python runtime entry can be read");
        let file_type = entry.file_type().expect("Python runtime entry has a type");
        if !file_type.is_file() {
            continue;
        }
        let destination_file = destination.join(entry.file_name());
        let source_length = entry
            .metadata()
            .expect("Python runtime metadata can be read")
            .len();
        let unchanged = destination_file
            .metadata()
            .is_ok_and(|metadata| metadata.len() == source_length);
        if !unchanged {
            fs::copy(entry.path(), destination_file)
                .expect("prepared Python runtime can be copied beside Sprout");
        }
    }
}
