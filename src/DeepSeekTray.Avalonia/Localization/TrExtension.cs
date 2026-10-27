using System;
using Avalonia.Markup.Xaml;

namespace DeepSeekTray.Localization;

/// <summary>XAML 里取文案：Text="{loc:Tr Accounts.Title}"</summary>
public sealed class TrExtension : MarkupExtension
{
    public TrExtension()
    {
    }

    public TrExtension(string key) => Key = key;

    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider) => Loc.T(Key);
}
