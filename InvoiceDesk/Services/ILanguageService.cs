using System.Globalization;

namespace InvoiceDesk.Services;

public interface ILanguageService
{
    CultureInfo CurrentCulture { get; }
    event EventHandler<CultureInfo>? CultureChanged;
    Task SetCultureAsync(string cultureCode);
}
