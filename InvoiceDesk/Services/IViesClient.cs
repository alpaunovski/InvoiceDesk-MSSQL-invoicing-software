using System;
using System.Threading;
using System.Threading.Tasks;

namespace InvoiceDesk.Services;

public interface IViesClient
{
    bool IsSupportedCountry(string countryCode);

    Task<ViesCheckResult> CheckVatAsync(string countryCode, string vatNumber, CancellationToken cancellationToken = default);
}

public record ViesCheckResult(
    bool Success,
    bool IsValid,
    string? TraderName,
    string? TraderAddress,
    DateTime? RequestDate,
    string? FaultCode,
    string? FaultMessage)
{
    public static ViesCheckResult UnsupportedCountry(string countryCode) => new(false, false, null, null, null, "UNSUPPORTED_COUNTRY", $"VIES does not support country code {countryCode}");

    public static ViesCheckResult InvalidInput(string message) => new(false, false, null, null, null, "INVALID_INPUT", message);

    public static ViesCheckResult Failure(string code, string message) => new(false, false, null, null, null, code, message);

    public static ViesCheckResult Valid(bool isValid, string? traderName, string? traderAddress, DateTime? requestDate) => new(true, isValid, traderName, traderAddress, requestDate, null, null);
}
