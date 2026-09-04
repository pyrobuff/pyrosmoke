# PyroSmoke

PyroSmoke is a lightweight CounterStrikeSharp plugin that gives each player a persistent smoke-grenade color through T3Menu. It changes only the native smoke color and does not add unrelated cosmetic systems.

## Features

- Per-player team, custom, or random smoke-color selection
- SteamID64-based preferences persisted across reconnects
- Configurable T, CT, and fallback team colors
- CounterStrikeSharp permission and admin-group restrictions
- Group defaults and SteamID-specific color, access, and lock overrides
- English and Turkish player localization
- Safe RGB validation, corrupt-data handling, hot reload, and optional debug logging
- Event-driven processing without a per-tick player scan

## Requirements

- A Counter-Strike 2 dedicated server
- CounterStrikeSharp API `1.0.373` or newer
- [T3Menu-API](https://github.com/T3Marius/T3Menu-API)
- .NET 10 SDK when building from source

T3Menu is a server dependency. `lib/T3MenuSharedAPI.dll` is used only to compile PyroSmoke and is not copied into the plugin release.

## Installation

1. Install T3Menu-API and enable `FreezePlayer` in its configuration if menu movement locking is desired.
2. Copy the PyroSmoke release directory to `game/csgo/addons/counterstrikesharp/plugins/PyroSmoke/`.
3. Restart the server or run `css_plugins load PyroSmoke` from the server console.

The installed plugin directory contains:

```text
PyroSmoke/
├── PyroSmoke.dll
├── PyroSmoke.deps.json
└── lang/
    ├── en.json
    └── tr.json
```

T3Menu-API must remain installed separately; do not copy `lib/T3MenuSharedAPI.dll` into this directory.

## Commands

The default public command is `!smoke` or `/smoke`. The `command` configuration value changes its name; an optional `css_` prefix is normalized automatically.

The menu uses the navigation and selection controls supplied by T3Menu.

## Permissions

An empty permission allows everyone. CounterStrikeSharp permissions begin with `@`, such as `@css/vip`; admin groups begin with `#`, such as `#css/vip`. Invalid requirement formats are logged and denied safely.

Permissions may restrict individual colors and random mode. `group_defaults` are evaluated from top to bottom, so put the most privileged match first. A `player_overrides` entry may assign a direct RGB value, lock selection, or grant plugin-local access to named requirements.

## Configuration

CounterStrikeSharp creates the configuration after the first load at:

```text
addons/counterstrikesharp/configs/plugins/PyroSmoke/PyroSmoke.json
```

The existing JSON property names remain stable:

```json
{
  "enabled": true,
  "command": "smoke",
  "debug": false,
  "default_colors": {
    "t": "237 163 56",
    "ct": "104 163 229",
    "other": "0 255 0"
  },
  "random_enabled": true,
  "random_permission": "",
  "colors": {
    "purple": {
      "name": "Purple",
      "rgb": "170 0 255",
      "permission": ""
    },
    "white": {
      "name": "White",
      "rgb": "255 255 255",
      "permission": "@css/admin"
    }
  },
  "group_defaults": [],
  "player_overrides": {},
  "ConfigVersion": 1
}
```

RGB values use `R G B`, with every channel between `0` and `255`. Invalid configured values are logged and replaced with the existing safe fallback behavior.

Color resolution order is SteamID override, saved player selection, matching group default, then team color.

## Player data

Preferences are created and managed at runtime in `Data/players.json` below the plugin directory. The directory and initial file are created automatically. This runtime file is intentionally excluded from source control and is not part of a release package.

Writes occur only when a player changes selection. Valid entries can still load when another entry is malformed; an unreadable file is left unchanged and an empty in-memory cache is used.

## Updating

Stop or unload PyroSmoke, replace its DLL, deps file, and bundled language files, then load it again. Preserve the generated CounterStrikeSharp configuration and `Data/players.json`. Ensure the installed CounterStrikeSharp and T3Menu versions still satisfy the requirements.

## Building from source

```powershell
dotnet restore PyroSmoke.slnx
dotnet build PyroSmoke.slnx --configuration Release
```

Build output is written to `bin/Release/net10.0/`. Language sources under `Localization/` are emitted under `lang/` so CounterStrikeSharp can discover them at runtime. A server release needs only `PyroSmoke.dll`, `PyroSmoke.deps.json`, and that `lang/` directory.

## Project structure

```text
PyroSmoke/
├── Plugin/          # BasePlugin lifecycle, commands, menus, and orchestration
├── Configuration/   # CounterStrikeSharp configuration models
├── Managers/        # Smoke processing and preference persistence
├── Models/          # Serialized and runtime data models
├── Localization/    # Source language JSON files (built as lang/)
├── lib/             # Compile-time T3Menu shared API reference
├── PyroSmoke.csproj
├── PyroSmoke.slnx
└── NuGet.Config
```

## Plugin information

| Field | Value |
|---|---|
| Name | PyroSmoke |
| Version | 1.0.0 |
| Author | pyroBuff |
| Platform | CounterStrikeSharp / Counter-Strike 2 |
