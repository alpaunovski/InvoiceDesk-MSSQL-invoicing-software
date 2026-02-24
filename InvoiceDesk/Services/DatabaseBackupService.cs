using System.Data;
using System.IO;
using System.IO.Compression;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace InvoiceDesk.Services;

/// <summary>
/// Creates and restores compressed SQL Server backups for the InvoiceDesk database.
/// </summary>
public class DatabaseBackupService
{
    private readonly string _connectionString;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseBackupService> _logger;

    public DatabaseBackupService(IConfiguration configuration, ILogger<DatabaseBackupService> logger)
    {
        _configuration = configuration;
        _connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Missing connection string");
        _logger = logger;
    }

    public string GetDefaultBackupDirectory()
    {
        return ResolveBackupDirectory();
    }

    public async Task<string> BackupToZipAsync(string zipPath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(zipPath);
        Directory.CreateDirectory(Path.GetDirectoryName(zipPath)!);

        var databaseName = GetDatabaseName();
        var workDir = ResolveBackupDirectory();
        var tempBak = Path.Combine(workDir, $"{databaseName}-{DateTime.UtcNow:yyyyMMddHHmmssfff}.bak");

        try
        {
            _logger.LogInformation("Starting database backup for {Database} to {Path}", databaseName, zipPath);
            await CreateBackupAsync(databaseName, tempBak, cancellationToken);

            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }

            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                archive.CreateEntryFromFile(tempBak, Path.GetFileName(tempBak), CompressionLevel.Optimal);
            }

            _logger.LogInformation("Database backup complete for {Database} at {Path}", databaseName, zipPath);
            return zipPath;
        }
        finally
        {
            SafeDelete(tempBak);
        }
    }

    public async Task RestoreFromZipAsync(string zipPath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(zipPath);
        if (!File.Exists(zipPath))
        {
            throw new FileNotFoundException("Backup zip not found", zipPath);
        }

        var databaseName = GetDatabaseName();
        var workDir = ResolveBackupDirectory();
        var tempBak = Path.Combine(workDir, $"{databaseName}-restore-{Guid.NewGuid():N}.bak");

        try
        {
            _logger.LogInformation("Restoring database {Database} from {Path}", databaseName, zipPath);
            await ExtractBakAsync(zipPath, tempBak, cancellationToken);
            await RestoreBackupAsync(databaseName, tempBak, cancellationToken);
            _logger.LogInformation("Database restore complete for {Database} from {Path}", databaseName, zipPath);
        }
        finally
        {
            SafeDelete(tempBak);
        }
    }

    private async Task CreateBackupAsync(string databaseName, string backupPath, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(BuildMasterConnectionString());
        await connection.OpenAsync(cancellationToken);

        var supportsCompression = await SupportsBackupCompressionAsync(connection, cancellationToken);
        var compressionClause = supportsCompression ? ", COMPRESSION" : string.Empty;
        var commandText = $"BACKUP DATABASE [{databaseName}] TO DISK=@path WITH INIT, COPY_ONLY, FORMAT{compressionClause}";
        await ExecuteNonQueryAsync(connection, commandText, backupPath, cancellationToken);
    }

    private async Task RestoreBackupAsync(string databaseName, string backupPath, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(BuildMasterConnectionString());
        await connection.OpenAsync(cancellationToken);

        await EnsureBackupValidAsync(connection, backupPath, databaseName, cancellationToken);

        var setSingleUser = $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE";
        var restore = $"RESTORE DATABASE [{databaseName}] FROM DISK=@path WITH REPLACE, RECOVERY";
        var setMultiUser = $"ALTER DATABASE [{databaseName}] SET MULTI_USER";

        await ExecuteNonQueryAsync(connection, setSingleUser, null, cancellationToken);
        try
        {
            await ExecuteNonQueryAsync(connection, restore, backupPath, cancellationToken);
        }
        finally
        {
            try
            {
                await ExecuteNonQueryAsync(connection, setMultiUser, null, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to set database {Database} back to MULTI_USER", databaseName);
            }
        }
    }

    private static async Task ExecuteNonQueryAsync(SqlConnection connection, string commandText, string? backupPath, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        command.CommandText = commandText;
        if (!string.IsNullOrWhiteSpace(backupPath))
        {
            command.Parameters.AddWithValue("@path", backupPath);
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<bool> SupportsBackupCompressionAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT CAST(SERVERPROPERTY('EngineEdition') AS INT)";
        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is int edition)
        {
            // EngineEdition 4 = Express (no compression support)
            return edition != 4;
        }
        return false;
    }

    private async Task EnsureBackupValidAsync(SqlConnection connection, string backupPath, string expectedDatabase, CancellationToken cancellationToken)
    {
        var info = new FileInfo(backupPath);
        if (!info.Exists || info.Length < 1024)
        {
            throw new InvalidOperationException($"Backup file {backupPath} is missing or too small.");
        }

        await using (var headerCmd = connection.CreateCommand())
        {
            headerCmd.CommandText = "RESTORE HEADERONLY FROM DISK=@path";
            headerCmd.Parameters.AddWithValue("@path", backupPath);
            await using var reader = await headerCmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("Cannot read backup header; backup may be corrupted.");
            }

            var sourceDb = reader["DatabaseName"] as string ?? string.Empty;
            if (!string.Equals(sourceDb, expectedDatabase, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Backup is for database '{sourceDb}', expected '{expectedDatabase}'. Aborting restore.");
            }
        }

        await using (var verifyCmd = connection.CreateCommand())
        {
            verifyCmd.CommandText = "RESTORE VERIFYONLY FROM DISK=@path";
            verifyCmd.Parameters.AddWithValue("@path", backupPath);
            await verifyCmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task ExtractBakAsync(string zipPath, string bakDestination, CancellationToken cancellationToken)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        var entry = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith(".bak", StringComparison.OrdinalIgnoreCase))
                    ?? archive.Entries.FirstOrDefault();

        if (entry == null)
        {
            throw new InvalidOperationException("Backup zip does not contain a .bak file");
        }

        await using var entryStream = entry.Open();
        await using var targetStream = File.Create(bakDestination);
        await entryStream.CopyToAsync(targetStream, cancellationToken);
    }

    private string BuildMasterConnectionString()
    {
        var builder = new SqlConnectionStringBuilder(_connectionString)
        {
            InitialCatalog = "master"
        };
        return builder.ConnectionString;
    }

    private string GetDatabaseName()
    {
        var builder = new SqlConnectionStringBuilder(_connectionString);
        if (string.IsNullOrWhiteSpace(builder.InitialCatalog))
        {
            throw new InvalidOperationException("Connection string must specify a database name (Initial Catalog)");
        }

        return builder.InitialCatalog;
    }

    private static void SafeDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    private string ResolveBackupDirectory()
    {
        var configured = _configuration.GetSection("Backup")?["Directory"];
        var path = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\", "SqlBackups", "InvoiceDesk")
            : Environment.ExpandEnvironmentVariables(configured);

        Directory.CreateDirectory(path);
        return path;
    }
}
