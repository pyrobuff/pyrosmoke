using System.Globalization;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using PyroSmoke.Configuration;
using PyroSmoke.Managers;
using PyroSmoke.Models;
using T3MenuSharedApi;

namespace PyroSmoke.Plugin;

[MinimumApiVersion(373)]
public sealed class PyroSmoke : BasePlugin, IPluginConfig<PyroSmokeConfig>
{
    public override string ModuleName => "PyroSmoke";
    public override string ModuleAuthor => "pyroBuff";
    public override string ModuleDescription => "Player-based colored smoke grenade system for CS2.";
    public override string ModuleVersion => "1.0.0";

    public PyroSmokeConfig Config { get; set; } = new();
    public Vector TeamT { get; private set; } = new(237, 163, 56);
    public Vector TeamCt { get; private set; } = new(104, 163, 229);
    public Vector TeamOther { get; private set; } = new(0, 255, 0);
    private PlayerPreferenceManager? _preferences;
    private SmokeManager? _smokes;
    private IT3MenuManager? _menuManager;
    private string _registeredCommand = "css_smoke";

    public void OnConfigParsed(PyroSmokeConfig config)
    {
        config.Colors ??= [];
        config.PlayerOverrides ??= [];
        config.GroupDefaults ??= [];
        config.DefaultColors ??= new DefaultColors();
        config.Command = NormalizeCommand(config.Command);

        TeamT = ParseRgb(config.DefaultColors.T, "default T color", new Vector(237, 163, 56));
        TeamCt = ParseRgb(config.DefaultColors.Ct, "default CT color", new Vector(104, 163, 229));
        TeamOther = ParseRgb(config.DefaultColors.Other, "default other color", new Vector(0, 255, 0));

        var normalized = new Dictionary<string, SmokeColorDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, definition) in config.Colors)
        {
            if (string.IsNullOrWhiteSpace(key) || definition is null) continue;
            definition.Name = string.IsNullOrWhiteSpace(definition.Name) ? key : definition.Name;
            definition.ParsedColor = ParseRgb(definition.Rgb, $"color '{key}'", TeamOther);
            ValidateRequirement(definition.Permission, $"color '{key}'");
            normalized[key.Trim()] = definition;
        }
        config.Colors = normalized;
        ValidateRequirement(config.RandomPermission, "random_permission");
        var groupDefaults = new List<GroupDefault>(config.GroupDefaults.Count);
        foreach (var groupDefault in config.GroupDefaults)
        {
            if (groupDefault is null || string.IsNullOrWhiteSpace(groupDefault.Color))
            {
                Logger.LogError("[PyroSmoke] Group default has no color and will be ignored.");
                continue;
            }

            groupDefault.Color = groupDefault.Color.Trim();
            if (!config.Colors.ContainsKey(groupDefault.Color))
            {
                Logger.LogError("[PyroSmoke] Unknown color '{Color}' in group default; entry will be ignored.", groupDefault.Color);
                continue;
            }

            ValidateRequirement(groupDefault.Permission, $"group default '{groupDefault.Color}'");
            groupDefaults.Add(groupDefault);
        }
        config.GroupDefaults = groupDefaults;

        var overrides = new Dictionary<string, PlayerOverride>(StringComparer.Ordinal);
        foreach (var (steamId, playerOverride) in config.PlayerOverrides)
        {
            if (string.IsNullOrWhiteSpace(steamId) || playerOverride is null) continue;
            playerOverride.Access ??= [];
            playerOverride.ParsedColor = TryParseRgb(playerOverride.Color, out var parsed)
                ? parsed : LogInvalidOverride(steamId, playerOverride.Color);
            overrides[steamId.Trim()] = playerOverride;
        }
        config.PlayerOverrides = overrides;
        Config = config;
    }

    public override void Load(bool hotReload)
    {
        _preferences = new PlayerPreferenceManager(ModuleDirectory, Logger);
        _preferences.Load();
        _smokes = new SmokeManager(this, _preferences);
        RegisterListener<Listeners.OnEntitySpawned>(_smokes.OnEntitySpawned);
        _registeredCommand = "css_" + Config.Command;
        AddCommand(_registeredCommand, "Smoke rengi seçim menüsünü açar.", OnSmokeCommand);
        Logger.LogInformation("[PyroSmoke] Plugin loaded successfully.{HotReload}", hotReload ? " (hot reload)" : "");
    }

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _menuManager = new PluginCapability<IT3MenuManager>("t3menu:manager").Get();
        if (_menuManager is null)
            Logger.LogError("[PyroSmoke] T3Menu-API bulunamadı. !{Command} menüsü T3Menu kurulana kadar açılamaz.", Config.Command);
        else
            Logger.LogInformation("[PyroSmoke] T3Menu-API bağlantısı hazır.");
    }

    public override void Unload(bool hotReload)
    {
        if (_smokes is not null)
        {
            _smokes.Deactivate();
            RemoveListener<Listeners.OnEntitySpawned>(_smokes.OnEntitySpawned);
        }
        RemoveCommand(_registeredCommand, OnSmokeCommand);
        Logger.LogInformation("[PyroSmoke] Plugin unloaded successfully.{HotReload}", hotReload ? " (hot reload)" : "");
    }

    private void OnSmokeCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (!Config.Enabled) { command.ReplyToCommand(Localizer.ForPlayer(player, "plugin.disabled")); return; }
        if (!IsUsablePlayer(player)) return;
        if (_menuManager is null) { player!.PrintToChat(Localizer.ForPlayer(player, "menu.unavailable")); return; }
        OpenMenu(player!);
    }

    private void OpenMenu(CCSPlayerController player)
    {
        if (_menuManager is null) return;
        var menu = _menuManager.CreateMenu(
            Localizer.ForPlayer(player, "menu.title"),
            showDeveloper: false,
            freezePlayer: true,
            hasSound: true,
            isSubMenu: false,
            isExitable: true);
        menu.AddOption(Localizer.ForPlayer(player, "menu.team"), (p, _) => SelectTeam(p));
        if (Config.RandomEnabled)
        {
            var allowed = HasAccess(player, Config.RandomPermission, GetOverride(player));
            menu.AddOption((allowed ? "" : "[VIP] ") + Localizer.ForPlayer(player, "menu.random"), (p, _) => SelectRandom(p));
        }
        foreach (var pair in Config.Colors)
        {
            var key = pair.Key;
            var definition = pair.Value;
            var allowed = HasAccess(player, definition.Permission, GetOverride(player));
            var label = (allowed ? "" : AccessLabel(definition.Permission)) + definition.Name;
            menu.AddOption(label, (p, _) => SelectColor(p, key));
        }
        _menuManager.OpenMainMenu(player, menu);
    }

    private void SelectTeam(CCSPlayerController player)
    {
        if (!CanChange(player)) return;
        Persist(player, new PlayerSmokePreference());
        player.PrintToChat(Localizer.ForPlayer(player, "color.team"));
    }

    private void SelectRandom(CCSPlayerController player)
    {
        if (!CanChange(player) || !Config.RandomEnabled) return;
        if (!HasAccess(player, Config.RandomPermission, GetOverride(player))) { Deny(player); return; }
        Persist(player, new PlayerSmokePreference { Mode = "random" });
        player.PrintToChat(Localizer.ForPlayer(player, "color.random"));
    }

    private void SelectColor(CCSPlayerController player, string key)
    {
        if (!CanChange(player) || !Config.Colors.TryGetValue(key, out var definition)) return;
        if (!HasAccess(player, definition.Permission, GetOverride(player))) { Deny(player); return; }
        Persist(player, new PlayerSmokePreference { Mode = "custom", Color = key });
        player.PrintToChat(Localizer.ForPlayer(player, "color.changed", definition.Name));
    }

    private bool CanChange(CCSPlayerController player)
    {
        if (!IsUsablePlayer(player)) return false;
        if (GetOverride(player)?.Locked != true) return true;
        player.PrintToChat(Localizer.ForPlayer(player, "override.locked"));
        return false;
    }

    private void Persist(CCSPlayerController player, PlayerSmokePreference preference)
    {
        if (_preferences?.Set(player.SteamID.ToString(), preference) == false)
            player.PrintToChat(Localizer.ForPlayer(player, "save.failed"));
    }

    private void Deny(CCSPlayerController player) => player.PrintToChat(Localizer.ForPlayer(player, "access.denied"));

    public bool HasAccess(CCSPlayerController player, string requirement, PlayerOverride? playerOverride = null)
    {
        if (string.IsNullOrWhiteSpace(requirement)) return true;
        requirement = requirement.Trim();
        if (playerOverride?.Access?.Any(x => x?.Equals(requirement, StringComparison.OrdinalIgnoreCase) == true) == true) return true;
        if (requirement[0] is not ('@' or '#')) return false;
        return requirement[0] == '#'
            ? AdminManager.PlayerInGroup(player, requirement)
            : AdminManager.PlayerHasPermissions(player, requirement);
    }

    public Vector? PickRandomColor(CCSPlayerController player, PlayerOverride? playerOverride)
    {
        Vector? selected = null;
        var seen = 0;
        foreach (var definition in Config.Colors.Values)
        {
            if (!HasAccess(player, definition.Permission, playerOverride)) continue;
            seen++;
            if (Random.Shared.Next(seen) == 0) selected = definition.ParsedColor;
        }
        return selected;
    }

    private PlayerOverride? GetOverride(CCSPlayerController player) =>
        Config.PlayerOverrides.TryGetValue(player.SteamID.ToString(), out var value) ? value : null;

    public static bool IsUsablePlayer(CCSPlayerController? player)
    {
        if (player is null || !player.IsValid || player.IsBot || player.IsHLTV) return false;
        try { return player.UserId is not null && player.AuthorizedSteamID is not null; }
        catch { return false; }
    }

    private Vector ParseRgb(string value, string context, Vector fallback)
    {
        if (TryParseRgb(value, out var parsed)) return parsed;
        Logger.LogError("[PyroSmoke] Invalid RGB color for {Context}: {Value}. Fallback kullanılıyor.", context, value);
        return fallback;
    }

    private Vector? LogInvalidOverride(string steamId, string value)
    {
        Logger.LogError("[PyroSmoke] Invalid RGB color for SteamID override '{SteamId}': {Value}. Override yok sayılıyor.", steamId, value);
        return null;
    }

    private void ValidateRequirement(string? requirement, string context)
    {
        if (string.IsNullOrWhiteSpace(requirement)) return;
        var first = requirement.Trim()[0];
        if (first is not ('@' or '#'))
            Logger.LogError("[PyroSmoke] Invalid permission/group for {Context}: {Requirement}. '@' permission veya '#' group kullanın; erişim güvenli biçimde reddedilecek.", context, requirement);
    }

    private static bool TryParseRgb(string? value, out Vector color)
    {
        color = new Vector(0, 255, 0);
        if (string.IsNullOrWhiteSpace(value)) return false;
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 3) return false;
        Span<byte> rgb = stackalloc byte[3];
        for (var i = 0; i < 3; i++)
            if (!byte.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out rgb[i])) return false;
        color = new Vector(rgb[0], rgb[1], rgb[2]);
        return true;
    }

    private static string NormalizeCommand(string? command)
    {
        var value = string.IsNullOrWhiteSpace(command) ? "smoke" : command.Trim().ToLowerInvariant();
        if (value.StartsWith("css_", StringComparison.Ordinal)) value = value[4..];
        return value.Length > 0 && value.All(c => char.IsAsciiLetterOrDigit(c) || c == '_') ? value : "smoke";
    }

    private static string AccessLabel(string requirement) => requirement.StartsWith("#", StringComparison.Ordinal)
        ? "[GRUP] " : requirement.Contains("admin", StringComparison.OrdinalIgnoreCase) ? "[ADMIN] " : "[VIP] ";
}
