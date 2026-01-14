using InvoiceDesk.Models;

namespace InvoiceDesk.Helpers;

public class VatTypeOption
{
    public VatType Value { get; set; }
    public string Label { get; set; } = string.Empty;
}
