using MySqlConnector;

string connStr = args.Length > 0
    ? args[0]
    : "server=localhost;port=3306;database=invoicedesk;user=root;password=l3tm3n0w;TreatTinyAsBoolean=false;SslMode=None";

await using var conn = new MySqlConnection(connStr);
await conn.OpenAsync();

const string columnSql = "SELECT column_name, data_type, is_nullable, character_maximum_length FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'Companies' AND column_name = 'Eik';";
await using var columnCmd = new MySqlCommand(columnSql, conn);
await using var columnReader = await columnCmd.ExecuteReaderAsync();

if (!columnReader.HasRows)
{
    Console.WriteLine("Eik column NOT found in Companies table for database: " + conn.Database);
}
else
{
    while (await columnReader.ReadAsync())
    {
        var name = columnReader.GetString(0);
        var type = columnReader.GetString(1);
        var nullable = columnReader.GetString(2);
        var len = columnReader.IsDBNull(3) ? "" : columnReader.GetInt32(3).ToString();
        Console.WriteLine($"Found column: {name}, type: {type}, nullable: {nullable}, len: {len}");
    }
}

await columnReader.CloseAsync();

const string historySql = "SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId;";
await using var historyCmd = new MySqlCommand(historySql, conn);
await using var historyReader = await historyCmd.ExecuteReaderAsync();
Console.WriteLine("Applied migrations:");
while (await historyReader.ReadAsync())
{
    Console.WriteLine(" - " + historyReader.GetString(0));
}
