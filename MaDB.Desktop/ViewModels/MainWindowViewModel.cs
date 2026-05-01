namespace MaDB.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public string Greeting { get; } = "Welcome to Avalonia!";
    public ViewModelBase ViewModel { get; } = new DemoViewModel();
}
