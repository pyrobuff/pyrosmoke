using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;
using PyroSmoke.Models;

namespace PyroSmoke.Configuration;

public sealed class DefaultColors
{
    [JsonPropertyName("t")] public string T { get; set; } = "237 163 56";
    [JsonPropertyName("ct")] public string Ct { get; set; } = "104 163 229";
    [JsonPropertyName("other")] public string Other { get; set; } = "0 255 0";
}

public sealed class PyroSmokeConfig : BasePluginConfig
{
    [JsonPropertyName("enabled")] public bool Enabled { get; set; } = true;
    [JsonPropertyName("command")] public string Command { get; set; } = "smoke";
    [JsonPropertyName("debug")] public bool Debug { get; set; }
    [JsonPropertyName("default_colors")] public DefaultColors DefaultColors { get; set; } = new();
    [JsonPropertyName("random_enabled")] public bool RandomEnabled { get; set; } = true;
    [JsonPropertyName("random_permission")] public string RandomPermission { get; set; } = "";
    [JsonPropertyName("colors")] public Dictionary<string, SmokeColorDefinition> Colors { get; set; } = DefaultPalette();
    [JsonPropertyName("group_defaults")] public List<GroupDefault> GroupDefaults { get; set; } = [];
    [JsonPropertyName("player_overrides")] public Dictionary<string, PlayerOverride> PlayerOverrides { get; set; } = [];

    private static Dictionary<string, SmokeColorDefinition> DefaultPalette() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["red"] = Color("Kırmızı", "255 0 0"), ["blue"] = Color("Mavi", "0 100 255"),
        ["green"] = Color("Yeşil", "0 255 0"), ["purple"] = Color("Mor", "170 0 255"),
        ["pink"] = Color("Pembe", "255 70 180"), ["orange"] = Color("Turuncu", "255 120 0"),
        ["cyan"] = Color("Cyan", "0 255 255"), ["white"] = Color("Beyaz", "255 255 255", "@css/admin"),
        ["yellow"] = Color("Sarı", "255 220 0"), ["lime"] = Color("Lime", "100 255 0")
    };

    private static SmokeColorDefinition Color(string name, string rgb, string permission = "") =>
        new() { Name = name, Rgb = rgb, Permission = permission };
}
