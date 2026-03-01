using InvoiceDesk.Data;
using InvoiceDesk.Models;
using InvoiceDesk.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace InvoiceDesk.Services;

public class CustomerService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ICompanyContext _companyContext;
    private readonly IViesClient _viesClient;
    private readonly ILogger<CustomerService> _logger;

    public CustomerService(IDbContextFactory<AppDbContext> dbFactory, ICompanyContext companyContext, IViesClient viesClient, ILogger<CustomerService> logger)
    {
        _dbFactory = dbFactory;
        _companyContext = companyContext;
        _viesClient = viesClient;
        _logger = logger;
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
        NormalizeCustomerTaxData(customer);

        await TryAutoCheckVatAsync(customer, cancellationToken);

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

    private static void NormalizeCustomerTaxData(Customer customer)
    {
        customer.CountryCode = NormalizeCountryCode(customer.CountryCode);
        customer.VatNumber = NormalizeVatNumber(customer.VatNumber, customer.CountryCode);
    }

    private async Task TryAutoCheckVatAsync(Customer customer, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(customer.VatNumber) || string.IsNullOrWhiteSpace(customer.CountryCode))
        {
            return;
        }

        if (!_viesClient.IsSupportedCountry(customer.CountryCode))
        {
            return;
        }

        var result = await _viesClient.CheckVatAsync(customer.CountryCode, customer.VatNumber, cancellationToken);
        if (result.Success)
        {
            customer.IsVatRegistered = result.IsValid;

            if (string.IsNullOrWhiteSpace(customer.Address) && !string.IsNullOrWhiteSpace(result.TraderAddress))
            {
                customer.Address = result.TraderAddress;
            }
        }
        else
        {
            _logger.LogWarning("VIES check failed for {CountryCode}{VatNumber}: {FaultCode} {FaultMessage}", customer.CountryCode, customer.VatNumber, result.FaultCode, result.FaultMessage);
        }
    }

    private static string NormalizeCountryCode(string? countryCode)
    {
        return countryCode?.Trim().ToUpperInvariant() ?? string.Empty;
    }

    private static string NormalizeVatNumber(string? vatNumber, string countryCode)
    {
        if (string.IsNullOrWhiteSpace(vatNumber))
        {
            return string.Empty;
        }

        var cleaned = new string(vatNumber.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(countryCode) && cleaned.StartsWith(countryCode, StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned[countryCode.Length..];
        }

        return cleaned;
    }
}
