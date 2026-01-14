namespace InvoiceDesk.Models;

public class InvoiceLine
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int InvoiceId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Qty { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxRate { get; set; }
    public VatType VatType { get; set; }
    public decimal LineTotal { get; set; }

    public Invoice? Invoice { get; set; }
}
