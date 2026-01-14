using System.Globalization;
using InvoiceDesk.Helpers;
using InvoiceDesk.Resources;

namespace InvoiceDesk.Services;

public class LanguageService : ILanguageService
{
    private readonly LocalizedStrings _localizedStrings;
    private readonly UserSettingsService _settingsService;

    public LanguageService(LocalizedStrings localizedStrings, UserSettingsService settingsService)
    {
        _localizedStrings = localizedStrings;
        _settingsService = settingsService;
        CurrentCulture = CultureInfo.CurrentUICulture;
    }

    public CultureInfo CurrentCulture { get; private set; }

    public event EventHandler<CultureInfo>? CultureChanged;

    public async Task SetCultureAsync(string cultureCode)
    {
        var culture = new CultureInfo(cultureCode);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        Strings.Culture = culture;
        CurrentCulture = culture;
        _localizedStrings.RaiseCultureChanged();
        CultureChanged?.Invoke(this, culture);
        var settings = await _settingsService.LoadAsync();
        settings.Culture = cultureCode;
        await _settingsService.SaveAsync(settings);
    }

    public LocalizedStrings Localizer => _localizedStrings;
}
