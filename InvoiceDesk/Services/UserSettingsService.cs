using System.IO;
using System.Text.Json;

namespace InvoiceDesk.Services;

public class UserSettings
{
    public string Culture { get; set; } = "en";
    public int? CompanyId { get; set; }
    public double WindowWidth { get; set; } = 1200;
    public double WindowHeight { get; set; } = 800;
}

public class UserSettingsService
{
    private readonly string _settingsPath;
    private UserSettings? _cached;

    public UserSettingsService()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "InvoiceDesk");
        Directory.CreateDirectory(folder);
        _settingsPath = Path.Combine(folder, "user-settings.json");
    }

    public async Task<UserSettings> LoadAsync()
    {
        if (_cached != null)
        {
            return _cached;
        }

        if (!File.Exists(_settingsPath))
        {
            _cached = new UserSettings();
            return _cached;
        }

        await using var stream = File.OpenRead(_settingsPath);
        _cached = await JsonSerializer.DeserializeAsync<UserSettings>(stream) ?? new UserSettings();
        return _cached;
    }

    public async Task SaveAsync(UserSettings settings)
    {
        _cached = settings;
        // Allow other readers while writing to reduce IO contention (e.g., multiple app instances).
        await using var stream = new FileStream(
            _settingsPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.Read);

        await JsonSerializer.SerializeAsync(stream, settings, new JsonSerializerOptions { WriteIndented = true });
    }
}
