using System;
using System.Globalization;
using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MaDB.Desktop.Services;

public partial class LocalizationService : ObservableObject, ILocalizationService
{
    [ObservableProperty]
    private string _currentLanguage = "zh-CN";

    public void ToggleLanguage()
    {
        if (Application.Current is not { } app)
        {
            return;
        }

        var dicts = app.Resources.MergedDictionaries;
        if (dicts.Count < 2)
        {
            return;
        }

        CurrentLanguage = CurrentLanguage == "zh-CN" ? "en-US" : "zh-CN";
        var uri = new Uri($"avares://MaDB.Desktop/Assets/i18n/{CurrentLanguage}.axaml");
        dicts[1] = new ResourceInclude(uri) { Source = uri };
    }

    public string? GetLocalizedString(string key)
    {
        if (Application.Current != null &&
            Application.Current.TryGetResource(key, ThemeVariant.Default, out var value) &&
            value is string strValue)
        {
            return strValue;
        }

        return null;
    }

    public string FormatLocalizedString(string key, params object[] args)
    {
        var format = GetLocalizedString(key);
        if (format is null)
        {
            return key;
        }

        return string.Format(CultureInfo.CurrentCulture, format, args);
    }
}