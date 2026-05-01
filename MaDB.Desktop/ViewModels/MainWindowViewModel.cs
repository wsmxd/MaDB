using System;
using System.Collections.ObjectModel;
using Avalonia.Markup.Xaml.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaDB.Core;

namespace MaDB.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public DatabaseDialect SelectedDialect { get; } = DatabaseDialect.Sqlite;
    public DatabaseAccessMode AccessMode { get; } = DatabaseAccessMode.ReadOnly;
    public string SelectedTarget { get; } = "demo.db";

    [ObservableProperty]
    private string sqlText = "SELECT name, type, sql\nFROM sqlite_master\nORDER BY type, name;";

    [ObservableProperty]
    private string statusMessage = "";

    public MainWindowViewModel()
    {
        StatusMessage = GetLocalizedString("VmStatusReady") ?? "Workspace loaded and ready.";
    }

    public ObservableCollection<ConnectionCardViewModel> Connections { get; } =
    [
        new ConnectionCardViewModel(
            "Local SQLite",
            "demo.db",
            "Sqlite",
            "readonly",
            "Read-only playground with schema and table preview.",
            true),
        new ConnectionCardViewModel(
            "Analytics Mirror",
            "analytics.db",
            "Sqlite",
            "readwrite",
            "Prepared for later import and export workflows.",
            false),
        new ConnectionCardViewModel(
            "PostgreSQL staging",
            "pg-staging:5432/app",
            "PostgreSql",
            "readonly",
            "Reserved for provider expansion and capability checks.",
            false)
    ];

    public ObservableCollection<string> ResultPreviewRows { get; } =
    [
        "name | type | sql",
        "sqlite_sequence | table | internal bookkeeping",
        "users | table | CREATE TABLE users (...)",
        "orders | view | CREATE VIEW orders AS ..."
    ];

    public ObservableCollection<ActivityEntryViewModel> ActivityFeed { get; } =
    [
        new ActivityEntryViewModel("09:12:04", "Workspace initialized with the SQLite demo target."),
        new ActivityEntryViewModel("09:12:10", "Read-only mode is active, so write operations stay blocked.")
    ];

    [RelayCommand]
    private void RunQueryPreview()
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var reqMsg = GetLocalizedString("VmMsgPreviewReq") ?? "Preview requested for";
        ActivityFeed.Insert(0, new ActivityEntryViewModel(timestamp, $"{reqMsg} {SelectedTarget}."));

        while (ActivityFeed.Count > 5)
        {
            ActivityFeed.RemoveAt(ActivityFeed.Count - 1);
        }

        var queMsg = GetLocalizedString("VmMsgPreviewQue") ?? "Preview queued at";
        StatusMessage = $"{queMsg} {timestamp}.";
    }

    [RelayCommand]
    private void ToggleLanguage()
    {
        if (Avalonia.Application.Current is { } app && app.Resources.MergedDictionaries.Count > 0)
        {
            var isZh = false;
            
            if (app.Resources.MergedDictionaries[0] is ResourceInclude currentInclude)
            {
                isZh = currentInclude.Source?.ToString().Contains("zh-CN") == true;
            }
            
            var newLang = isZh ? "en-US" : "zh-CN";
            var uri = new Uri($"avares://MaDB.Desktop/Assets/i18n/{newLang}.axaml");
            
            app.Resources.MergedDictionaries[0] = new ResourceInclude(uri) { Source = uri };
            
            StatusMessage = GetLocalizedString("VmStatusReady") ?? StatusMessage;
        }
    }
    
    private static string? GetLocalizedString(string key)
    {
        if (Avalonia.Application.Current != null && 
            Avalonia.Application.Current.TryGetResource(key, Avalonia.Styling.ThemeVariant.Default, out var value) && 
            value is string strValue)
        {
            return strValue;
        }
        return null;
    }
}
