using System;
using System.IO;
using System.Text.Json;
using DeepSeekTray.Models;

namespace DeepSeekTray.Services;

/// <summary>settings.json 的读写。路径固定为 %APPDATA%\DeepSeekTray\settings.json。</summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
    };

    public static string Folder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DeepSeekTray");

    public string FilePath => Path.Combine(Folder, "settings.json");

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();
        }
        catch (Exception ex)
        {
            Log.Error("读取设置失败，将使用默认设置", ex);
            TryBackup();
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            var temp = FilePath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(settings, Options));
            File.Move(temp, FilePath, true);
        }
        catch (Exception ex)
        {
            Log.Error("保存设置失败", ex);
        }
    }

    private void TryBackup()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                File.Move(FilePath, FilePath + ".corrupt", true);
            }
        }
        catch
        {
            // 备份失败无所谓
        }
    }
}
