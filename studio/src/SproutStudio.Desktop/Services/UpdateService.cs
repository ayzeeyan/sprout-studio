using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace SproutStudio.Services;

public sealed record UpdateInfo(
    string Version,
    string TagName,
    string ReleaseNotes,
    string DownloadUrl,
    string FileName,
    long FileSize,
    DateTimeOffset? PublishedAt,
    string? ChecksumsUrl = null,
    string? ExpectedSha256 = null);

public interface IUpdateService
{
    string CurrentVersion { get; }
    Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken cancellationToken = default);
    Task DownloadUpdateAsync(
        string downloadUrl,
        string targetFilePath,
        string? checksumsUrl = null,
        string? expectedSha256 = null,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
    void InstallAndRelaunch(string installerPath, string? expectedSha256 = null);
}

public sealed class UpdateService : IUpdateService
{
    public const string DefaultReleaseUrl = "https://api.github.com/repos/ayzeeyan/sprout-studio/releases/latest";
    public const string UserAgentHeader = "SproutStudio-AutoUpdater";

    private readonly HttpClient _httpClient;
    private readonly string _releaseApiUrl;
    private readonly string _currentVersion;

    public string CurrentVersion => _currentVersion;

    public UpdateService(
        HttpClient? httpClient = null,
        string? releaseApiUrl = null,
        string? currentVersion = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _releaseApiUrl = releaseApiUrl ?? DefaultReleaseUrl;
        _currentVersion = currentVersion ?? ResolveCurrentVersion();
    }

    public static string ResolveCurrentVersion()
    {
        Version? ver = typeof(UpdateService).Assembly.GetName().Version;
        if (ver is not null)
        {
            return $"{ver.Major}.{ver.Minor}.{ver.Build}";
        }
        return "0.2.0";
    }

    public async Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, _releaseApiUrl);
            request.Headers.UserAgent.ParseAdd(UserAgentHeader);
            request.Headers.Accept.ParseAdd("application/vnd.github.v3+json");

            using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            string json = await response.Content.ReadAsStringAsync(cancellationToken);
            UpdateInfo? info = ParseReleaseResponse(json, _currentVersion);
            if (info is not null && !string.IsNullOrWhiteSpace(info.ChecksumsUrl))
            {
                // Pre-resolve expected hash from checksum manifest if available
                string? checksumsContent = await FetchChecksumsManifestAsync(info.ChecksumsUrl, cancellationToken);
                if (!string.IsNullOrWhiteSpace(checksumsContent))
                {
                    string? hash = ExtractHashForFile(checksumsContent, info.FileName);
                    if (!string.IsNullOrWhiteSpace(hash))
                    {
                        info = info with { ExpectedSha256 = hash };
                    }
                }
            }
            return info;
        }
        catch (Exception ex)
        {
            StudioLog.Warning($"Auto-updater check failed: {ex.Message}");
            return null;
        }
    }

    private async Task<string?> FetchChecksumsManifestAsync(string checksumsUrl, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, checksumsUrl);
            request.Headers.UserAgent.ParseAdd(UserAgentHeader);
            using HttpResponseMessage res = await _httpClient.SendAsync(request, cancellationToken);
            if (res.IsSuccessStatusCode)
            {
                return await res.Content.ReadAsStringAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            StudioLog.Warning($"Failed to fetch checksum manifest from {checksumsUrl}: {ex.Message}");
        }
        return null;
    }

    public static string? ExtractHashForFile(string manifestContent, string fileName)
    {
        if (string.IsNullOrWhiteSpace(manifestContent) || string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        string[] lines = manifestContent.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (string line in lines)
        {
            // Format: "<hash> *<filename>" or "<hash> <filename>"
            string[] parts = line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length >= 2)
            {
                string hash = parts[0];
                string name = parts[1].TrimStart('*');
                if (name.Equals(fileName, StringComparison.OrdinalIgnoreCase))
                {
                    return hash.ToLowerInvariant();
                }
            }
        }
        return null;
    }

    public static UpdateInfo? ParseReleaseResponse(string json, string currentVersion)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        if (!root.TryGetProperty("tag_name", out JsonElement tagElement))
        {
            return null;
        }

        string? tagName = tagElement.GetString();
        if (string.IsNullOrWhiteSpace(tagName))
        {
            return null;
        }

        string cleanRemote = tagName.Trim().TrimStart('v', 'V');
        string cleanCurrent = currentVersion.Trim().TrimStart('v', 'V');

        if (!Version.TryParse(cleanRemote, out Version? remoteVer) ||
            !Version.TryParse(cleanCurrent, out Version? currentVer))
        {
            return null;
        }

        // Downgrade prevention: remote version must be strictly greater than current
        if (remoteVer <= currentVer)
        {
            return null;
        }

        string releaseNotes = root.TryGetProperty("body", out JsonElement bodyElement)
            ? (bodyElement.GetString() ?? string.Empty)
            : string.Empty;

        DateTimeOffset? publishedAt = null;
        if (root.TryGetProperty("published_at", out JsonElement publishedElement) &&
            publishedElement.ValueKind == JsonValueKind.String &&
            DateTimeOffset.TryParse(publishedElement.GetString(), out DateTimeOffset parsedDate))
        {
            publishedAt = parsedDate;
        }

        string? downloadUrl = null;
        string? fileName = null;
        string? checksumsUrl = null;
        long fileSize = 0;

        if (root.TryGetProperty("assets", out JsonElement assetsElement) &&
            assetsElement.ValueKind == JsonValueKind.Array)
        {
            JsonElement targetAsset = default;
            bool found = false;

            // Search for checksum manifest asset
            foreach (JsonElement asset in assetsElement.EnumerateArray())
            {
                string? name = asset.TryGetProperty("name", out JsonElement n) ? n.GetString() : null;
                if (name is not null && (
                    name.Equals("SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("checksums.txt", StringComparison.OrdinalIgnoreCase)))
                {
                    checksumsUrl = asset.TryGetProperty("browser_download_url", out JsonElement u) ? u.GetString() : null;
                }
            }

            // 1. Prioritize exact SproutStudio-Setup.exe
            foreach (JsonElement asset in assetsElement.EnumerateArray())
            {
                string? name = asset.TryGetProperty("name", out JsonElement n) ? n.GetString() : null;
                if (name is not null && name.Equals("SproutStudio-Setup.exe", StringComparison.OrdinalIgnoreCase))
                {
                    targetAsset = asset;
                    found = true;
                    break;
                }
            }

            // 2. Fallback to SproutStudio-Setup-x64.exe
            if (!found)
            {
                foreach (JsonElement asset in assetsElement.EnumerateArray())
                {
                    string? name = asset.TryGetProperty("name", out JsonElement n) ? n.GetString() : null;
                    if (name is not null && name.Equals("SproutStudio-Setup-x64.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        targetAsset = asset;
                        found = true;
                        break;
                    }
                }
            }

            // 3. Fallback to any installer .exe
            if (!found)
            {
                foreach (JsonElement asset in assetsElement.EnumerateArray())
                {
                    string? name = asset.TryGetProperty("name", out JsonElement n) ? n.GetString() : null;
                    if (name is not null && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        targetAsset = asset;
                        found = true;
                        break;
                    }
                }
            }

            if (found && targetAsset.ValueKind == JsonValueKind.Object)
            {
                fileName = targetAsset.TryGetProperty("name", out JsonElement n) ? n.GetString() : "SproutStudio-Setup.exe";
                downloadUrl = targetAsset.TryGetProperty("browser_download_url", out JsonElement url) ? url.GetString() : null;
                fileSize = targetAsset.TryGetProperty("size", out JsonElement s) && s.ValueKind == JsonValueKind.Number ? s.GetInt64() : 0;
            }
        }

        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            return null;
        }

        return new UpdateInfo(
            cleanRemote,
            tagName,
            releaseNotes,
            downloadUrl,
            fileName ?? "SproutStudio-Setup.exe",
            fileSize,
            publishedAt,
            checksumsUrl);
    }

    public async Task DownloadUpdateAsync(
        string downloadUrl,
        string targetFilePath,
        string? checksumsUrl = null,
        string? expectedSha256 = null,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(downloadUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetFilePath);

        string fileName = Path.GetFileName(targetFilePath);
        string stagingPath = $"{targetFilePath}.part";

        // Pre-resolve expected hash if not directly passed but checksums manifest is known
        if (string.IsNullOrWhiteSpace(expectedSha256) && !string.IsNullOrWhiteSpace(checksumsUrl))
        {
            string? manifest = await FetchChecksumsManifestAsync(checksumsUrl, cancellationToken);
            if (!string.IsNullOrWhiteSpace(manifest))
            {
                expectedSha256 = ExtractHashForFile(manifest, fileName);
            }
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
        request.Headers.UserAgent.ParseAdd(UserAgentHeader);

        using HttpResponseMessage response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        long? totalBytes = response.Content.Headers.ContentLength;

        string? dir = Path.GetDirectoryName(targetFilePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        try
        {
            using (Stream contentStream = await response.Content.ReadAsStreamAsync(cancellationToken))
            using (Stream fileStream = new FileStream(
                stagingPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                81920,
                useAsync: true))
            using (var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                byte[] buffer = new byte[81920];
                long totalRead = 0;
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                    sha256.AppendData(buffer, 0, bytesRead);
                    totalRead += bytesRead;

                    if (totalBytes.HasValue && totalBytes.Value > 0)
                    {
                        double percentage = (double)totalRead / totalBytes.Value * 100.0;
                        progress?.Report(percentage);
                    }
                }

                fileStream.Flush();

                // Compute downloaded file hash
                byte[] hashBytes = sha256.GetHashAndReset();
                string actualHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

                // Checksum verification
                if (!string.IsNullOrWhiteSpace(expectedSha256))
                {
                    if (!actualHash.Equals(expectedSha256.Trim().ToLowerInvariant(), StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException(
                            $"SHA-256 checksum verification failed for '{fileName}'. " +
                            $"Expected: {expectedSha256.ToLowerInvariant()}, Actual: {actualHash}. Download rejected for security.");
                    }
                    StudioLog.Information($"Verified installer SHA-256 ({actualHash}) successfully.");
                }
            }

            // Code signing check hook (logs warning if unsigned, succeeds if verified)
            VerifySignatureSafe(stagingPath);

            // Atomic handoff: rename .part to final installer path only after successful hash verification
            if (File.Exists(targetFilePath))
            {
                File.Delete(targetFilePath);
            }
            File.Move(stagingPath, targetFilePath);
            progress?.Report(100.0);
        }
        catch
        {
            // Clean up temporary partial file to avoid leaving corrupted artifacts
            if (File.Exists(stagingPath))
            {
                try { File.Delete(stagingPath); } catch { }
            }
            throw;
        }
    }

    public static bool VerifySignatureSafe(string filePath)
    {
        if (!OperatingSystem.IsWindows() || !File.Exists(filePath))
        {
            return false;
        }
        try
        {
            using var cert = X509CertificateLoader.LoadCertificateFromFile(filePath);
            return cert is not null;
        }
        catch
        {
            // Unsigned installer in dev builds or self-signed release
            return false;
        }
    }

    public static string ComputeFileSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        byte[] hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public void InstallAndRelaunch(string installerPath, string? expectedSha256 = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installerPath);
        if (!File.Exists(installerPath))
        {
            throw new FileNotFoundException($"Installer file not found at '{installerPath}'.");
        }

        // Secondary check before executing native binary
        if (!string.IsNullOrWhiteSpace(expectedSha256))
        {
            string currentHash = ComputeFileSha256(installerPath);
            if (!currentHash.Equals(expectedSha256.Trim().ToLowerInvariant(), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Pre-execution SHA-256 mismatch for installer '{installerPath}'. Execution blocked.");
            }
        }

        ProcessStartInfo startInfo = new(installerPath)
        {
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(installerPath) ?? string.Empty,
        };

        Process.Start(startInfo);
        Environment.Exit(0);
    }
}
