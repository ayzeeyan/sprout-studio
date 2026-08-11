using SproutStudio.Toolchains;

namespace SproutStudio.Editor;

public sealed class ExternalLanguageService
{
    private readonly ToolchainManager _toolchains;

    public ExternalLanguageService(ToolchainManager toolchains)
    {
        _toolchains = toolchains ?? throw new ArgumentNullException(nameof(toolchains));
    }

    public async Task<ProcessResult> ExecuteAsync(
        EditorDocument document,
        ToolchainAction action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        string toolchainId = document.Language.ToolchainId
            ?? throw new NotSupportedException(
                $"{document.Language.DisplayName} does not have an executable toolchain adapter yet.");
        if (document.FilePath is null)
        {
            throw new InvalidOperationException("Save this document before running an external toolchain.");
        }
        ToolchainCapability capability = action switch
        {
            ToolchainAction.Run => ToolchainCapability.Run,
            ToolchainAction.Build => ToolchainCapability.Build,
            ToolchainAction.Format => ToolchainCapability.Format,
            ToolchainAction.Test => ToolchainCapability.Test,
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };
        if (!document.Language.Capabilities.HasFlag(ToEditorCapability(capability)))
        {
            throw new NotSupportedException(
                $"{document.Language.DisplayName} does not support {action.ToString().ToLowerInvariant()} in Sprout Studio.");
        }
        string workingDirectory = FindProjectRoot(document.FilePath, document.Language.Id);
        return await _toolchains.ExecuteAsync(
            toolchainId,
            action,
            workingDirectory,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static string FindProjectRoot(string filePath, DocumentLanguage language)
    {
        string marker = language switch
        {
            DocumentLanguage.Rust => "Cargo.toml",
            DocumentLanguage.Go => "go.mod",
            _ => throw new NotSupportedException("Only Rust and Go project roots are supported."),
        };
        DirectoryInfo? directory = new(Path.GetDirectoryName(Path.GetFullPath(filePath))
            ?? throw new InvalidOperationException("Document path has no parent directory."));
        DirectoryInfo fallback = directory;
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, marker)))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        return fallback.FullName;
    }

    private static EditorCapability ToEditorCapability(ToolchainCapability capability) =>
        capability switch
        {
            ToolchainCapability.Run => EditorCapability.Run,
            ToolchainCapability.Build => EditorCapability.Build,
            ToolchainCapability.Format => EditorCapability.Format,
            ToolchainCapability.Test => EditorCapability.Test,
            _ => EditorCapability.None,
        };
}
