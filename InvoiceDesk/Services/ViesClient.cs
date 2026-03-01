using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace InvoiceDesk.Services;

public class ViesClient : IViesClient
{
    private static readonly Uri ServiceUri = new("https://ec.europa.eu/taxation_customs/vies/services/checkVatService");

    private static readonly HashSet<string> SupportedCountries = new(StringComparer.OrdinalIgnoreCase)
    {
        "AT", "BE", "BG", "CY", "CZ", "DE", "DK", "EE", "EL", "ES", "FI", "FR", "HR", "HU", "IE", "IT", "LT", "LU", "LV", "MT", "NL", "PL", "PT", "RO", "SE", "SI", "SK", "XI"
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<ViesClient> _logger;

    public ViesClient(HttpClient httpClient, ILogger<ViesClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _httpClient.Timeout = TimeSpan.FromSeconds(15);
        if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("InvoiceDesk/1.0");
        }
    }

    public bool IsSupportedCountry(string countryCode)
    {
        var normalized = NormalizeCountryCode(countryCode);
        return SupportedCountries.Contains(normalized);
    }

    public async Task<ViesCheckResult> CheckVatAsync(string countryCode, string vatNumber, CancellationToken cancellationToken = default)
    {
        var normalizedCountry = NormalizeCountryCode(countryCode);
        var normalizedVat = NormalizeVatNumber(vatNumber, normalizedCountry);

        if (string.IsNullOrWhiteSpace(normalizedCountry) || string.IsNullOrWhiteSpace(normalizedVat))
        {
            return ViesCheckResult.InvalidInput("Country code and VAT number are required for VIES checks.");
        }

        if (!IsSupportedCountry(normalizedCountry))
        {
            return ViesCheckResult.UnsupportedCountry(normalizedCountry);
        }

        var requestBody = BuildRequestBody(normalizedCountry, normalizedVat);
        using var request = new HttpRequestMessage(HttpMethod.Post, ServiceUri)
        {
            Content = new StringContent(requestBody, Encoding.UTF8, "text/xml")
        };

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync();
            var parsed = ParseResponse(content);
            if (parsed.Success)
            {
                return parsed;
            }

            var statusMessage = $"VIES responded with status {(int)response.StatusCode} ({response.ReasonPhrase})";
            return ViesCheckResult.Failure(parsed.FaultCode ?? response.StatusCode.ToString(), parsed.FaultMessage ?? statusMessage);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "VIES check failed for {Country}{Vat}", normalizedCountry, normalizedVat);
            return ViesCheckResult.Failure("EXCEPTION", ex.Message);
        }
    }

    private static string BuildRequestBody(string countryCode, string vatNumber)
    {
        return $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\">\n  <soap:Body>\n    <checkVat xmlns=\"urn:ec.europa.eu:taxud:vies:services:checkVat:types\">\n      <countryCode>{countryCode}</countryCode>\n      <vatNumber>{vatNumber}</vatNumber>\n    </checkVat>\n  </soap:Body>\n</soap:Envelope>";
    }

    private static ViesCheckResult ParseResponse(string xml)
    {
        try
        {
            var doc = XDocument.Parse(xml);
            var soapNs = XNamespace.Get("http://schemas.xmlsoap.org/soap/envelope/");
            var viesNs = XNamespace.Get("urn:ec.europa.eu:taxud:vies:services:checkVat:types");

            var fault = doc.Descendants(soapNs + "Fault").FirstOrDefault();
            if (fault != null)
            {
                var faultCode = fault.Element("faultcode")?.Value;
                var faultString = fault.Element("faultstring")?.Value;
                return ViesCheckResult.Failure(faultCode ?? "SOAP_FAULT", faultString ?? "VIES returned a SOAP fault.");
            }

            var response = doc.Descendants(viesNs + "checkVatResponse").FirstOrDefault();
            if (response == null)
            {
                return ViesCheckResult.Failure("PARSE_ERROR", "Could not find VIES checkVatResponse.");
            }

            var validValue = response.Element(viesNs + "valid")?.Value;
            var requestDateText = response.Element(viesNs + "requestDate")?.Value;
            var traderName = CleanValue(response.Element(viesNs + "name")?.Value);
            var traderAddress = CleanAddress(response.Element(viesNs + "address")?.Value);

            DateTime? requestDate = null;
            if (DateTimeOffset.TryParse(requestDateText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces, out var parsedDate))
            {
                requestDate = parsedDate.UtcDateTime;
            }

            var isValid = string.Equals(validValue, "true", StringComparison.OrdinalIgnoreCase);
            return ViesCheckResult.Valid(isValid, traderName, traderAddress, requestDate);
        }
        catch (Exception ex)
        {
            return ViesCheckResult.Failure("PARSE_EXCEPTION", ex.Message);
        }
    }

    private static string NormalizeCountryCode(string countryCode) => countryCode?.Trim().ToUpperInvariant() ?? string.Empty;

    private static string NormalizeVatNumber(string vatNumber, string countryCode)
    {
        if (string.IsNullOrWhiteSpace(vatNumber))
        {
            return string.Empty;
        }

        var cleaned = new string(vatNumber.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(countryCode) && cleaned.StartsWith(countryCode, StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned[countryCode.Length..];
        }

        return cleaned;
    }

    private static string CleanValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Equals("---", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return value.Trim();
    }

    private static string CleanAddress(string? address)
    {
        if (string.IsNullOrWhiteSpace(address) || address.Equals("---", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var parts = address
            .Replace("\r", string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return string.Join(", ", parts);
    }
}
