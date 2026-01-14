using System.Collections.Generic;

namespace InvoiceDesk.Models;

public class Company
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string VatNumber { get; set; } = string.Empty;
    public string? Eik { get; set; }
    public string CountryCode { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string BankIban { get; set; } = string.Empty;
    public string BankBic { get; set; } = string.Empty;
    public string? InvoiceNumberPrefix { get; set; }
    public int NextInvoiceNumber { get; set; } = 1;
    public string? LogoPath { get; set; }

    public ICollection<Customer> Customers { get; set; } = new List<Customer>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
