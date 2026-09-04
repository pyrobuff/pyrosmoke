using System.Text.Json.Serialization;

namespace PyroSmoke.Models;

public sealed class PlayerSmokePreference
{
    [JsonPropertyName("mode")] public string? Mode { get; set; } = "team";
    [JsonPropertyName("color")] public string? Color { get; set; }
}

public sealed class PlayerOverride
{
    [JsonPropertyName("color")] public string Color { get; set; } = "255 0 255";
    [JsonPropertyName("locked")] public bool Locked { get; set; }
    [JsonPropertyName("access")] public List<string>? Access { get; set; } = [];
    [JsonIgnore] public CounterStrikeSharp.API.Modules.Utils.Vector? ParsedColor { get; set; }
}

public sealed class GroupDefault
{
    [JsonPropertyName("permission")] public string Permission { get; set; } = "";
    [JsonPropertyName("color")] public string Color { get; set; } = "";
}
