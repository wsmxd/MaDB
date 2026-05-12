namespace MaDB.Desktop.Services;

public interface ILocalizationService
{
    string CurrentLanguage { get; }
    
    void ToggleLanguage();
    
    string? GetLocalizedString(string key);
    
    string FormatLocalizedString(string key, params object[] args);
}