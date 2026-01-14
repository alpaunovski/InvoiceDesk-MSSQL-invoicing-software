namespace InvoiceDesk.Services;

public interface ICompanyContext
{
    int CurrentCompanyId { get; }
    event EventHandler<int>? CompanyChanged;
    Task SetCompanyAsync(int companyId, CancellationToken cancellationToken = default);
}
