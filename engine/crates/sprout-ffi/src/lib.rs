//! Stable, coarse-grained C ABI for Sprout Studio and other native hosts.

use sprout_core::diagnostic::{Diagnostic, Severity};
use sprout_core::source::SourceFile;
use sprout_core::{SyntaxProfile, check_profile, execute_profile_with_python};
use sprout_project::{ProjectError, ProjectTemplate, SproutProject, create_project, load_project};
use sprout_python::EmbeddedPythonHost;
use std::fmt::Write;
use std::panic::{AssertUnwindSafe, catch_unwind};
use std::path::Path;
use std::slice;
use std::str;
use std::str::FromStr;

pub const SPROUT_API_VERSION: u32 = 1;

pub const SPROUT_OPERATION_CHECK: u32 = 0;
pub const SPROUT_OPERATION_RUN: u32 = 1;

pub const SPROUT_PROFILE_GUIDED: u32 = 0;
pub const SPROUT_PROFILE_STRUCTURED: u32 = 1;
pub const SPROUT_PROFILE_ADVANCED: u32 = 2;

/// An owned UTF-8 buffer allocated by Sprout. Release it with `sprout_buffer_free`.
#[repr(C)]
pub struct SproutBuffer {
    pub data: *mut u8,
    pub length: usize,
    pub capacity: usize,
}

impl SproutBuffer {
    fn from_string(value: String) -> Self {
        let mut bytes = value.into_bytes();
        let buffer = Self {
            data: bytes.as_mut_ptr(),
            length: bytes.len(),
            capacity: bytes.capacity(),
        };
        std::mem::forget(bytes);
        buffer
    }
}

#[unsafe(no_mangle)]
pub extern "C" fn sprout_api_version() -> u32 {
    SPROUT_API_VERSION
}

/// Executes a complete high-level engine operation.
///
/// # Safety
///
/// Each pointer must be valid for its corresponding byte length for the duration
/// of this call. Text must be UTF-8. The returned buffer must be freed exactly
/// once with `sprout_buffer_free`.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn sprout_execute(
    operation: u32,
    profile: u32,
    source_name_data: *const u8,
    source_name_length: usize,
    source_data: *const u8,
    source_length: usize,
) -> SproutBuffer {
    let response = match catch_unwind(AssertUnwindSafe(|| {
        // SAFETY: Requirements are documented as part of this exported ABI.
        let source_name =
            unsafe { read_utf8(source_name_data, source_name_length, "source name") }?;
        // SAFETY: Requirements are documented as part of this exported ABI.
        let source = unsafe { read_utf8(source_data, source_length, "source") }?;
        process(operation, profile, source_name, source)
    })) {
        Ok(Ok(response)) => response,
        Ok(Err(message)) => internal_error(&message),
        Err(_) => internal_error("The Sprout engine stopped unexpectedly."),
    };
    SproutBuffer::from_string(response)
}

/// Executes an engine operation with a validated Sprout project as its context.
/// Python imports resolve packages from that project's `.sprout/python` only.
///
/// # Safety
///
/// Each pointer must be valid for its corresponding byte length for the duration
/// of this call. Text must be UTF-8. The returned buffer must be freed exactly
/// once with `sprout_buffer_free`.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn sprout_execute_project(
    operation: u32,
    profile: u32,
    project_root_data: *const u8,
    project_root_length: usize,
    source_name_data: *const u8,
    source_name_length: usize,
    source_data: *const u8,
    source_length: usize,
) -> SproutBuffer {
    let response = match catch_unwind(AssertUnwindSafe(|| {
        // SAFETY: Requirements are documented as part of this exported ABI.
        let project_root =
            unsafe { read_utf8(project_root_data, project_root_length, "project root") }?;
        // SAFETY: Requirements are documented as part of this exported ABI.
        let source_name =
            unsafe { read_utf8(source_name_data, source_name_length, "source name") }?;
        // SAFETY: Requirements are documented as part of this exported ABI.
        let source = unsafe { read_utf8(source_data, source_length, "source") }?;
        process_with_project(
            operation,
            profile,
            source_name,
            source,
            Some(Path::new(project_root)),
        )
    })) {
        Ok(Ok(response)) => response,
        Ok(Err(message)) => internal_error(&message),
        Err(_) => internal_error("The Sprout engine stopped unexpectedly."),
    };
    SproutBuffer::from_string(response)
}

/// Loads and validates a Sprout project and returns structured project JSON.
///
/// # Safety
///
/// The pointer must be valid for its byte length for the duration of this call.
/// Text must be UTF-8. The returned buffer must be freed exactly once with
/// `sprout_buffer_free`.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn sprout_project_load(
    project_path_data: *const u8,
    project_path_length: usize,
) -> SproutBuffer {
    let response = match catch_unwind(AssertUnwindSafe(|| {
        // SAFETY: Requirements are documented as part of this exported ABI.
        let path = unsafe { read_utf8(project_path_data, project_path_length, "project path") }?;
        Ok::<String, String>(project_result_json(load_project(path)))
    })) {
        Ok(Ok(response)) => response,
        Ok(Err(message)) => project_internal_error(&message),
        Err(_) => project_internal_error("The Sprout project service stopped unexpectedly."),
    };
    SproutBuffer::from_string(response)
}

/// Creates a project from a built-in template and returns the loaded project.
///
/// # Safety
///
/// Each pointer must be valid for its byte length for the duration of this call.
/// Text must be UTF-8. The returned buffer must be freed exactly once with
/// `sprout_buffer_free`.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn sprout_project_create(
    destination_data: *const u8,
    destination_length: usize,
    name_data: *const u8,
    name_length: usize,
    template_data: *const u8,
    template_length: usize,
) -> SproutBuffer {
    let response = match catch_unwind(AssertUnwindSafe(|| {
        // SAFETY: Requirements are documented as part of this exported ABI.
        let destination =
            unsafe { read_utf8(destination_data, destination_length, "project destination") }?;
        // SAFETY: Requirements are documented as part of this exported ABI.
        let name = unsafe { read_utf8(name_data, name_length, "project name") }?;
        // SAFETY: Requirements are documented as part of this exported ABI.
        let template = unsafe { read_utf8(template_data, template_length, "project template") }?;
        let result = ProjectTemplate::from_str(template)
            .and_then(|template| create_project(destination, name, template));
        Ok::<String, String>(project_result_json(result))
    })) {
        Ok(Ok(response)) => response,
        Ok(Err(message)) => project_internal_error(&message),
        Err(_) => project_internal_error("The Sprout project service stopped unexpectedly."),
    };
    SproutBuffer::from_string(response)
}

/// Releases a buffer returned by Sprout.
///
/// # Safety
///
/// `buffer` must be an unchanged value returned by `sprout_execute`, and it must
/// not have been released previously.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn sprout_buffer_free(buffer: SproutBuffer) {
    if buffer.data.is_null() {
        return;
    }
    // SAFETY: The caller contract requires a buffer created by `from_string`.
    unsafe {
        drop(Vec::from_raw_parts(
            buffer.data,
            buffer.length,
            buffer.capacity,
        ));
    }
}

unsafe fn read_utf8<'a>(
    data: *const u8,
    length: usize,
    description: &str,
) -> Result<&'a str, String> {
    if data.is_null() && length != 0 {
        return Err(format!("The {description} pointer was null."));
    }
    let bytes = if length == 0 {
        &[]
    } else {
        // SAFETY: The exported function's caller guarantees this memory range.
        unsafe { slice::from_raw_parts(data, length) }
    };
    str::from_utf8(bytes).map_err(|_| format!("The {description} was not valid UTF-8."))
}

fn process(
    operation: u32,
    profile: u32,
    source_name: &str,
    source: &str,
) -> Result<String, String> {
    process_with_project(operation, profile, source_name, source, None)
}

fn process_with_project(
    operation: u32,
    profile: u32,
    source_name: &str,
    source: &str,
    project_root: Option<&Path>,
) -> Result<String, String> {
    let profile = match profile {
        SPROUT_PROFILE_GUIDED => SyntaxProfile::Guided,
        SPROUT_PROFILE_STRUCTURED => SyntaxProfile::Structured,
        SPROUT_PROFILE_ADVANCED => SyntaxProfile::Advanced,
        _ => return Err(format!("Unknown syntax profile value `{profile}`.")),
    };
    let source = SourceFile::new(source_name, source);
    match operation {
        SPROUT_OPERATION_CHECK => {
            let result = check_profile(&source, profile);
            Ok(response_json(Vec::new(), &result.diagnostics))
        }
        SPROUT_OPERATION_RUN => {
            let packages = project_root.map(|root| root.join(".sprout/python"));
            let mut python =
                EmbeddedPythonHost::new(packages).map_err(|error| error.to_string())?;
            let result = execute_profile_with_python(&source, profile, &mut python);
            Ok(response_json(result.output, &result.diagnostics))
        }
        _ => Err(format!("Unknown engine operation value `{operation}`.")),
    }
}

fn response_json(output: Vec<String>, diagnostics: &[Diagnostic]) -> String {
    let mut json = String::from("{\"apiVersion\":1,\"success\":");
    json.push_str(if diagnostics.is_empty() {
        "true"
    } else {
        "false"
    });
    json.push_str(",\"output\":[");
    for (index, line) in output.iter().enumerate() {
        if index > 0 {
            json.push(',');
        }
        push_json_string(&mut json, line);
    }
    json.push_str("],\"diagnostics\":[");
    for (index, diagnostic) in diagnostics.iter().enumerate() {
        if index > 0 {
            json.push(',');
        }
        json.push_str("{\"code\":");
        push_json_string(&mut json, diagnostic.code);
        json.push_str(",\"severity\":");
        push_json_string(
            &mut json,
            match diagnostic.severity {
                Severity::Error => "error",
                Severity::Warning => "warning",
            },
        );
        json.push_str(",\"title\":");
        push_json_string(&mut json, &diagnostic.title);
        json.push_str(",\"message\":");
        push_json_string(&mut json, &diagnostic.message);
        json.push_str(",\"span\":");
        push_span_json(&mut json, &diagnostic.span);
        json.push_str(",\"relatedInformation\":[");
        for (related_index, related) in diagnostic.related_information.iter().enumerate() {
            if related_index > 0 {
                json.push(',');
            }
            json.push_str("{\"message\":");
            push_json_string(&mut json, &related.message);
            json.push_str(",\"span\":");
            push_span_json(&mut json, &related.span);
            json.push('}');
        }
        json.push_str("],\"suggestions\":[");
        for (suggestion_index, suggestion) in diagnostic.suggestions.iter().enumerate() {
            if suggestion_index > 0 {
                json.push(',');
            }
            push_json_string(&mut json, suggestion);
        }
        json.push_str("],\"optionalFixes\":[");
        for (fix_index, fix) in diagnostic.optional_fixes.iter().enumerate() {
            if fix_index > 0 {
                json.push(',');
            }
            json.push_str("{\"title\":");
            push_json_string(&mut json, &fix.title);
            json.push_str(",\"replacement\":");
            push_json_string(&mut json, &fix.replacement);
            json.push_str(",\"span\":");
            push_span_json(&mut json, &fix.span);
            json.push('}');
        }
        json.push_str("]}");
    }
    json.push_str("]}");
    json
}

fn project_result_json(result: Result<SproutProject, ProjectError>) -> String {
    match result {
        Ok(project) => project_success_json(&project),
        Err(error) => project_error_json(&error),
    }
}

fn project_success_json(project: &SproutProject) -> String {
    let mut json = String::from("{\"apiVersion\":1,\"success\":true,\"project\":{\"rootPath\":");
    push_json_string(&mut json, &portable_path(&project.root));
    json.push_str(",\"manifestPath\":");
    push_json_string(&mut json, &portable_path(&project.manifest_path));
    json.push_str(",\"name\":");
    push_json_string(&mut json, &project.manifest.project.name);
    json.push_str(",\"version\":");
    push_json_string(&mut json, &project.manifest.project.version);
    json.push_str(",\"entryPath\":");
    push_json_string(&mut json, &portable_path(&project.entry_path));
    json.push_str(",\"profile\":");
    push_json_string(
        &mut json,
        match project.profile {
            SyntaxProfile::Guided => "guided",
            SyntaxProfile::Structured => "structured",
            SyntaxProfile::Advanced => "advanced",
        },
    );
    json.push_str(",\"sourceFiles\":[");
    for (index, path) in project.source_files.iter().enumerate() {
        if index > 0 {
            json.push(',');
        }
        push_json_string(&mut json, &portable_path(path));
    }
    json.push_str("]},\"diagnostics\":[]}");
    json
}

fn project_error_json(error: &ProjectError) -> String {
    let mut json =
        String::from("{\"apiVersion\":1,\"success\":false,\"project\":null,\"diagnostics\":[");
    for (index, issue) in error.issues.iter().enumerate() {
        if index > 0 {
            json.push(',');
        }
        json.push_str("{\"code\":");
        push_json_string(&mut json, &issue.code);
        json.push_str(",\"title\":\"Invalid Sprout project\",\"message\":");
        push_json_string(&mut json, &issue.message);
        json.push_str(",\"path\":");
        match &issue.path {
            Some(path) => push_json_string(&mut json, &portable_path(path)),
            None => json.push_str("null"),
        }
        json.push('}');
    }
    json.push_str("]}");
    json
}

fn project_internal_error(message: &str) -> String {
    let mut json = String::from(
        "{\"apiVersion\":1,\"success\":false,\"project\":null,\"diagnostics\":[{\"code\":\"SPP900\",\"title\":\"Project service failure\",\"message\":",
    );
    push_json_string(&mut json, message);
    json.push_str(",\"path\":null}]}");
    json
}

fn portable_path(path: &Path) -> String {
    let value = path.to_string_lossy();
    if let Some(path) = value.strip_prefix(r"\\?\UNC\") {
        format!(r"\\{path}")
    } else if let Some(path) = value.strip_prefix(r"\\?\") {
        path.to_owned()
    } else {
        value.into_owned()
    }
}

fn internal_error(message: &str) -> String {
    let mut json = String::from(
        "{\"apiVersion\":1,\"success\":false,\"output\":[],\"diagnostics\":[{\"code\":\"SPT900\",\"severity\":\"error\",\"title\":\"Engine failure\",\"message\":",
    );
    push_json_string(&mut json, message);
    json.push_str(",\"span\":{\"file\":\"<engine>\",\"start\":0,\"end\":0,\"startLine\":1,\"startColumn\":1,\"endLine\":1,\"endColumn\":1},\"relatedInformation\":[],\"suggestions\":[],\"optionalFixes\":[]}]}");
    json
}

fn push_span_json(output: &mut String, span: &sprout_core::source::SourceSpan) {
    output.push_str("{\"file\":");
    push_json_string(output, &span.file);
    let _ = write!(
        output,
        ",\"start\":{},\"end\":{},\"startLine\":{},\"startColumn\":{},\"endLine\":{},\"endColumn\":{}}}",
        span.start, span.end, span.start_line, span.start_column, span.end_line, span.end_column
    );
}

fn push_json_string(output: &mut String, value: &str) {
    output.push('"');
    for character in value.chars() {
        match character {
            '"' => output.push_str("\\\""),
            '\\' => output.push_str("\\\\"),
            '\n' => output.push_str("\\n"),
            '\r' => output.push_str("\\r"),
            '\t' => output.push_str("\\t"),
            character if character <= '\u{1f}' => {
                let _ = write!(output, "\\u{:04x}", character as u32);
            }
            character => output.push(character),
        }
    }
    output.push('"');
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn run_response_contains_output_and_version() {
        let response = process(
            SPROUT_OPERATION_RUN,
            SPROUT_PROFILE_GUIDED,
            "test.spr",
            "print(\"hello\")\n",
        )
        .unwrap();
        assert!(response.contains("\"apiVersion\":1"));
        assert!(response.contains("\"success\":true"));
        assert!(response.contains("\"output\":[\"hello\"]"));
    }

    #[test]
    fn run_response_executes_embedded_python() {
        let response = process(
            SPROUT_OPERATION_RUN,
            SPROUT_PROFILE_GUIDED,
            "python.spr",
            "import python \"math\" as math\nprint(math.factorial(6))\n",
        )
        .unwrap();
        assert!(response.contains("\"success\":true"), "{response}");
        assert!(response.contains("\"output\":[\"720\"]"), "{response}");
    }

    #[test]
    fn project_execution_uses_its_local_python_packages() {
        let destination = std::env::temp_dir().join(format!(
            "sprout-ffi-python-{}-{}",
            std::process::id(),
            std::time::SystemTime::now()
                .duration_since(std::time::UNIX_EPOCH)
                .unwrap()
                .as_nanos()
        ));
        let packages = destination.join(".sprout/python");
        std::fs::create_dir_all(&packages).unwrap();
        std::fs::write(
            packages.join("ffi_project_value.py"),
            "def read():\n    return 'ffi-project'\n",
        )
        .unwrap();
        let response = process_with_project(
            SPROUT_OPERATION_RUN,
            SPROUT_PROFILE_GUIDED,
            "python.spr",
            "import python \"ffi_project_value\" as value\nprint(value.read())\n",
            Some(&destination),
        )
        .unwrap();
        assert!(
            response.contains("\"output\":[\"ffi-project\"]"),
            "{response}"
        );
        std::fs::remove_dir_all(destination).unwrap();
    }

    #[test]
    fn check_response_preserves_structured_diagnostic_span() {
        let response = process(
            SPROUT_OPERATION_CHECK,
            SPROUT_PROFILE_GUIDED,
            "broken.spr",
            "print(missing)\n",
        )
        .unwrap();
        assert!(response.contains("\"code\":\"SPT201\""));
        assert!(response.contains("\"startLine\":1,\"startColumn\":7"));
    }

    #[test]
    fn response_preserves_suggestions_and_machine_applicable_fixes() {
        let response = process(
            SPROUT_OPERATION_CHECK,
            SPROUT_PROFILE_GUIDED,
            "broken.spr",
            "has attack = 1\nprint(attck)\n",
        )
        .unwrap();
        assert!(response.contains("\"suggestions\":[\"Did you mean `attack`?\"]"));
        assert!(response.contains("\"replacement\":\"attack\""));
        assert!(response.contains("\"optionalFixes\":[{"));
    }

    #[test]
    fn exported_buffer_round_trips_and_can_be_freed() {
        let name = b"test.spr";
        let source = b"print(3)\n";
        // SAFETY: Both byte slices remain alive for the call and the returned
        // buffer is released exactly once below.
        let buffer = unsafe {
            sprout_execute(
                SPROUT_OPERATION_RUN,
                SPROUT_PROFILE_GUIDED,
                name.as_ptr(),
                name.len(),
                source.as_ptr(),
                source.len(),
            )
        };
        // SAFETY: `buffer` is owned and valid until the following free call.
        let response = unsafe { slice::from_raw_parts(buffer.data, buffer.length) };
        assert!(
            str::from_utf8(response)
                .unwrap()
                .contains("\"output\":[\"3\"]")
        );
        // SAFETY: This is the unchanged buffer and its only release.
        unsafe { sprout_buffer_free(buffer) };
    }

    #[test]
    fn project_api_creates_and_loads_a_template() {
        let destination = std::env::temp_dir().join(format!(
            "sprout-ffi-project-{}-{}",
            std::process::id(),
            std::time::SystemTime::now()
                .duration_since(std::time::UNIX_EPOCH)
                .unwrap()
                .as_nanos()
        ));
        let destination_text = destination.to_string_lossy();
        let name = b"FfiProject";
        let template = b"advanced";
        // SAFETY: All byte ranges remain valid through the call.
        let created = unsafe {
            sprout_project_create(
                destination_text.as_ptr(),
                destination_text.len(),
                name.as_ptr(),
                name.len(),
                template.as_ptr(),
                template.len(),
            )
        };
        // SAFETY: The returned buffer remains owned until it is freed below.
        let created_text = unsafe { slice::from_raw_parts(created.data, created.length) };
        let created_text = str::from_utf8(created_text).unwrap();
        assert!(created_text.contains("\"success\":true"), "{created_text}");
        assert!(created_text.contains("\"profile\":\"advanced\""));
        // SAFETY: This is the only release of the unchanged buffer.
        unsafe { sprout_buffer_free(created) };

        // SAFETY: The destination text remains alive through the call.
        let loaded =
            unsafe { sprout_project_load(destination_text.as_ptr(), destination_text.len()) };
        // SAFETY: The returned buffer remains owned until it is freed below.
        let loaded_text = unsafe { slice::from_raw_parts(loaded.data, loaded.length) };
        let loaded_text = str::from_utf8(loaded_text).unwrap();
        assert!(loaded_text.contains("\"name\":\"FfiProject\""));
        // SAFETY: This is the only release of the unchanged buffer.
        unsafe { sprout_buffer_free(loaded) };
        std::fs::remove_dir_all(destination).unwrap();
    }
}
