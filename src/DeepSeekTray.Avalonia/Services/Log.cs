using System;
using System.IO;

namespace DeepSeekTray.Services;

/// <summary>极简文件日志，落在 %APPDATA%\DeepSeekTray\app.log，超过 512KB 自动重建。</summary>
public static class Log
{
    private static readonly object Gate = new();

    public static string FilePath => Path.Combine(SettingsStore.Folder, "app.log");

    public static void Info(string message) => Write("INFO", message);

    public static void Warn(string message) => Write("WARN", message);

    public static void Error(string message, Exception? ex = null)
        => Write("ERROR", ex is null ? message : $"{message} :: {ex.GetType().Name}: {ex.Message}");

    private static void Write(string level, string message)
    {
        try
        {
            System.IO.Directory.CreateDirectory(SettingsStore.Folder);

            var file = new FileInfo(FilePath);
            if (file.Exists && file.Length > 512 * 1024)
            {
                file.Delete();
            }

            var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}{Environment.NewLine}";
            lock (Gate)
            {
                File.AppendAllText(FilePath, line);
            }
        }
        catch
        {
            // 日志失败不能影响主流程
        }
    }
}
