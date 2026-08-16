using System.Text;

namespace SproutStudio.Services;

public static class StudioLog
{
    private const long MaximumLogBytes = 2 * 1024 * 1024;
    private static readonly object Gate = new();
    private static readonly string DirectoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SproutStudio",
        "Logs");
    private static readonly string FilePath = Path.Combine(DirectoryPath, "studio.log");

    public static string CurrentFilePath => FilePath;

    public static string LogsDirectoryPath => DirectoryPath;

    public static void Information(string message) => Write("INFO", message, null);

    public static void Warning(string message, Exception? exception = null) =>
        Write("WARN", message, exception);

    public static void Error(string message, Exception exception) =>
        Write("ERROR", message, exception);

    private static void Write(string level, string message, Exception? exception)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(DirectoryPath);
                RotateIfNeeded();
                var line = new StringBuilder()
                    .Append(DateTimeOffset.Now.ToString("O"))
                    .Append(" [")
                    .Append(level)
                    .Append("] ")
                    .AppendLine(message.ReplaceLineEndings(" "));
                if (exception is not null)
                {
                    line.AppendLine(exception.ToString());
                }
                File.AppendAllText(FilePath, line.ToString(), Encoding.UTF8);
            }
        }
        catch (Exception logException) when (logException is IOException
            or UnauthorizedAccessException)
        {
            // Logging must never become a second application failure.
        }
    }

    private static void RotateIfNeeded()
    {
        if (!File.Exists(FilePath) || new FileInfo(FilePath).Length < MaximumLogBytes)
        {
            return;
        }

        string previous = Path.Combine(DirectoryPath, "studio.previous.log");
        File.Move(FilePath, previous, overwrite: true);
    }
}
