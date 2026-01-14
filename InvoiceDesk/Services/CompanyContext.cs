using InvoiceDesk.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InvoiceDesk.Services;

public class CompanyContext : ICompanyContext
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ILogger<CompanyContext> _logger;

    public CompanyContext(IDbContextFactory<AppDbContext> dbFactory, ILogger<CompanyContext> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public int CurrentCompanyId { get; private set; }

    public event EventHandler<int>? CompanyChanged;

    public async Task SetCompanyAsync(int companyId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var exists = await db.Companies.AnyAsync(c => c.Id == companyId, cancellationToken);
        if (!exists)
        {
            throw new InvalidOperationException($"Company {companyId} not found");
        }

        if (CurrentCompanyId == companyId)
        {
            return;
        }

        CurrentCompanyId = companyId;
        _logger.LogInformation("Switched company context to {CompanyId}", companyId);
        // Notify listeners (view models, services) so they can reload scoped data.
        CompanyChanged?.Invoke(this, companyId);
    }
}
