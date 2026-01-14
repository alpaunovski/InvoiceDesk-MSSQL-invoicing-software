using InvoiceDesk.Data;
using InvoiceDesk.Models;
using InvoiceDesk.Resources;
using Microsoft.EntityFrameworkCore;

namespace InvoiceDesk.Services;

public class CustomerService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ICompanyContext _companyContext;

    public CustomerService(IDbContextFactory<AppDbContext> dbFactory, ICompanyContext companyContext)
    {
        _dbFactory = dbFactory;
        _companyContext = companyContext;
    }

    public async Task<List<Customer>> GetCustomersAsync(string? search = null, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Customers.AsNoTracking()
            .Where(c => c.CompanyId == _companyContext.CurrentCompanyId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(c => EF.Functions.Like(c.Name, $"%{search}%") || EF.Functions.Like(c.Email, $"%{search}%"));
        }

        return await query.OrderBy(c => c.Name).ToListAsync(cancellationToken);
    }

    public async Task<Customer?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id && c.CompanyId == _companyContext.CurrentCompanyId, cancellationToken);
    }

    public async Task<Customer> SaveAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        customer.CompanyId = _companyContext.CurrentCompanyId;
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        if (customer.Id == 0)
        {
            db.Customers.Add(customer);
        }
        else
        {
            db.Customers.Update(customer);
        }

        await db.SaveChangesAsync(cancellationToken);
        return customer;
    }

    public async Task DeleteAsync(int customerId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.Customers.FirstOrDefaultAsync(c => c.Id == customerId && c.CompanyId == _companyContext.CurrentCompanyId, cancellationToken);
        if (entity == null)
        {
            return;
        }

        var hasInvoices = await db.Invoices.AnyAsync(i => i.CustomerId == customerId && i.CompanyId == _companyContext.CurrentCompanyId, cancellationToken);
        if (hasInvoices)
        {
            throw new InvalidOperationException(Strings.MessageCustomerDeleteHasInvoices);
        }

        db.Customers.Remove(entity);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException(Strings.MessageCustomerDeleteFailed, ex);
        }
    }
}
