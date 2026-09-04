using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Modules.Utils;

namespace PyroSmoke.Models;

public sealed class SmokeColorDefinition
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("rgb")] public string Rgb { get; set; } = "0 255 0";
    [JsonPropertyName("permission")] public string Permission { get; set; } = "";
    [JsonIgnore] public Vector ParsedColor { get; set; } = new(0, 255, 0);
}
