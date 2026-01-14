using CommunityToolkit.Mvvm.ComponentModel;
using InvoiceDesk.Models;

namespace InvoiceDesk.ViewModels;

public partial class InvoiceLineViewModel : ObservableObject
{
    [ObservableProperty]
    private int id;

    [ObservableProperty]
    private int invoiceId;

    [ObservableProperty]
    private string description = string.Empty;

    [ObservableProperty]
    private decimal qty;

    [ObservableProperty]
    private decimal unitPrice;

    [ObservableProperty]
    private decimal taxRate;

    [ObservableProperty]
    private VatType vatType;

    [ObservableProperty]
    private decimal lineTotal;

    public InvoiceLine ToEntity(int companyId)
    {
        var normalizedTaxRate = TaxRate > 1m
            ? Math.Round(TaxRate / 100m, 4, MidpointRounding.AwayFromZero)
            : Math.Max(TaxRate, 0m);

        return new InvoiceLine
        {
            Id = Id,
            InvoiceId = InvoiceId,
            CompanyId = companyId,
            Description = Description,
            Qty = Qty,
            UnitPrice = UnitPrice,
            TaxRate = normalizedTaxRate,
            VatType = VatType,
            LineTotal = LineTotal
        };
    }

    public static InvoiceLineViewModel FromEntity(InvoiceLine line)
    {
        return new InvoiceLineViewModel
        {
            Id = line.Id,
            InvoiceId = line.InvoiceId,
            Description = line.Description,
            Qty = line.Qty,
            UnitPrice = line.UnitPrice,
            TaxRate = line.TaxRate,
            VatType = line.VatType,
            LineTotal = line.LineTotal
        };
    }
}
