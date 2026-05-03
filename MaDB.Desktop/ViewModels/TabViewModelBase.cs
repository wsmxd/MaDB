using CommunityToolkit.Mvvm.ComponentModel;

namespace MaDB.Desktop.ViewModels;

public abstract partial class TabViewModelBase : ViewModelBase
{
    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _icon = string.Empty;

    [ObservableProperty]
    private bool _isSelected;

    public void RefreshSelectionState()
    {
        OnPropertyChanged(nameof(IsSelected));
    }
}