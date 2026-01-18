using InvoiceDesk.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;
using Microsoft.Extensions.Logging;

namespace InvoiceDesk.Data;

public class AppDbInitializer
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly ILogger<AppDbInitializer> _logger;

    public AppDbInitializer(IDbContextFactory<AppDbContext> dbContextFactory, ILogger<AppDbInitializer> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    /// <summary>
    /// Ensures the database exists, applies migrations, repairs missing data, and seeds a default company.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureDatabaseExistsAsync(db, cancellationToken);
        // Apply pending migrations so the app can run without manual database setup.
        await db.Database.MigrateAsync(cancellationToken);

        // Backfill missing invoice numbers to satisfy the unique index.
        await FixMissingInvoiceNumbersAsync(db, cancellationToken);

        if (!await db.Companies.AnyAsync(cancellationToken))
        {
            // Seed a minimal company to let the app start end-to-end on first run.
            var company = new Company
            {
                Name = "Default Company",
                VatNumber = "BG000000000",
                Eik = "000000000",
                CountryCode = "BG",
                Address = "",
                BankIban = "",
                BankBic = "",
                InvoiceNumberPrefix = "INV",
                NextInvoiceNumber = 1
            };
            db.Companies.Add(company);
            await db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Seeded initial company");
        }
    }

    private async Task FixMissingInvoiceNumbersAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var missing = await db.Invoices
            .Where(i => string.IsNullOrWhiteSpace(i.InvoiceNumber))
            .ToListAsync(cancellationToken);

        if (missing.Count == 0)
        {
            return;
        }

        foreach (var invoice in missing)
        {
            // Use a timestamped prefix with company/invoice IDs to avoid collisions when backfilling.
            invoice.InvoiceNumber = GenerateRepairNumber(invoice.CompanyId, invoice.Id);
        }

        await db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Backfilled {Count} invoices missing numbers", missing.Count);
    }

    /// <summary>
    /// Creates the target database on the SQL Server instance if it does not yet exist.
    /// </summary>
    private async Task EnsureDatabaseExistsAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var connectionString = db.Database.GetConnectionString() ?? throw new InvalidOperationException("Missing database connection string");
        var builder = new SqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.InitialCatalog))
        {
            throw new InvalidOperationException("Connection string must specify a Database/Initial Catalog name.");
        }

        var databaseName = builder.InitialCatalog;
        var masterBuilder = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = "master"
        };

        await using var connection = new SqlConnection(masterBuilder.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = """
IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = @name)
BEGIN
    DECLARE @cmd nvarchar(max) = 'CREATE DATABASE [' + @name + ']';
    EXEC (@cmd);
END
""";

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add(new SqlParameter("@name", SqlDbType.NVarChar, 128) { Value = databaseName });
        await command.ExecuteNonQueryAsync(cancellationToken);
        _logger.LogInformation("Ensured database {DatabaseName} exists", databaseName);
    }

    /// <summary>
    /// Generates a deterministic-ish repair number to avoid collisions when backfilling missing invoice numbers.
    /// </summary>
    private static string GenerateRepairNumber(int companyId, int invoiceId)
    {
        var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
        return $"DRAFTFIX-{companyId}-{invoiceId}-{stamp}";
    }
}
