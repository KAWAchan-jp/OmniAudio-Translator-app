using System.Text;

namespace OmniAudioTranslator.Core.Diagnostics;

/// <summary>
/// クラウドASR等の失敗を後から追跡するための軽量エラーログ。
/// <c>%AppData%\OmniAudioTranslator\logs\error.log</c>へ追記する。
/// ログ出力自体の失敗でアプリを止めないことを優先し、例外は握りつぶす。
/// </summary>
public static class ErrorLog
{
    private const long MaxFileSize = 2 * 1024 * 1024;

    private static readonly object SyncRoot = new();

    public static string LogFilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "OmniAudioTranslator",
        "logs",
        "error.log");

    public static void Write(string context, string message, Exception? exception = null)
    {
        try
        {
            lock (SyncRoot)
            {
                string? directory = Path.GetDirectoryName(LogFilePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                RotateIfNeeded();

                var line = new StringBuilder();
                line.Append('[').Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append("] [").Append(context).Append("] ").AppendLine(message);
                if (exception is not null)
                {
                    line.AppendLine(exception.ToString());
                }

                File.AppendAllText(LogFilePath, line.ToString());
            }
        }
        catch (IOException)
        {
            // ログが書けないことでアプリを止めない。
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void RotateIfNeeded()
    {
        try
        {
            var info = new FileInfo(LogFilePath);
            if (!info.Exists || info.Length < MaxFileSize)
            {
                return;
            }

            string backupPath = LogFilePath + ".1";
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }

            File.Move(LogFilePath, backupPath);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
