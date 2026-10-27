using System;
using System.Diagnostics;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DeepSeekTray.Localization;
using DeepSeekTray.Services;

namespace DeepSeekTray.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();

        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "2.0.0";
        VersionText.Text = Loc.F("About.VersionFmt", version);
    }

    private void Console_Click(object? sender, RoutedEventArgs e)
        => Open("https://platform.deepseek.com/api_keys");

    private void Folder_Click(object? sender, RoutedEventArgs e)
        => Open(SettingsStore.Folder);

    private static void Open(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Error("打开链接失败：" + target, ex);
        }
    }

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}
