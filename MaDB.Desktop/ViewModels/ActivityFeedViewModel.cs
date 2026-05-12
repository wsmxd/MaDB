using System;
using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MaDB.Desktop.ViewModels;

public partial class ActivityFeedViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _footerSummary = string.Empty;

    public ObservableCollection<ActivityEntryViewModel> ActivityFeed { get; } = [];

    public void AddActivity(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        ActivityFeed.Insert(0, new ActivityEntryViewModel(timestamp, message));

        while (ActivityFeed.Count > 6)
        {
            ActivityFeed.RemoveAt(ActivityFeed.Count - 1);
        }
    }

    public void UpdateFooterSummary(string summary)
    {
        FooterSummary = summary;
    }
}