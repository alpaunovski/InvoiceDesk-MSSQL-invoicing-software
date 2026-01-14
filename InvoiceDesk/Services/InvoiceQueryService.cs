using InvoiceDesk.Data;
using InvoiceDesk.Models;
using Microsoft.EntityFrameworkCore;

namespace InvoiceDesk.Services;

public class InvoiceQueryService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ICompanyContext _companyContext;

    public InvoiceQueryService(IDbContextFactory<AppDbContext> dbFactory, ICompanyContext companyContext)
    {
        _dbFactory = dbFactory;
        _companyContext = companyContext;
    }

    public async Task<List<Invoice>> SearchAsync(string? search, DateTime? from, DateTime? to, int? customerId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        // Always scope queries to the active company to avoid cross-tenant leakage.
        var query = db.Invoices.AsNoTracking()
            .Include(i => i.Customer)
            .Where(i => i.CompanyId == _companyContext.CurrentCompanyId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            // Simple text search over invoice number and customer snapshot.
            query = query.Where(i => EF.Functions.Like(i.InvoiceNumber, $"%{search}%") || EF.Functions.Like(i.CustomerNameSnapshot, $"%{search}%"));
        }

        if (from.HasValue)
        {
            query = query.Where(i => i.IssueDate >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(i => i.IssueDate <= to.Value);
        }

        if (customerId.HasValue)
        {
            query = query.Where(i => i.CustomerId == customerId.Value);
        }

        return await query.OrderByDescending(i => i.IssueDate).ThenByDescending(i => i.Id).ToListAsync(cancellationToken);
    }

    public async Task<Invoice?> GetInvoiceWithLinesAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Invoices
            .Include(i => i.Lines)
            .Include(i => i.Customer)
            .Include(i => i.Company)
            .FirstOrDefaultAsync(i => i.Id == id && i.CompanyId == _companyContext.CurrentCompanyId, cancellationToken);
    }
}
