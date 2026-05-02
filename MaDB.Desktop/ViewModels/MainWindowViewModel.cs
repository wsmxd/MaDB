using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaDB.Core;
using MaDB.Desktop.Services;

namespace MaDB.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly DatabaseWorkspaceService _workspaceService;
    private readonly ILocalizationService _localizationService;
    private readonly IThemeService _themeService;

    public MainWindowViewModel(
        DatabaseWorkspaceService workspaceService,
        ILocalizationService localizationService,
        IThemeService themeService)
    {
        _workspaceService = workspaceService;
        _localizationService = localizationService;
        _themeService = themeService;

        ConnectionManager = new ConnectionManagerViewModel(workspaceService, localizationService);
        TableBrowser = new TableBrowserViewModel(workspaceService, localizationService);
        ActivityFeed = new ActivityFeedViewModel();

        SelectedDialect = _workspaceService.Dialect;
        AccessMode = _workspaceService.AccessMode;
        SelectedTarget = _workspaceService.DatabaseFileName;
        StatusMessage = _localizationService.GetLocalizedString("VmStatusConnecting") ?? "Connecting to SQLite workspace...";
    }

    public ConnectionManagerViewModel ConnectionManager { get; }

    public TableBrowserViewModel TableBrowser { get; }

    public ActivityFeedViewModel ActivityFeed { get; }

    public ObservableCollection<TabViewModelBase> Tabs { get; } = [];

    [ObservableProperty]
    private TabViewModelBase? _selectedTab;

    partial void OnSelectedTabChanged(TabViewModelBase? oldValue, TabViewModelBase? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.IsSelected = false;
        }

        if (newValue is not null)
        {
            newValue.IsSelected = true;
        }
    }

    public DatabaseDialect SelectedDialect { get; }

    public DatabaseAccessMode AccessMode { get; }

    public string SelectedTarget { get; }

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _themeIcon = "\U0001f319";

    public async Task InitializeAsync()
    {
        try
        {
            await _workspaceService.InitializeAsync();
            await TableBrowser.LoadSchemaAsync();

            ActivityFeed.AddActivity(_localizationService.GetLocalizedString("VmMsgWorkspaceReady") ?? "Database workspace initialized.");
            StatusMessage = _localizationService.GetLocalizedString("VmStatusReady") ?? "Workspace loaded and ready.";

            var footerFormat = _localizationService.GetLocalizedString("VmFooterSummary") ?? "SQLite workspace ready with {0} schema objects.";
            ActivityFeed.UpdateFooterSummary(TableBrowser.Tables.Count, footerFormat);
        }
        catch (Exception exception)
        {
            StatusMessage = $"{_localizationService.GetLocalizedString("VmStatusError") ?? "Database error:"} {exception.Message}";
            ActivityFeed.AddActivity(StatusMessage);
        }
    }

    [RelayCommand]
    private void OpenTableTab(string tableName)
    {
        var existing = Tabs.OfType<TableDataTabViewModel>().FirstOrDefault(t => t.Title == tableName);
        if (existing is not null)
        {
            SelectedTab = existing;
            return;
        }

        var tab = new TableDataTabViewModel(tableName, _workspaceService, _localizationService);
        Tabs.Add(tab);
        SelectedTab = tab;

        _ = tab.LoadDataAsync();

        ActivityFeed.AddActivity(string.Format(
            System.Globalization.CultureInfo.CurrentCulture,
            _localizationService.GetLocalizedString("VmMsgTableLoaded") ?? "Opened table {0}.",
            tableName));
    }

    [RelayCommand]
    private void OpenTableSchema(string tableName)
    {
        var tabTitle = $"{tableName} - Schema";
        var existing = Tabs.OfType<TableSchemaTabViewModel>().FirstOrDefault(t => t.Title == tabTitle);
        if (existing is not null)
        {
            SelectedTab = existing;
            return;
        }

        var tab = new TableSchemaTabViewModel(tableName, _workspaceService, _localizationService);
        Tabs.Add(tab);
        SelectedTab = tab;

        _ = tab.LoadSchemaAsync();

        ActivityFeed.AddActivity(string.Format(
            System.Globalization.CultureInfo.CurrentCulture,
            _localizationService.GetLocalizedString("VmMsgTableSchemaLoaded") ?? "Opened table schema {0}.",
            tableName));
    }

    [RelayCommand]
    private async Task NewSqlEditor()
    {
        var tab = new SqlEditorTabViewModel(_workspaceService, _localizationService, RefreshSchema);
        Tabs.Add(tab);
        SelectedTab = tab;
    }

    private async void RefreshSchema()
    {
        await TableBrowser.LoadSchemaAsync();
        var footerFormat = _localizationService.GetLocalizedString("VmFooterSummary") ?? "SQLite workspace ready with {0} schema objects.";
        ActivityFeed.UpdateFooterSummary(TableBrowser.Tables.Count, footerFormat);
    }

    [RelayCommand]
    private void CloseTab(TabViewModelBase? tab)
    {
        if (tab is null)
        {
            return;
        }

        var index = Tabs.IndexOf(tab);
        Tabs.Remove(tab);

        if (SelectedTab == tab)
        {
            var next = index < Tabs.Count ? Tabs[index] : Tabs.LastOrDefault();
            SelectedTab = next;
        }
    }

    [RelayCommand]
    private void ToggleLanguage()
    {
        _localizationService.ToggleLanguage();
        RefreshLocalizedText();
        StatusMessage = _localizationService.GetLocalizedString("VmStatusReady") ?? StatusMessage;
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        _themeService.ToggleTheme();
        ThemeIcon = _themeService.CurrentTheme == ThemeVariant.Dark ? "\U0001f319" : "\u2600\ufe0f";
    }

    private void RefreshLocalizedText()
    {
        ConnectionManager.RefreshLocalizedText();
        TableBrowser.RefreshLocalizedText();

        var footerFormat = _localizationService.GetLocalizedString("VmFooterSummary") ?? "SQLite workspace ready with {0} schema objects.";
        ActivityFeed.UpdateFooterSummary(TableBrowser.Tables.Count, footerFormat);
    }
}