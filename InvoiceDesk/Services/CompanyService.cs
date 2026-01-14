using InvoiceDesk.Data;
using InvoiceDesk.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using InvoiceDesk.Resources;

namespace InvoiceDesk.Services;

public class CompanyService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public CompanyService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<List<Company>> GetCompaniesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Companies.AsNoTracking().OrderBy(c => c.Name).ToListAsync(cancellationToken);
    }

    public async Task<Company?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Companies.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<Company> SaveAsync(Company company, CancellationToken cancellationToken = default)
    {
        Normalize(company);
        Validate(company);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        if (company.Id == 0)
        {
            db.Companies.Add(company);
        }
        else
        {
            db.Companies.Update(company);
        }

        await db.SaveChangesAsync(cancellationToken);
        return company;
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.Companies.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (entity == null)
        {
            return;
        }

        var hasIssuedInvoices = await db.Invoices
            .AnyAsync(i => i.CompanyId == id && i.Status != InvoiceStatus.Draft, cancellationToken);
        if (hasIssuedInvoices)
        {
            throw new InvalidOperationException(Strings.MessageCompanyDeleteIssued);
        }

        // Delete dependent entities explicitly to avoid FK restrictions on customers with invoices.
        var invoices = await db.Invoices.Where(i => i.CompanyId == id).ToListAsync(cancellationToken);
        if (invoices.Count > 0)
        {
            db.Invoices.RemoveRange(invoices);
        }

        var customers = await db.Customers.Where(c => c.CompanyId == id).ToListAsync(cancellationToken);
        if (customers.Count > 0)
        {
            db.Customers.RemoveRange(customers);
        }

        db.Companies.Remove(entity);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException(Strings.MessageCompanyDeleteFailed, ex);
        }
    }

    private static void Normalize(Company company)
    {
        company.Name = company.Name?.Trim() ?? string.Empty;
        company.VatNumber = company.VatNumber?.Trim() ?? string.Empty;
        company.Eik = string.IsNullOrWhiteSpace(company.Eik) ? null : company.Eik.Trim();
        company.CountryCode = company.CountryCode?.Trim() ?? string.Empty;
        company.Address = company.Address?.Trim() ?? string.Empty;
        company.BankIban = company.BankIban?.Trim() ?? string.Empty;
        company.BankBic = company.BankBic?.Trim() ?? string.Empty;
        company.InvoiceNumberPrefix = string.IsNullOrWhiteSpace(company.InvoiceNumberPrefix)
            ? null
            : company.InvoiceNumberPrefix.Trim();
        company.LogoPath = string.IsNullOrWhiteSpace(company.LogoPath) ? null : company.LogoPath.Trim();
    }

    private static void Validate(Company company)
    {
        var isBg = company.CountryCode.Equals("BG", StringComparison.OrdinalIgnoreCase);
        if (isBg)
        {
            var eik = company.Eik ?? string.Empty;
            if (eik.Length == 0)
            {
                throw new InvalidOperationException("ЕИК е задължителен за български фирми.");
            }

            if (!(eik.Length == 9 || eik.Length == 13))
            {
                throw new InvalidOperationException("ЕИК трябва да има 9 или 13 цифри.");
            }

            if (!eik.All(char.IsDigit))
            {
                throw new InvalidOperationException("ЕИК трябва да съдържа само цифри.");
            }
        }

        if (!string.IsNullOrWhiteSpace(company.Eik) && !company.Eik.All(char.IsDigit))
        {
            throw new InvalidOperationException("ЕИК трябва да съдържа само цифри.");
        }
        if (!string.IsNullOrWhiteSpace(company.Eik) && company.Eik.Length > 13)
        {
            throw new InvalidOperationException("ЕИК трябва да има до 13 цифри.");
        }
    }
}
