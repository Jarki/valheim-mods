#!/bin/sh
# Native Linux only. Run explicitly; never installs into the Steam game directory.
set -eu
root=$(CDPATH= cd -- "$(dirname -- "$0")/../.." && pwd)
game=${VALHEIM_GAME_DIR:-"$HOME/.local/share/Steam/steamapps/common/Valheim"}
reference=${VALHEIM_REFERENCE_PROFILE:-"$HOME/.config/r2modmanPlus-local/Valheim/profiles/Valheim2"}
if [ -z "${VALHEIM_REFERENCE_PROFILE:-}" ] && [ ! -d "$reference" ] && [ -d "$root/.dev/reference-profile" ]; then
    reference="$root/.dev/reference-profile"
fi
dev="$root/.dev/pistol"
profile="$dev/profile"
plugin="$root/mods/PistolMod/bin/Release/netstandard2.1/PistolMod.dll"
if [ ! -f "$plugin" ]; then
    printf '%s\n' 'Build PistolMod.csproj in Release configuration first.' >&2
    exit 1
fi
if [ ! -x "$game/valheim.x86_64" ] || [ ! -f "$reference/BepInEx/plugins/ValheimModding-Jotunn/Jotunn.dll" ]; then
    printf '%s\n' 'Set VALHEIM_GAME_DIR and VALHEIM_REFERENCE_PROFILE to the local game and BepInEx/Jotunn reference profile.' >&2
    exit 1
fi
if pgrep -x valheim.x86_64 >/dev/null; then
    printf '%s\n' 'Close the running Valheim instance before launching the isolated development profile.' >&2
    exit 1
fi
mkdir -p "$profile/BepInEx/plugins/Jotunn" "$profile/BepInEx/config" "$dev/config" "$dev/evidence"
cp -R "$reference/BepInEx/core" "$profile/BepInEx/"
cp -R "$reference/doorstop_libs" "$profile/"
cp "$reference/start_game_bepinex.sh" "$profile/"
cp "$reference/BepInEx/plugins/ValheimModding-Jotunn/Jotunn.dll" "$profile/BepInEx/plugins/Jotunn/"
cp "$plugin" "$profile/BepInEx/plugins/PistolMod.dll"
printf '%s\n' "Local development saves: $dev/config/unity3d/IronGate/Valheim" 'Use a NEW LOCAL character and world. Do not select existing Steam Cloud saves or join a server.'
export XDG_CONFIG_HOME="$dev/config"
export SteamAppId=892970 SteamGameId=892970
cd "$game"
exec /bin/sh "$profile/start_game_bepinex.sh" "$game/valheim.x86_64" \
    -console -screen-fullscreen 0 -screen-width 1280 -screen-height 720 \
    -logFile "$dev/evidence/manual-game.log" "$@"
