using System;

namespace InvoiceDesk.Helpers;

/// <summary>
/// Centralizes legally mandated currency conversion for Bulgaria's euro transition.
/// BGN stays the accounting base; EUR is derived display-only using the fixed rate.
/// EUR values are display-only and legally required during the euro transition period.
/// Conversion rate is fixed by law: 1 EUR = 1.95583 BGN.
/// Do NOT modify without legal review.
/// </summary>


public static class CurrencyHelper
{
    // Fixed conversion rate set by law: 1 EUR = 1.95583 BGN.
    public const decimal EurToBgnRate = 1.95583m;

    // Legal note required on invoices when dual display is shown.
    public const string DualCurrencyLegalNote = "Сумите в евро са изчислени по фиксиран курс 1 EUR = 1.95583 лв.";

    public static decimal ConvertBgnToEur(decimal amountInBgn)
    {
        return Math.Round(amountInBgn / EurToBgnRate, 2, MidpointRounding.AwayFromZero);
    }

    public static bool IsBgn(string? currencyCode)
    {
        return string.Equals(currencyCode?.Trim(), "BGN", StringComparison.OrdinalIgnoreCase);
    }

    public static string NormalizeCurrencyOrDefault(string? currencyCode)
    {
        return string.IsNullOrWhiteSpace(currencyCode)
            ? "BGN"
            : currencyCode.Trim().ToUpperInvariant();
    }

    public static bool ShouldShowDualCurrency(CurrencyDisplayOptions options, string? currencyCode)
    {
        if (options == null)
        {
            return false;
        }

        if (options.EurOnlyMode)
        {
            return false;
        }

        return options.DualCurrencyEnabled && IsBgn(currencyCode);
    }
}
