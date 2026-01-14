namespace InvoiceDesk.Models;

public enum VatType
{
    Domestic = 0,
    IntraEuReverseCharge = 1,
    ExportOutsideEu = 2,
    VatExempt = 3
}
