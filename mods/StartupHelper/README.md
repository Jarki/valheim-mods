# Startup Helper

**Paused by the owner (2026-11-05), including server-join work: no further
development or publication for now.** Source/tests and the historical notes below
are preserved. Resume only on an explicit owner decision.

A standalone **client-only BepInEx 5** plugin that selects an existing character
and starts an existing private world or joins a server on game launch.
It has no Jotunn, Firearm Arsenal, or PistolMod dependency.

**Disabled by default.** It makes at most one automatic attempt per process.
Logging out, failed validation, a failed connection, or cancelling does not rearm
it. Restart Valheim for another automatic attempt.

## Build

```sh
dotnet build mods/StartupHelper/StartupHelper.csproj -c Release
```

Defaults read the native Steam game at
`~/.local/share/Steam/steamapps/common/Valheim` and loader references from
`.dev/reference-profile`. Override with `-p:GameDir=/path/to/Valheim` and
`-p:ReferenceProfile=/path/to/profile`. No dependency assemblies are bundled.

Output: `mods/StartupHelper/bin/Release/netstandard2.1/StartupHelper.dll`.

## Install and configure

1. In a separate development profile with an existing BepInEx 5 installation,
   place **only `StartupHelper.dll`** in `BepInEx/plugins/StartupHelper/`.
2. Run that profile once to generate
   `BepInEx/config/local.valheim.startuphelper.cfg`, or copy the adjacent
   `local.valheim.startuphelper.cfg.example` to that path before launching.
3. Edit configuration while Valheim is closed. Set the exact character and target,
   then set `Enabled = true`. Start through that profile's normal loader route.

Native Linux requires Linux Doorstop; use the profile's existing Linux launcher.
The plugin does not install a loader or change save directories. Its saves are
the ones visible to the launched game/profile. Steam Cloud saves can still be
visible with an isolated `XDG_CONFIG_HOME`; explicit `Local` sources prevent
selecting them. Keep first verification on disposable local saves.

### Disposable local configuration

The example targets the development character `firearmlab` and existing world
`firearmlab2026`; it remains disabled until deliberately enabled:

```ini
[Startup]
Enabled = true
Character = firearmlab
CharacterSource = Local
Mode = LocalWorld
LaunchDelaySeconds = 5
CancelKey = F8

[LocalWorld]
Name = firearmlab2026
Source = Local
```

`Character` is the **case-sensitive filename without `.fch`**, not the character's
display name. `Name` is the exact world name in the game's world list (`m_name`).
Sources can be `Local`, `Cloud`, or `Legacy`; there is no `Auto`. The game supplies
its active saves, so a file visible only as a backup is not eligible. Exactly one
matching, loadable character and world must exist. Missing, ambiguous, corrupt,
or incompatible saves stop the helper; it never selects the first/last-used save
as a fallback or creates a new character/world.

LocalWorld starts with **Start Server, Community Server, and Crossplay off**.
World modifiers are retained. Normal play still saves the chosen character/world.

### Server configuration — runtime unverified

Keep the desired exact character settings above, then use:

```ini
[Startup]
Enabled = true
Character = your_exact_character_filename
CharacterSource = Local
Mode = Server
LaunchDelaySeconds = 5
CancelKey = F8

[Server]
Address = your-server.example.net
Port = 2456
PasswordEnvironmentVariable =
```

Use an IP address or DNS hostname, with the **gameplay port** in `Port` (not the
Steam query port). IPv6 addresses use no brackets in `Address`. Join codes and
lobby IDs are not configuration targets. The game resolves the address and
chooses the connection backend using its ordinary dedicated-server join flow.

Leave `PasswordEnvironmentVariable` empty for the normal game password prompt.
To automate password entry, set it to the **name** of an environment variable
inherited by the actual Valheim process, for example
`PasswordEnvironmentVariable = VALHEIM_PASSWORD`. The helper reads its value
only at the first password handshake and submits it through the game's existing
password handler. An unset/empty variable leaves the prompt open. The helper
does not save or log the password, retry a wrong password, or retain it for
later manual joins. The one-handshake window expires after 120 seconds. An
existing vanilla `-password` argument takes precedence; avoid combining them.

The normal EULA prompt, multiplayer privileges, server/version checks, connection
errors, and password authentication still apply. A prompt can require manual
input; a rejected check is not retried. No server installation of this helper
is needed.

## Cancellation and diagnostics

- The countdown appears after the main menu/intro is ready. Default delay: five
  seconds. Hold or press **F8** during startup/countdown to cancel for this run.
  Configure another Unity `KeyCode`, or `None` to disable this shortcut.
- `Startup.SkipStartupCinematic = true` (default) skips the startup intro through
  the game's normal `CinematicsManager.Stop()` action once playback is skippable,
  only while automatic startup is enabled and a character/target is configured.
  It does not dismiss consent or other dialogs. Set it to `false` to watch the intro.
- Navigating away during the countdown cancels it. The initial menu wait allows
  180 seconds. A detected startup intro extends the deadline once to at least its
  full clip duration plus 180 seconds from detection, so normal playback fits
  when skipping is disabled. Existing Steam invites or command-line join requests take
  precedence and cancel this helper's attempt.
- Once loading/joining has been handed to Valheim, use Valheim's usual UI to
  cancel. The shortcut does not interrupt an in-progress game load.
- Read `BepInEx/LogOutput.log` for `[Startup Helper]` messages. A failure message
  also appears briefly on-screen. Countdown/failure text uses a screen-space
  Unity UI/TMP label with an already-loaded game font (no external assets),
  avoiding the native Unity 6 client's invisible default IMGUI text. The overlay
  does not intercept clicks and is destroyed when finished or the plugin unloads.
  “Starting” or “handed to the game” confirms
  dispatch, not a successful world load or connection.
- Disable with `Enabled = false`, or remove this plugin's DLL while the game is
  closed. It adds no items, save schema, or server-side state.

## Implementation and verification

Inspected owned native client **build 25909357**, Valheim **l-1.0.20**, Unity
**6000.0.75f1**, with BepInEx **5.4.2351**. Implementation uses a postfix on
`FejdStartup.Start`, exact indices into its loaded profiles, `OnCharacterStart`,
and `OnWorldStart` / the queued dedicated-server `JoinServer` route. Private menu
members use Harmony reflection and are checked at plugin initialization. The
password postfix calls the original `ZNet.OnPasswordEntered` only after the game
has opened its password dialog. Decompiled reference material stays outside the
repository.

`dotnet build` succeeds with .NET SDK **10.0.113**, zero errors and two existing
reference-version warnings (`System.Net.Http`, `System.IO.Compression`).
**Local startup verified in-game:** selected Local `firearmlab` /
`firearmlab2026`, loaded gameplay automatically, skipped the startup cinematic
through its native handler, and stayed at the menu after logout. The local flow
was used for repeated firearm regression launches. Evidence is recorded in
`.dev/pistol/evidence/arsenal-run4/LogOutput.log`.
Server/password flow, F8 cancellation, and missing-save error paths have not
been runtime-tested. Server flow requires a separately authorized test.

Written with AI assistance; depends on the user's own Valheim and BepInEx files.
