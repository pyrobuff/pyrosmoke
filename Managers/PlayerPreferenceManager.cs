using System.Text.Json;
using Microsoft.Extensions.Logging;
using PyroSmoke.Models;

namespace PyroSmoke.Managers;

public sealed class PlayerPreferenceManager
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    private readonly Dictionary<string, PlayerSmokePreference> _preferences = new(StringComparer.Ordinal);
    private readonly string _path;
    private readonly ILogger _logger;

    public PlayerPreferenceManager(string moduleDirectory, ILogger logger)
    {
        _path = Path.Combine(moduleDirectory, "Data", "players.json");
        _logger = logger;
    }

    public void Load()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        if (!File.Exists(_path)) { Save(); return; }
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(_path));
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new JsonException("The preference file root must be a JSON object.");

            _preferences.Clear();
            var invalidEntries = 0;
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (string.IsNullOrWhiteSpace(property.Name))
                {
                    invalidEntries++;
                    continue;
                }

                try
                {
                    var preference = property.Value.Deserialize<PlayerSmokePreference>(JsonOptions);
                    if (preference is null)
                    {
                        invalidEntries++;
                        continue;
                    }

                    preference.Mode = NormalizeMode(preference.Mode);
                    if (preference.Mode == "custom" && string.IsNullOrWhiteSpace(preference.Color))
                        preference.Mode = "team";
                    _preferences[property.Name.Trim()] = preference;
                }
                catch (JsonException)
                {
                    invalidEntries++;
                }
            }

            if (invalidEntries > 0)
                _logger.LogWarning("[PyroSmoke] players.json contained {Count} invalid preference entries; valid entries were loaded.", invalidEntries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PyroSmoke] players.json okunamadı; tercihler boş cache ile başlatıldı. Bozuk dosya değiştirilmedi.");
            _preferences.Clear();
        }
    }

    public PlayerSmokePreference Get(string steamId) =>
        _preferences.TryGetValue(steamId, out var preference) ? preference : new PlayerSmokePreference();

    public bool Set(string steamId, PlayerSmokePreference preference)
    {
        _preferences[steamId] = preference;
        return Save();
    }

    private bool Save()
    {
        var temp = _path + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(_preferences, JsonOptions));
            File.Move(temp, _path, true);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PyroSmoke] Oyuncu tercihleri kaydedilemedi.");
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
            return false;
        }
    }

    private static string NormalizeMode(string? mode) => mode?.Trim().ToLowerInvariant() switch
    {
        "custom" => "custom",
        "random" => "random",
        _ => "team"
    };
}
