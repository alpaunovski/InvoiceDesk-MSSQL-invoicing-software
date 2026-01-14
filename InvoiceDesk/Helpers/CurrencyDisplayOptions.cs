namespace InvoiceDesk.Helpers;

/// <summary>
/// Controls how currencies are shown during and after the euro transition.
/// DualCurrencyEnabled keeps EUR as a secondary, display-only currency alongside BGN.
/// EurOnlyMode is reserved for the future EUR-only switchover; leave false until policy changes.
/// </summary>
public class CurrencyDisplayOptions
{
    public bool DualCurrencyEnabled { get; set; } = true;

    // Future-proof flag for the post-transition EUR-only mode; keep false until legally required.
    public bool EurOnlyMode { get; set; } = false;
}
