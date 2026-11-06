# Rotheart

An original swamp-tier Valheim trinket: living ooze in a rusted iron cage, bound with entrails. **[Version 1.0.0 published on Thunderstore](https://thunderstore.io/c/valheim/p/Jarki/Rotheart/), 2026-11-06.** Public download is byte-identical to the approved ZIP. [Source on GitHub](https://github.com/Jarki/valheim-mods/tree/main/mods/Rotheart).

## Gameplay

Craft at a **level-one forge** after discovering the ingredients:

| Ingredient | Quantity |
| --- | ---: |
| Ooze | 10 |
| Guck | 5 |
| Entrails | 5 |
| Iron | 5 |

- Uses the native **trinket slot**, not a weapon slot. Weight 2; one quality; no durability drain.
- While equipped, grants native **Poison Resistant: 50% less poison damage**. Stronger poison-resistance mead retains its stronger protection; reductions do not multiply.
- Sets the wearer's adrenaline capacity to **10**. Native combat actions fill the meter. Reaching full adrenaline automatically consumes it and activates **Rotheart miasma for 10 seconds**. No additional hotkey or second resource system.
- Every **2 seconds**, the wearer pays **1 health**, stopping at **1 HP**, and creatures inside **6 metres** receive **12 base blunt damage**. Five pulses per complete activation: 5 wearer health versus 60 base damage per eligible creature.
- The aura hits native enemy factions, including wild animals. **Other players, tamed animals, friendly factions, structures and terrain are excluded**, even with PvP enabled. It does not require line of sight.
- Damage uses the native hit/RPC path, so creature resistances and world difficulty still apply. Blunt damage keeps the swamp-themed effect useful against poison-immune blobs and undead; it is not poison damage.
- Reaching full adrenaline again refreshes the active duration and restarts its pulse timer; it does not add a second aura. Unequipping or dying stops it. Unequipping also removes the passive resistance and restores normal adrenaline capacity. Teleport/cutscene frames do not deal aura damage.

Valheim currently labels the native trinket category as “Utility item” in its tooltip; Rotheart uses `ItemType.Trinket` internally.

## Installation and save safety

Requires the owned **Valheim l-1.0.20** native API, **BepInExPack Valheim 5.4.2351** and **Jotunn 2.30.2**. Older game builds without native trinket/adrenaline support are unsupported.

Build output: `bin/Release/netstandard2.1/Rotheart.dll`. With Valheim closed, copy only that DLL to a separately managed profile's `BepInEx/plugins/Rotheart/` directory. The model and icon are embedded. Do not copy test DLLs, game assemblies, dependency binaries or development profiles into a distribution.

This adds a networked item: matching mod versions are required on the server and every client. **Remote multiplayer and dedicated-server operation have not been verified.** This work installed it only into disposable local test profiles; it did not change your normal gameplay profile or server.

Back up characters/worlds before introducing custom items. Remove Rotheart from inventories, chests, drops and displays before uninstalling; loading a save without its custom item definition can lose those items. Stable identifiers: plugin `jarki.rotheart`, DLL `Rotheart.dll`, item/prefab `Rotheart`. Console spawning is `spawn Rotheart` in an explicitly enabled disposable local debug session.

## Build and original art

From the repository root, using .NET SDK 10 and read-only owned game/loader references:

```sh
dotnet build mods/Rotheart/Rotheart.csproj -c Release \
  -p:GameDir=/path/to/Valheim \
  -p:ReferenceProfile=/path/to/BepInEx-Jotunn-profile
```

The reference profile must contain `BepInEx/core/` and `BepInEx/plugins/ValheimModding-Jotunn/Jotunn.dll`. No game or dependency assemblies are bundled. The existing `System.Net.Http` and `System.IO.Compression` MSB3277 reference warnings remain visible.

Package through the pinned local tcli wrapper; see [release gates](../../docs/RELEASING.md):

```sh
uv run python tools/release.py check MobThreat
uv run python tools/release.py check Rotheart
uv run python tools/release.py build Rotheart \
  --game-dir /path/to/Valheim --reference-profile /path/to/BepInEx-Jotunn-profile
uv run python tools/release.py verify dist/Jarki-Rotheart-1.0.0.zip
```

Only the plugin, manifest, public README/changelog, package icon and MIT license
are shipped. Public installation instructions are in [package/README.md](package/README.md).

The committed original model has 2,048 triangles in eight material batches, with a transparent 256×256 inventory icon. Regenerate both with Blender:

```sh
blender -b --python mods/Rotheart/Tools/build_models.py
uv run python mods/Rotheart/Tools/build_package_icon.py
```

`Tools/build_models.py` exports the repository's existing FHM1 mesh format in Unity metres, +Y up/+Z front. Original meshes, runtime material palette, icon and sparse aura particles require neither Unity Editor nor paid asset services. No extracted game art is included.

## Repeatable native verification

The test plugin is **not for ordinary gameplay**. It creates a local disposable character/world, crafts the item through the native forge UI, spawns controlled creature fixtures, exercises damage/persistence and kills the disposable character at the end. It exits the game automatically. The launcher refuses to run alongside an existing Valheim process.

Only run after explicitly authorizing game automation. On Linux/X11, with Steam running and appropriate `DISPLAY`/`XAUTHORITY`:

```sh
dotnet build tests/Rotheart.InGameTests/Rotheart.InGameTests.csproj -c Release \
  -p:GameDir=/path/to/Valheim \
  -p:ReferenceProfile=/path/to/BepInEx-Jotunn-profile

export VALHEIM_GAME_DIR=/path/to/Valheim
export VALHEIM_REFERENCE_PROFILE=/path/to/BepInEx-Jotunn-profile
export VALHEIM_LOADER_PROFILE=/path/to/native-BepInEx-profile
python tests/Rotheart.InGameTests/run.py setup
# Use the exact new directory printed above, and back it up before launch:
uv run um backup create /printed/run/directory --name rotheart-lab
ROTHEART_RUN=disposable-local-only python tests/Rotheart.InGameTests/run.py run \
  --run-dir /printed/run/directory
```

The native loader profile supplies `doorstop_libs/` and `start_game_bepinex.sh`; it is read only. Each setup copies only loader core, Jotunn, Rotheart and the test DLL into a fresh `.dev/rotheart-lab/runs/<id>/`. Separate XDG directories isolate local saves; the harness selects only explicitly created `FileSource.Local` records, never existing/cloud saves. Screenshots, runtime assertions, game logs and the completion marker are under that run's `evidence/` directory. The DLL under test is copied at setup time; create another run after rebuilding.

## Verification result

On 2026-11-05, the final DLL passed **65 native-game assertions** in Valheim
`l-1.0.20` on Linux with BepInExPack `5.4.2351` and Jotunn `2.30.2`.
The run exercised real forge crafting/ingredient consumption, equipment and
poison-mead precedence, 9-versus-10 adrenaline activation, meter consumption,
five timed pulses, actual creature health loss, swamp draugr/blob targets,
radius and friendly/player/tamed/structure exclusions, the 1-HP floor,
refresh, unequip/death cleanup, and character-file save/load into a fresh Player.
No Unity errors or exceptions occurred in the final scenario.

Inventory/tooltip, hip attachment, world-drop materials and aura-combat screenshots
were inspected. Initial testing found and corrected a particle curve-mode mismatch
and an incorrect particle-material fallback for the solid model; fixture-only
failures are recorded in MODLOG rather than hidden.

Final evidence: `.dev/rotheart-lab/runs/20261105205139565502/evidence/`.
The delivered DLL and the tested profile DLL have the same SHA-256:
`93ce43500cfc24e3b2c737778d8a7f46029e580f7e8ab81f4cd4bc5f4c3293be`.
Standalone mesh validation also passed. These are local controlled tests, not
remote-multiplayer, dedicated-server, controller, every-armor-outfit or every-mod
compatibility certification.

## License and credits

Original code and art are MIT licensed under the repository [LICENSE](../../LICENSE), copyright 2026 Jarki. Developed with AI assistance (OpenAI Codex). BepInEx, Jotunn and Valheim retain their own licenses and are not redistributed. See [MODLOG.md](MODLOG.md) for implementation findings and test history.
