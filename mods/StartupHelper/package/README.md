# Startup Helper

**Unpublished release candidate for the Jarki team.** Mod-manager instructions apply after an approved publication; until then, import a locally built ZIP or use the manual instructions.

A standalone **client-only** plugin that selects an existing character and starts an existing private world or makes one server-join attempt when Valheim launches. Disabled by default. No Jotunn or firearm dependency; nothing is installed on the server.

## Requirements and installation

Requires Valheim and **BepInExPack Valheim 5.4.2351**.

- **Mod manager:** install `Jarki-Startup_Helper` into your Valheim profile with a Thunderstore-compatible manager such as r2modman, accept its BepInEx dependency, then launch modded.
- **Manual:** close Valheim; install BepInEx for your operating system. Copy the archive's `plugins/StartupHelper/` directory into `BepInEx/plugins/`, producing `BepInEx/plugins/StartupHelper/StartupHelper.dll`. Keep `LICENSE`, remove duplicate DLLs, and use your existing loader launch route (Linux requires Linux Doorstop).

## Configuration and use

Launch once to generate `BepInEx/config/local.valheim.startuphelper.cfg`, then close Valheim before editing. Start with disposable, backed-up local saves:

```ini
[Startup]
Enabled = true
Character = YourExactCharacterFilename
CharacterSource = Local
Mode = LocalWorld
LaunchDelaySeconds = 5
CancelKey = F8
SkipStartupCinematic = true

[LocalWorld]
Name = YourExactWorldName
Source = Local
```

`Character` is the case-sensitive filename without `.fch`, not its display name. World `Name` is the exact name shown in the world list. Sources are `Local`, `Cloud` or `Legacy`; no automatic fallback exists. A unique, loadable match must already exist. It never creates saves. LocalWorld starts privately with Start Server, Community Server and Crossplay off; world modifiers remain intact.

For the **runtime-unverified server flow**, keep your character settings, set `Startup.Mode = Server`, and configure:

```ini
[Server]
Address = your-server.example.net
Port = 2456
PasswordEnvironmentVariable =
```

Use a DNS name or IP and the gameplay port, not the Steam query port; IPv6 addresses have no brackets. Join codes/lobby IDs are unsupported. Leave the password setting empty for the game's prompt, or set it to the **name** of an environment variable inherited by the game process, never the password itself. Unset values leave the prompt open. The helper neither logs nor stores passwords and does not retry rejected passwords. Avoid combining this with vanilla `-password`, which takes precedence.

Hold/press **F8** during startup/countdown to cancel. Menu navigation and existing invites/join requests also cancel. Once loading is handed to Valheim, use its normal UI to cancel. There is at most one automatic attempt per process: logout, failure and cancellation do not rearm it. Restart for another attempt. Normal consent, privilege and authentication prompts still apply. See `BepInEx/LogOutput.log` for `[Startup Helper]` diagnostics; dispatch is not proof of successful loading.

## Uninstall and save safety

Set `Startup.Enabled = false` or close Valheim and remove `BepInEx/plugins/StartupHelper/StartupHelper.dll`; its config may also be deleted. It adds no items, save schema or server-side state. Normal gameplay still saves the selected character/world, so back up saves and choose targets carefully. An isolated mod profile does not necessarily hide Steam Cloud saves.

## Testing and limitations

Local-world automatic startup, intro skipping and staying at the menu after logout were exercised on native **Valheim l-1.0.20, build 25909357, Unity 6000.0.75f1**, with BepInEx 5.4.2351. Server/password flow, F8 cancellation and missing-save error paths were not runtime-tested. Future game-menu changes can break integration. This first Thunderstore package is unpublished; no clean-profile archive installation or new server launch is claimed.

## License, credits and AI disclosure

Original code and package icon: **MIT, copyright 2026 Jarki**; see `LICENSE`. Valheim and BepInEx belong to their respective owners and are not bundled. Developed with AI assistance, including OpenAI Codex. This packaging work used **gpt-6.1-sol**, without attributing all historical work to that model. The icon is original programmatic artwork.

## Source and support

[Source and development documentation](https://github.com/Jarki/valheim-mods/tree/main/mods/StartupHelper) · [Report issues](https://github.com/Jarki/valheim-mods/issues). Include game/mod versions, reproduction steps and relevant redacted BepInEx logs; never post passwords, tokens or private saves.
