//! Stable, coarse-grained C ABI for Sprout Studio and other native hosts.

use sprout_core::diagnostic::{Diagnostic, Severity};
use sprout_core::source::SourceFile;
use sprout_core::{SyntaxProfile, check_profile, execute_profile};
use std::fmt::Write;
use std::panic::{AssertUnwindSafe, catch_unwind};
use std::slice;
use std::str;

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
            let result = execute_profile(&source, profile);
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
        json.push_str(",\"span\":{\"file\":");
        push_json_string(&mut json, &diagnostic.span.file);
        let _ = write!(
            json,
            ",\"start\":{},\"end\":{},\"startLine\":{},\"startColumn\":{},\"endLine\":{},\"endColumn\":{}",
            diagnostic.span.start,
            diagnostic.span.end,
            diagnostic.span.start_line,
            diagnostic.span.start_column,
            diagnostic.span.end_line,
            diagnostic.span.end_column
        );
        json.push_str("},\"suggestions\":[");
        for (suggestion_index, suggestion) in diagnostic.suggestions.iter().enumerate() {
            if suggestion_index > 0 {
                json.push(',');
            }
            push_json_string(&mut json, suggestion);
        }
        json.push_str("]}");
    }
    json.push_str("]}");
    json
}

fn internal_error(message: &str) -> String {
    let mut json = String::from(
        "{\"apiVersion\":1,\"success\":false,\"output\":[],\"diagnostics\":[{\"code\":\"SPT900\",\"severity\":\"error\",\"title\":\"Engine failure\",\"message\":",
    );
    push_json_string(&mut json, message);
    json.push_str(",\"span\":{\"file\":\"<engine>\",\"start\":0,\"end\":0,\"startLine\":1,\"startColumn\":1,\"endLine\":1,\"endColumn\":1},\"suggestions\":[]}]}");
    json
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
}
