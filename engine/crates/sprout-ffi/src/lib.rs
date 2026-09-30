//! Stable, coarse-grained C ABI for Sprout Studio and other native hosts.

use sprout_core::diagnostic::{Diagnostic, Severity};
use sprout_core::formatter::format_profile;
use sprout_core::ide::{DocumentSymbol, inspect};
use sprout_core::runtime::{CanvasCommand, DebugSnapshot};
use sprout_core::source::SourceFile;
use sprout_core::{
    SyntaxProfile, check_profile, debug_profile_with_python, execute_profile_with_python,
};
use sprout_project::{
    DependencyKind, ProjectError, ProjectTemplate, SproutProject, build_project_bundle,
    create_project, load_project, update_dependency,
};
use sprout_python::EmbeddedPythonHost;
use std::fmt::Write;
use std::panic::{AssertUnwindSafe, catch_unwind};
use std::path::Path;
use std::slice;
use std::str;
use std::str::FromStr;

pub const SPROUT_API_VERSION: u32 = 1;
pub const SPROUT_ENGINE_VERSION: &str = include_str!("../../../../VERSION");

pub const SPROUT_OPERATION_CHECK: u32 = 0;
pub const SPROUT_OPERATION_RUN: u32 = 1;
pub const SPROUT_OPERATION_DEBUG: u32 = 2;

pub const SPROUT_LANGUAGE_INSPECT: u32 = 0;
pub const SPROUT_LANGUAGE_FORMAT: u32 = 1;

pub const SPROUT_DEPENDENCY_SPROUT: u32 = 0;
pub const SPROUT_DEPENDENCY_PYTHON: u32 = 1;
pub const SPROUT_DEPENDENCY_SET: u32 = 0;
pub const SPROUT_DEPENDENCY_REMOVE: u32 = 1;

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

/// Returns structured build and ABI metadata for compatibility validation.
/// Release the returned UTF-8 JSON buffer with `sprout_buffer_free`.
#[unsafe(no_mangle)]
pub extern "C" fn sprout_engine_info() -> SproutBuffer {
    let configuration = if cfg!(debug_assertions) {
        "debug"
    } else {
        "release"
    };
    let mut json = String::from("{\"engineVersion\":");
    push_json_string(&mut json, SPROUT_ENGINE_VERSION.trim());
    let _ = write!(
        json,
        ",\"abiVersion\":{SPROUT_API_VERSION},\"architecture\":"
    );
    push_json_string(&mut json, std::env::consts::ARCH);
    json.push_str(",\"buildConfiguration\":");
    push_json_string(&mut json, configuration);
    json.push('}');
    SproutBuffer::from_string(json)
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

/// Adds, updates, or removes one validated project dependency.
///
/// # Safety
///
/// Each pointer must be valid for its corresponding byte length for the duration
/// of this call. Text must be UTF-8. The returned buffer must be freed exactly
/// once with `sprout_buffer_free`.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn sprout_project_dependency(
    kind: u32,
    operation: u32,
    project_path_data: *const u8,
    project_path_length: usize,
    name_data: *const u8,
    name_length: usize,
    requirement_data: *const u8,
    requirement_length: usize,
) -> SproutBuffer {
    let response = match catch_unwind(AssertUnwindSafe(|| {
        // SAFETY: Requirements are documented as part of this exported ABI.
        let project_path =
            unsafe { read_utf8(project_path_data, project_path_length, "project path") }?;
        // SAFETY: Requirements are documented as part of this exported ABI.
        let name = unsafe { read_utf8(name_data, name_length, "dependency name") }?;
        // SAFETY: Requirements are documented as part of this exported ABI.
        let requirement = unsafe {
            read_utf8(
                requirement_data,
                requirement_length,
                "dependency requirement",
            )
        }?;
        let kind = match kind {
            SPROUT_DEPENDENCY_SPROUT => DependencyKind::Sprout,
            SPROUT_DEPENDENCY_PYTHON => DependencyKind::Python,
            _ => return Err(format!("Unknown dependency kind value `{kind}`.")),
        };
        let requirement = match operation {
            SPROUT_DEPENDENCY_SET => Some(requirement),
            SPROUT_DEPENDENCY_REMOVE => None,
            _ => return Err(format!("Unknown dependency operation value `{operation}`.")),
        };
        Ok::<String, String>(project_result_json(update_dependency(
            project_path,
            kind,
            name,
            requirement,
        )))
    })) {
        Ok(Ok(response)) => response,
        Ok(Err(message)) => project_internal_error(&message),
        Err(_) => project_internal_error("The Sprout package service stopped unexpectedly."),
    };
    SproutBuffer::from_string(response)
}

/// Builds a validated, portable interpreted Sprout bundle.
///
/// # Safety
///
/// The pointer must be valid for its byte length for the duration of this call.
/// Text must be UTF-8. The returned buffer must be freed exactly once with
/// `sprout_buffer_free`.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn sprout_project_build(
    project_path_data: *const u8,
    project_path_length: usize,
) -> SproutBuffer {
    let response = match catch_unwind(AssertUnwindSafe(|| {
        // SAFETY: Requirements are documented as part of this exported ABI.
        let project_path =
            unsafe { read_utf8(project_path_data, project_path_length, "project path") }?;
        Ok::<String, String>(match build_project_bundle(project_path) {
            Ok(artifact) => {
                let mut json =
                    String::from("{\"apiVersion\":1,\"success\":true,\"artifact\":{\"path\":");
                push_json_string(&mut json, &portable_path(&artifact.path));
                json.push_str(",\"target\":");
                push_json_string(&mut json, &artifact.target);
                let _ = write!(
                    json,
                    ",\"sourceCount\":{}}},\"diagnostics\":[]}}",
                    artifact.source_count
                );
                json
            }
            Err(error) => build_error_json(&error),
        })
    })) {
        Ok(Ok(response)) => response,
        Ok(Err(message)) => build_internal_error(&message),
        Err(_) => build_internal_error("The Sprout build service stopped unexpectedly."),
    };
    SproutBuffer::from_string(response)
}

/// Runs a cursor-sensitive language-service operation without executing source.
///
/// # Safety
///
/// Each pointer must be valid for its corresponding byte length for the duration
/// of this call. Text must be UTF-8 and `cursor` must be a UTF-8 byte offset.
/// The returned buffer must be freed exactly once with `sprout_buffer_free`.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn sprout_language_service(
    operation: u32,
    profile: u32,
    cursor: usize,
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
        language_service_process(operation, profile, cursor, source_name, source)
    })) {
        Ok(Ok(response)) => response,
        Ok(Err(message)) => language_internal_error(&message),
        Err(_) => language_internal_error("The Sprout language service stopped unexpectedly."),
    };
    SproutBuffer::from_string(response)
}

/// Returns a serialized JSON string of the parsed AST from sprout-core.
///
/// # Safety
///
/// `source` must be a null-terminated UTF-8 C string pointer or null.
/// The returned buffer must be freed with `sprout_buffer_free`.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn sprout_get_ast_json(source: *const std::os::raw::c_char) -> SproutBuffer {
    let response = match catch_unwind(AssertUnwindSafe(|| {
        if source.is_null() {
            return Ok::<String, String>(String::from("{\"program\":null,\"diagnostics\":[]}"));
        }
        let c_str = unsafe { std::ffi::CStr::from_ptr(source) };
        let text = c_str.to_str().map_err(|e| format!("Invalid UTF-8: {e}"))?;
        let source_file = sprout_core::source::SourceFile::new("input.spr", text);
        let parsed = sprout_core::parser::parse_profile(&source_file, sprout_core::parser::SyntaxProfile::Guided);
        Ok::<String, String>(ast_to_json(&parsed.program, &parsed.diagnostics))
    })) {
        Ok(Ok(response)) => response,
        Ok(Err(message)) => {
            let mut s = String::from("{\"program\":null,\"diagnostics\":[{\"code\":\"SPT500\",\"severity\":\"Error\",\"title\":\"AST Error\",\"message\":");
            push_json_string(&mut s, &message);
            s.push_str("}]}");
            s
        }
        Err(_) => String::from("{\"program\":null,\"diagnostics\":[{\"code\":\"SPT500\",\"severity\":\"Error\",\"title\":\"AST Error\",\"message\":\"AST generation panicked.\"}]}"),
    };
    SproutBuffer::from_string(response)
}

/// Returns the serialized JSON string of all standard library documentation metadata.
/// The returned buffer must be freed with `sprout_buffer_free`.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn sprout_get_stdlib_docs_json() -> SproutBuffer {
    let json = sprout_core::stdlib::get_stdlib_docs_json();
    SproutBuffer::from_string(json)
}

/// Releases a buffer returned by Sprout.
///
/// # Safety
///
/// `buffer` must be an unchanged value returned by a Sprout FFI function, and it
/// must not have been released previously.
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
    let profile = profile_value(profile)?;
    let source = SourceFile::new(source_name, source);
    match operation {
        SPROUT_OPERATION_CHECK => {
            let result = check_profile(&source, profile);
            Ok(response_json(Vec::new(), &result.diagnostics, &[], &[]))
        }
        SPROUT_OPERATION_RUN => {
            let packages = project_root.map(|root| root.join(".sprout/python"));
            let mut python =
                EmbeddedPythonHost::new(packages).map_err(|error| error.to_string())?;
            let result = execute_profile_with_python(&source, profile, &mut python);
            Ok(response_json(
                result.output,
                &result.diagnostics,
                &result.canvas_commands,
                &[],
            ))
        }
        SPROUT_OPERATION_DEBUG => {
            let packages = project_root.map(|root| root.join(".sprout/python"));
            let mut python =
                EmbeddedPythonHost::new(packages).map_err(|error| error.to_string())?;
            let result = debug_profile_with_python(&source, profile, &mut python);
            Ok(response_json(
                result.output,
                &result.diagnostics,
                &result.canvas_commands,
                &result.snapshots,
            ))
        }
        _ => Err(format!("Unknown engine operation value `{operation}`.")),
    }
}

fn profile_value(profile: u32) -> Result<SyntaxProfile, String> {
    match profile {
        SPROUT_PROFILE_GUIDED => Ok(SyntaxProfile::Guided),
        SPROUT_PROFILE_STRUCTURED => Ok(SyntaxProfile::Structured),
        SPROUT_PROFILE_ADVANCED => Ok(SyntaxProfile::Advanced),
        _ => Err(format!("Unknown syntax profile value `{profile}`.")),
    }
}

fn response_json(
    output: Vec<String>,
    diagnostics: &[Diagnostic],
    canvas_commands: &[CanvasCommand],
    debug_snapshots: &[DebugSnapshot],
) -> String {
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
    json.push_str("],\"diagnostics\":");
    push_diagnostics_json(&mut json, diagnostics);
    json.push_str(",\"canvasCommands\":");
    push_canvas_json(&mut json, canvas_commands);
    json.push_str(",\"debugSnapshots\":[");
    for (index, snapshot) in debug_snapshots.iter().enumerate() {
        if index > 0 {
            json.push(',');
        }
        json.push_str("{\"span\":");
        push_span_json(&mut json, &snapshot.span);
        json.push_str(",\"variables\":[");
        for (variable_index, variable) in snapshot.variables.iter().enumerate() {
            if variable_index > 0 {
                json.push(',');
            }
            json.push_str("{\"name\":");
            push_json_string(&mut json, &variable.name);
            json.push_str(",\"typeName\":");
            push_json_string(&mut json, &variable.type_name);
            json.push_str(",\"value\":");
            push_json_string(&mut json, &variable.value);
            json.push('}');
        }
        json.push_str("],\"callStack\":[");
        for (frame_index, frame) in snapshot.call_stack.iter().enumerate() {
            if frame_index > 0 {
                json.push(',');
            }
            json.push_str("{\"name\":");
            push_json_string(&mut json, &frame.name);
            json.push_str(",\"span\":");
            push_span_json(&mut json, &frame.span);
            json.push('}');
        }
        json.push_str("],\"output\":[");
        for (line_index, line) in snapshot.output.iter().enumerate() {
            if line_index > 0 {
                json.push(',');
            }
            push_json_string(&mut json, line);
        }
        json.push_str("],\"canvasCommands\":");
        push_canvas_json(&mut json, &snapshot.canvas_commands);
        json.push('}');
    }
    json.push_str("]}");
    json
}

fn push_diagnostics_json(json: &mut String, diagnostics: &[Diagnostic]) {
    json.push('[');
    for (index, diagnostic) in diagnostics.iter().enumerate() {
        if index > 0 {
            json.push(',');
        }
        json.push_str("{\"code\":");
        push_json_string(json, diagnostic.code);
        json.push_str(",\"severity\":");
        push_json_string(
            json,
            match diagnostic.severity {
                Severity::Error => "error",
                Severity::Warning => "warning",
            },
        );
        json.push_str(",\"title\":");
        push_json_string(json, &diagnostic.title);
        json.push_str(",\"message\":");
        push_json_string(json, &diagnostic.message);
        json.push_str(",\"span\":");
        push_span_json(json, &diagnostic.span);
        json.push_str(",\"relatedInformation\":[");
        for (related_index, related) in diagnostic.related_information.iter().enumerate() {
            if related_index > 0 {
                json.push(',');
            }
            json.push_str("{\"message\":");
            push_json_string(json, &related.message);
            json.push_str(",\"span\":");
            push_span_json(json, &related.span);
            json.push('}');
        }
        json.push_str("],\"suggestions\":[");
        for (suggestion_index, suggestion) in diagnostic.suggestions.iter().enumerate() {
            if suggestion_index > 0 {
                json.push(',');
            }
            push_json_string(json, suggestion);
        }
        json.push_str("],\"optionalFixes\":[");
        for (fix_index, fix) in diagnostic.optional_fixes.iter().enumerate() {
            if fix_index > 0 {
                json.push(',');
            }
            json.push_str("{\"title\":");
            push_json_string(json, &fix.title);
            json.push_str(",\"replacement\":");
            push_json_string(json, &fix.replacement);
            json.push_str(",\"span\":");
            push_span_json(json, &fix.span);
            json.push('}');
        }
        json.push_str("]}");
    }
    json.push(']');
}

fn push_canvas_json(json: &mut String, commands: &[CanvasCommand]) {
    json.push('[');
    for (index, command) in commands.iter().enumerate() {
        if index > 0 {
            json.push(',');
        }
        match command {
            CanvasCommand::Clear { color } => {
                json.push_str("{\"kind\":\"clear\",\"color\":");
                push_json_string(json, color);
                json.push('}');
            }
            CanvasCommand::Rectangle {
                x,
                y,
                width,
                height,
                color,
            } => {
                let _ = write!(
                    json,
                    "{{\"kind\":\"rectangle\",\"x\":{x},\"y\":{y},\"width\":{width},\"height\":{height},\"color\":"
                );
                push_json_string(json, color);
                json.push('}');
            }
            CanvasCommand::Circle {
                x,
                y,
                radius,
                color,
            } => {
                let _ = write!(
                    json,
                    "{{\"kind\":\"circle\",\"x\":{x},\"y\":{y},\"radius\":{radius},\"color\":"
                );
                push_json_string(json, color);
                json.push('}');
            }
        }
    }
    json.push(']');
}

fn language_service_process(
    operation: u32,
    profile: u32,
    cursor: usize,
    source_name: &str,
    source: &str,
) -> Result<String, String> {
    let profile = profile_value(profile)?;
    let source = SourceFile::new(source_name, source);
    match operation {
        SPROUT_LANGUAGE_INSPECT => Ok(inspect_response_json(inspect(&source, profile, cursor))),
        SPROUT_LANGUAGE_FORMAT => {
            let result = format_profile(&source, profile);
            let mut json = String::from("{\"apiVersion\":1,\"success\":");
            json.push_str(if result.diagnostics.is_empty() {
                "true"
            } else {
                "false"
            });
            json.push_str(",\"diagnostics\":");
            push_diagnostics_json(&mut json, &result.diagnostics);
            json.push_str(",\"completions\":[],\"hover\":null,\"definition\":null,\"symbols\":[],\"formattedSource\":");
            match result.formatted_source {
                Some(formatted) => push_json_string(&mut json, &formatted),
                None => json.push_str("null"),
            }
            json.push('}');
            Ok(json)
        }
        _ => Err(format!(
            "Unknown language-service operation value `{operation}`."
        )),
    }
}

fn inspect_response_json(result: sprout_core::ide::InspectResult) -> String {
    let mut json = String::from("{\"apiVersion\":1,\"success\":");
    json.push_str(if result.diagnostics.is_empty() {
        "true"
    } else {
        "false"
    });
    json.push_str(",\"diagnostics\":");
    push_diagnostics_json(&mut json, &result.diagnostics);
    json.push_str(",\"completions\":[");
    for (index, completion) in result.completions.iter().enumerate() {
        if index > 0 {
            json.push(',');
        }
        json.push_str("{\"label\":");
        push_json_string(&mut json, &completion.label);
        json.push_str(",\"kind\":");
        push_json_string(&mut json, &completion.kind);
        json.push_str(",\"detail\":");
        push_json_string(&mut json, &completion.detail);
        json.push_str(",\"insertText\":");
        push_json_string(&mut json, &completion.insert_text);
        json.push_str(",\"replacementSpan\":");
        push_span_json(&mut json, &completion.replacement_span);
        json.push('}');
    }
    json.push_str("],\"hover\":");
    match result.hover {
        Some(hover) => {
            json.push_str("{\"title\":");
            push_json_string(&mut json, &hover.title);
            json.push_str(",\"contents\":");
            push_json_string(&mut json, &hover.contents);
            json.push_str(",\"span\":");
            push_span_json(&mut json, &hover.span);
            json.push('}');
        }
        None => json.push_str("null"),
    }
    json.push_str(",\"definition\":");
    match result.definition {
        Some(definition) => {
            json.push_str("{\"name\":");
            push_json_string(&mut json, &definition.name);
            json.push_str(",\"kind\":");
            push_json_string(&mut json, &definition.kind);
            json.push_str(",\"span\":");
            push_span_json(&mut json, &definition.span);
            json.push('}');
        }
        None => json.push_str("null"),
    }
    json.push_str(",\"symbols\":[");
    for (index, symbol) in result.symbols.iter().enumerate() {
        if index > 0 {
            json.push(',');
        }
        push_document_symbol_json(&mut json, symbol);
    }
    json.push_str("],\"formattedSource\":null}");
    json
}

fn push_document_symbol_json(json: &mut String, symbol: &DocumentSymbol) {
    json.push_str("{\"name\":");
    push_json_string(json, &symbol.name);
    json.push_str(",\"kind\":");
    push_json_string(json, &symbol.kind);
    json.push_str(",\"detail\":");
    push_json_string(json, &symbol.detail);
    json.push_str(",\"span\":");
    push_span_json(json, &symbol.span);
    json.push_str(",\"children\":[");
    for (index, child) in symbol.children.iter().enumerate() {
        if index > 0 {
            json.push(',');
        }
        push_document_symbol_json(json, child);
    }
    json.push_str("]}");
}

fn language_internal_error(message: &str) -> String {
    let diagnostic = Diagnostic::error(
        "SPT900",
        "Language service failure",
        message,
        sprout_core::source::SourceSpan::new("<engine>", 0, 0, 1, 1, 1, 1),
    );
    let mut json = String::from("{\"apiVersion\":1,\"success\":false,\"diagnostics\":");
    push_diagnostics_json(&mut json, &[diagnostic]);
    json.push_str(",\"completions\":[],\"hover\":null,\"definition\":null,\"symbols\":[],\"formattedSource\":null}");
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
    json.push_str("],\"dependencies\":");
    push_string_map_json(&mut json, &project.manifest.dependencies);
    json.push_str(",\"pythonDependencies\":");
    push_string_map_json(&mut json, &project.manifest.python);
    json.push_str(",\"buildTarget\":");
    push_json_string(&mut json, &project.manifest.build.target);
    json.push_str("},\"diagnostics\":[]}");
    json
}

fn push_string_map_json(json: &mut String, values: &std::collections::BTreeMap<String, String>) {
    json.push('{');
    for (index, (name, requirement)) in values.iter().enumerate() {
        if index > 0 {
            json.push(',');
        }
        push_json_string(json, name);
        json.push(':');
        push_json_string(json, requirement);
    }
    json.push('}');
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

fn build_error_json(error: &ProjectError) -> String {
    project_error_json(error).replacen("\"project\":null", "\"artifact\":null", 1)
}

fn build_internal_error(message: &str) -> String {
    project_internal_error(message).replacen("\"project\":null", "\"artifact\":null", 1)
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
    json.push_str(",\"span\":{\"file\":\"<engine>\",\"start\":0,\"end\":0,\"startLine\":1,\"startColumn\":1,\"endLine\":1,\"endColumn\":1},\"relatedInformation\":[],\"suggestions\":[],\"optionalFixes\":[]}],\"canvasCommands\":[],\"debugSnapshots\":[]}");
    json
}

fn ast_to_json(
    program: &Option<sprout_core::ast::Program>,
    diagnostics: &[sprout_core::diagnostic::Diagnostic],
) -> String {
    let mut json = String::from("{\"program\":");
    match program {
        Some(program) => {
            let span = program
                .statements
                .first()
                .map(|s| {
                    let last = program.statements.last().unwrap();
                    s.span.merge(&last.span)
                })
                .unwrap_or_else(|| {
                    sprout_core::source::SourceSpan::new("<ast>", 0, 0, 1, 1, 1, 1)
                });
            push_ast_node(
                &mut json,
                "Program",
                "Program",
                &format!("{} statements", program.statements.len()),
                &span,
                |j| {
                    for (index, statement) in program.statements.iter().enumerate() {
                        if index > 0 {
                            j.push(',');
                        }
                        push_statement_ast_json(j, statement);
                    }
                },
            );
        }
        None => json.push_str("null"),
    }
    json.push_str(",\"diagnostics\":");
    push_diagnostics_json(&mut json, diagnostics);
    json.push('}');
    json
}

fn push_ast_node(
    json: &mut String,
    kind: &str,
    label: &str,
    detail: &str,
    span: &sprout_core::source::SourceSpan,
    children_fn: impl FnOnce(&mut String),
) {
    json.push_str("{\"kind\":");
    push_json_string(json, kind);
    json.push_str(",\"label\":");
    push_json_string(json, label);
    json.push_str(",\"detail\":");
    push_json_string(json, detail);
    json.push_str(",\"span\":");
    push_span_json(json, span);
    json.push_str(",\"children\":[");
    children_fn(json);
    json.push_str("]}");
}

fn push_statement_ast_json(json: &mut String, statement: &sprout_core::ast::Statement) {
    use sprout_core::ast::StatementKind;
    match &statement.kind {
        StatementKind::Import { module, alias } => {
            let label = format!("import {module} as {alias}");
            let detail = format!("module: {module}, alias: {alias}");
            push_ast_node(json, "Import", &label, &detail, &statement.span, |_| {});
        }
        StatementKind::ImportPython { module, alias } => {
            let label = format!("import python \"{module}\" as {alias}");
            let detail = format!("Python module: {module}");
            push_ast_node(json, "ImportPython", &label, &detail, &statement.span, |_| {});
        }
        StatementKind::Variable { name, initializer } => {
            let label = format!("has {name} = ...");
            let detail = format!("variable: {name}");
            push_ast_node(json, "Variable", &label, &detail, &statement.span, |j| {
                push_expression_ast_json(j, initializer);
            });
        }
        StatementKind::Assignment { name, value } => {
            let label = format!("{name} = ...");
            let detail = format!("assignment to: {name}");
            push_ast_node(json, "Assignment", &label, &detail, &statement.span, |j| {
                push_expression_ast_json(j, value);
            });
        }
        StatementKind::Expression(expr) => {
            push_ast_node(json, "ExpressionStatement", "Expression", "", &statement.span, |j| {
                push_expression_ast_json(j, expr);
            });
        }
        StatementKind::If {
            condition,
            then_branch,
            else_branch,
        } => {
            let detail = format!("then: {} stmts, else: {} stmts", then_branch.len(), else_branch.len());
            push_ast_node(json, "If", "if condition", &detail, &statement.span, |j| {
                push_expression_ast_json(j, condition);
                for stmt in then_branch {
                    j.push(',');
                    push_statement_ast_json(j, stmt);
                }
                for stmt in else_branch {
                    j.push(',');
                    push_statement_ast_json(j, stmt);
                }
            });
        }
        StatementKind::Repeat { count, body } => {
            let detail = format!("body: {} stmts", body.len());
            push_ast_node(json, "Repeat", "repeat loop", &detail, &statement.span, |j| {
                push_expression_ast_json(j, count);
                for stmt in body {
                    j.push(',');
                    push_statement_ast_json(j, stmt);
                }
            });
        }
        StatementKind::While { condition, body } => {
            let detail = format!("body: {} stmts", body.len());
            push_ast_node(json, "While", "while loop", &detail, &statement.span, |j| {
                push_expression_ast_json(j, condition);
                for stmt in body {
                    j.push(',');
                    push_statement_ast_json(j, stmt);
                }
            });
        }
        StatementKind::Function {
            name,
            parameters,
            body,
        } => {
            let label = format!("action {name}({})", parameters.join(", "));
            let detail = format!("{} parameters, {} statements", parameters.len(), body.len());
            push_ast_node(json, "Function", &label, &detail, &statement.span, |j| {
                for (idx, stmt) in body.iter().enumerate() {
                    if idx > 0 {
                        j.push(',');
                    }
                    push_statement_ast_json(j, stmt);
                }
            });
        }
        StatementKind::Class { name, methods } => {
            let label = format!("class {name}");
            let detail = format!("{} methods", methods.len());
            push_ast_node(json, "Class", &label, &detail, &statement.span, |j| {
                for (idx, stmt) in methods.iter().enumerate() {
                    if idx > 0 {
                        j.push(',');
                    }
                    push_statement_ast_json(j, stmt);
                }
            });
        }
        StatementKind::Return(value) => {
            let detail = if value.is_some() { "returns value" } else { "returns void" };
            push_ast_node(json, "Return", "return", detail, &statement.span, |j| {
                if let Some(val) = value {
                    push_expression_ast_json(j, val);
                }
            });
        }
        StatementKind::ProfileBlock { profile, body } => {
            let label = match profile {
                sprout_core::parser::SyntaxProfile::Guided => "guided { ... }",
                sprout_core::parser::SyntaxProfile::Structured => "structured { ... }",
                sprout_core::parser::SyntaxProfile::Advanced => "advanced { ... }",
            };
            let detail = format!("profile block: {profile:?}, {} statements", body.len());
            push_ast_node(json, "ProfileBlock", label, &detail, &statement.span, |j| {
                for (idx, stmt) in body.iter().enumerate() {
                    if idx > 0 {
                        j.push(',');
                    }
                    push_statement_ast_json(j, stmt);
                }
            });
        }
    }
}

fn push_expression_ast_json(json: &mut String, expression: &sprout_core::ast::Expression) {
    use sprout_core::ast::ExpressionKind;
    match &expression.kind {
        ExpressionKind::Integer(v) => {
            let label = v.to_string();
            push_ast_node(json, "Integer", &label, "Int literal", &expression.span, |_| {});
        }
        ExpressionKind::Float(v) => {
            let label = v.to_string();
            push_ast_node(json, "Float", &label, "Float literal", &expression.span, |_| {});
        }
        ExpressionKind::Bool(v) => {
            let label = v.to_string();
            push_ast_node(json, "Bool", &label, "Bool literal", &expression.span, |_| {});
        }
        ExpressionKind::String(v) => {
            let label = format!("\"{v}\"");
            push_ast_node(json, "String", &label, "String literal", &expression.span, |_| {});
        }
        ExpressionKind::Nil => {
            push_ast_node(json, "Nil", "nil", "Nil literal", &expression.span, |_| {});
        }
        ExpressionKind::Variable(name) => {
            push_ast_node(json, "VariableIdentifier", name, "Variable reference", &expression.span, |_| {});
        }
        ExpressionKind::Unary { operator, operand } => {
            let label = format!("{operator:?}");
            push_ast_node(json, "Unary", &label, "Unary operator", &expression.span, |j| {
                push_expression_ast_json(j, operand);
            });
        }
        ExpressionKind::Binary {
            left,
            operator,
            right,
        } => {
            let label = format!("{operator:?}");
            push_ast_node(json, "Binary", &label, "Binary operator", &expression.span, |j| {
                push_expression_ast_json(j, left);
                j.push(',');
                push_expression_ast_json(j, right);
            });
        }
        ExpressionKind::Call { callee, arguments } => {
            let detail = format!("{} arguments", arguments.len());
            push_ast_node(json, "Call", "Call", &detail, &expression.span, |j| {
                push_expression_ast_json(j, callee);
                for arg in arguments {
                    j.push(',');
                    push_expression_ast_json(j, arg);
                }
            });
        }
        ExpressionKind::Member {
            object,
            name,
            member_span: _,
        } => {
            let label = format!(".{name}");
            push_ast_node(json, "Member", &label, "Member access", &expression.span, |j| {
                push_expression_ast_json(j, object);
            });
        }
        ExpressionKind::Index { object, index } => {
            push_ast_node(json, "Index", "[index]", "Index access", &expression.span, |j| {
                push_expression_ast_json(j, object);
                j.push(',');
                push_expression_ast_json(j, index);
            });
        }
    }
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
    fn engine_info_reports_version_abi_architecture_and_configuration() {
        let buffer = sprout_engine_info();
        // SAFETY: The exported buffer owns `length` readable bytes until it is freed below.
        let bytes = unsafe { slice::from_raw_parts(buffer.data, buffer.length) };
        let response = str::from_utf8(bytes).unwrap();
        assert!(
            response.contains(&format!(
                "\"engineVersion\":\"{}\"",
                SPROUT_ENGINE_VERSION.trim()
            )),
            "{response}"
        );
        assert!(response.contains("\"abiVersion\":1"), "{response}");
        assert!(
            response.contains("\"architecture\":\"x86_64\""),
            "{response}"
        );
        let configuration = if cfg!(debug_assertions) {
            "debug"
        } else {
            "release"
        };
        assert!(
            response.contains(&format!("\"buildConfiguration\":\"{configuration}\"")),
            "{response}"
        );
        // SAFETY: This is the matching allocator release operation, called exactly once.
        unsafe { sprout_buffer_free(buffer) };
    }

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
    fn run_response_contains_canvas_protocol() {
        let response = process(
            SPROUT_OPERATION_RUN,
            SPROUT_PROFILE_GUIDED,
            "canvas.spr",
            "canvas_clear(\"#101010\")\ncanvas_rect(1, 2, 3, 4, \"green\")\n",
        )
        .unwrap();
        assert!(response.contains("\"kind\":\"clear\""), "{response}");
        assert!(response.contains("\"kind\":\"rectangle\""), "{response}");
        assert!(response.contains("\"width\":3"), "{response}");
    }

    #[test]
    fn debug_response_contains_variable_snapshots() {
        let response = process(
            SPROUT_OPERATION_DEBUG,
            SPROUT_PROFILE_GUIDED,
            "debug.spr",
            "has value = 3\nprint(value)\n",
        )
        .unwrap();
        assert!(response.contains("\"debugSnapshots\":[{"), "{response}");
        assert!(response.contains("\"name\":\"value\""), "{response}");
        assert!(response.contains("\"value\":\"3\""), "{response}");
    }

    #[test]
    fn language_service_returns_inspection_and_formatting() {
        let source = "has name = \"Sprout\"\nprint(na)\n";
        let inspected = language_service_process(
            SPROUT_LANGUAGE_INSPECT,
            SPROUT_PROFILE_GUIDED,
            source.rfind("na").unwrap() + 2,
            "test.spr",
            source,
        )
        .unwrap();
        assert!(inspected.contains("\"label\":\"name\""), "{inspected}");
        assert!(inspected.contains("\"definition\":"), "{inspected}");

        let formatted = language_service_process(
            SPROUT_LANGUAGE_FORMAT,
            SPROUT_PROFILE_GUIDED,
            0,
            "test.spr",
            "has name=\"Sprout\"\n",
        )
        .unwrap();
        assert!(
            formatted.contains("\"formattedSource\":\"has name = \\\"Sprout\\\"\\n\""),
            "{formatted}"
        );
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

    #[test]
    fn get_ast_json_returns_serialized_ast_tree() {
        use std::ffi::CString;
        let source = CString::new("import math\nhas x = 10\naction add(a, b)\n    return a + b\nend\nprint(add(x, 20))\n").unwrap();
        let buffer = unsafe { sprout_get_ast_json(source.as_ptr()) };
        let text = unsafe { slice::from_raw_parts(buffer.data, buffer.length) };
        let text = str::from_utf8(text).unwrap();
        assert!(text.contains("\"kind\":\"Program\""), "{text}");
        assert!(text.contains("\"kind\":\"Import\""));
        assert!(text.contains("\"kind\":\"Variable\""));
        assert!(text.contains("\"kind\":\"Function\""));
        assert!(text.contains("\"kind\":\"Call\""));
        assert!(text.contains("\"startLine\":1"));
        unsafe { sprout_buffer_free(buffer) };
    }

    #[test]
    fn get_stdlib_docs_json_returns_metadata() {
        let buffer = unsafe { sprout_get_stdlib_docs_json() };
        let text = unsafe { slice::from_raw_parts(buffer.data, buffer.length) };
        let text = str::from_utf8(text).unwrap();
        assert!(text.contains("\"name\": \"sqrt\""), "{text}");
        assert!(text.contains("\"module\": \"math\""));
        assert!(text.contains("\"module\": \"string\""));
        assert!(text.contains("\"module\": \"io\""));
        assert!(text.contains("\"module\": \"json\""));
        unsafe { sprout_buffer_free(buffer) };
    }
}
