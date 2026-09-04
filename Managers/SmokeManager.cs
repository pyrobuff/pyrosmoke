using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using PyroSmoke.Models;
using PyroSmokePlugin = PyroSmoke.Plugin.PyroSmoke;

namespace PyroSmoke.Managers;

public sealed class SmokeManager
{
    private readonly PyroSmokePlugin _plugin;
    private readonly PlayerPreferenceManager _preferences;
    private bool _active = true;

    public SmokeManager(PyroSmokePlugin plugin, PlayerPreferenceManager preferences) { _plugin = plugin; _preferences = preferences; }

    public void OnEntitySpawned(CEntityInstance entity)
    {
        if (!_active || !_plugin.Config.Enabled || !entity.IsValid || entity.DesignerName != "smokegrenade_projectile") return;
        var smoke = entity.As<CSmokeGrenadeProjectile>();
        Server.NextFrame(() => Apply(smoke));
    }

    public void Deactivate() => _active = false;

    private void Apply(CSmokeGrenadeProjectile smoke)
    {
        if (!_active || !smoke.IsValid || !smoke.Thrower.IsValid) return;
        var pawn = smoke.Thrower.Value;
        if (pawn is null || !pawn.IsValid || !pawn.Controller.IsValid) return;
        var baseController = pawn.Controller.Value;
        if (baseController is null || !baseController.IsValid) return;
        var player = baseController.As<CCSPlayerController>();
        if (!PyroSmokePlugin.IsUsablePlayer(player)) return;
        var color = ResolveColor(player, out var source);
        smoke.SmokeColor.X = color.X; smoke.SmokeColor.Y = color.Y; smoke.SmokeColor.Z = color.Z;
        if (_plugin.Config.Debug)
            _plugin.Logger.LogInformation("[PyroSmoke] player={Player} steamid={SteamId} source={Source} smoke={Index} rgb={R} {G} {B}",
                player.PlayerName, player.SteamID, source, smoke.Index, color.X, color.Y, color.Z);
    }

    private Vector ResolveColor(CCSPlayerController player, out string source)
    {
        var steamId = player.SteamID.ToString();
        if (_plugin.Config.PlayerOverrides.TryGetValue(steamId, out var playerOverride) && playerOverride.ParsedColor is not null)
        { source = "override"; return playerOverride.ParsedColor; }

        var preference = _preferences.Get(steamId);
        if (string.Equals(preference.Mode, "custom", StringComparison.OrdinalIgnoreCase) && preference.Color is not null &&
            _plugin.Config.Colors.TryGetValue(preference.Color, out var selected) && _plugin.HasAccess(player, selected.Permission, playerOverride))
        { source = preference.Color; return selected.ParsedColor; }

        if (string.Equals(preference.Mode, "random", StringComparison.OrdinalIgnoreCase) && _plugin.Config.RandomEnabled &&
            _plugin.HasAccess(player, _plugin.Config.RandomPermission, playerOverride))
        {
            var randomColor = _plugin.PickRandomColor(player, playerOverride);
            if (randomColor is not null) { source = "random"; return randomColor; }
        }

        foreach (var groupDefault in _plugin.Config.GroupDefaults)
            if (_plugin.HasAccess(player, groupDefault.Permission, playerOverride) && _plugin.Config.Colors.TryGetValue(groupDefault.Color, out var definition))
            { source = "group:" + groupDefault.Permission; return definition.ParsedColor; }

        source = "team";
        return player.Team switch { CsTeam.Terrorist => _plugin.TeamT, CsTeam.CounterTerrorist => _plugin.TeamCt, _ => _plugin.TeamOther };
    }
}
