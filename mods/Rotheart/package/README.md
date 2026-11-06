# Rotheart

**The swamp takes its tithe.** Carry a living ooze heart in a rusted iron cage: resist poison, then spend adrenaline to unleash a miasma that hurts nearby creatures much more than it hurts you.

Rotheart adds one original swamp-tier trinket, with a Blender-made model, inventory icon, visible hip attachment and sparse green aura particles.

**Tested in a disposable native Linux client. Remote multiplayer and dedicated servers are unverified.** Use a separate profile and back up your saves before adding custom items.

## Crafting and use

Craft one Rotheart at a **level-one forge** after discovering its ingredients:

| Material | Amount |
| --- | ---: |
| Ooze | 10 |
| Guck | 5 |
| Entrails | 5 |
| Iron | 5 |

Equip it in the native **trinket slot**. It weighs **2**, has one quality level and does not drain durability. Valheim currently labels this category “Utility item” in the tooltip; Rotheart does not occupy a weapon slot.

| Effect | Behavior |
| --- | --- |
| Passive protection | **50% less poison damage** while equipped |
| Activation | Native combat actions fill a **10-adrenaline** meter; reaching full automatically consumes it |
| Active miasma | Lasts **10 seconds**, with a **6-metre radius** |
| Every two seconds | Lose **1 health**, and deal **12 base blunt damage** to each eligible nearby creature |
| Health floor | Rotheart's health cost cannot reduce you below **1 HP** |

A complete activation gives five pulses: **5 wearer health versus 60 base damage per target**. Native creature resistances and world difficulty still apply. The green miasma deals **blunt**, not poison, so it remains useful against poison-immune swamp creatures.

- Other players, tamed creatures, friendly factions, structures and terrain are excluded, even with PvP enabled. Native hostile factions and wild animals can be hit.
- The radius is spherical and does not require line of sight.
- No extra hotkey or separate charge resource. Adrenaline capacity returns to normal when unequipped.
- Filling adrenaline again refreshes the duration and restarts the pulse timer; it does not stack another aura.
- Unequipping or dying stops the active aura. Unequipping also removes the passive resistance. Teleport/cutscene frames do not deal aura damage.
- Stronger native poison-resistance mead keeps its stronger protection; reductions do not multiply.

## Requirements and installation

Built and tested against **Valheim l-1.0.20**, Unity **6000.0.75f1**. Older versions without native trinket/adrenaline APIs are unsupported.

Required dependencies:

- **denikson-BepInExPack_Valheim 5.4.2351**
- **ValheimModding-Jotunn 2.30.2**

**Mod manager:** install `Jarki-Rotheart` into a separate Valheim profile and accept both dependencies. Launch modded through your normal OS-appropriate loader route.

**Manual:** close Valheim, install the required dependencies, then copy this archive's `plugins/Rotheart/` directory into `BepInEx/plugins/`. The resulting plugin path is `BepInEx/plugins/Rotheart/Rotheart.dll`. Keep `LICENSE` and remove duplicate Rotheart DLLs. Model, icon and effects are embedded; no additional asset downloads are needed. Native Linux uses Linux Doorstop, not Proton DLL overrides.

The mod declares **matching installations on the server and every client** because it adds a networked item. This requirement is not a claim of verified remote-multiplayer compatibility. Windows, controllers, unrelated-mod combinations and every armor outfit have not been tested.

There are no gameplay configuration options in this release. Stable plugin ID: `jarki.rotheart`; item prefab: `Rotheart`.

## Uninstall and troubleshooting

**Back up first.** Remove Rotheart items from characters, chests, drops and displays before disabling or uninstalling the mod. Loading a save without its custom prefab definition can lose those items. Then close Valheim and remove `BepInEx/plugins/Rotheart/Rotheart.dll`. Uninstall/reinstall has not been runtime-tested.

If the item is missing, confirm that both dependencies loaded and that your game supports native trinkets. Learn all four ingredients and use a sheltered level-one forge. For errors, report your game/mod/dependency versions, reproduction steps and relevant redacted `BepInEx/LogOutput.log` entries. Never publish saves, account identifiers, passwords or tokens.

## Verification and credits

The exact gameplay DLL completed **65 in-game assertions**, including fixture prerequisites: real forge crafting and ingredient consumption, native equipping, poison resistance and stronger-mead precedence, 9-versus-10 adrenaline activation, meter consumption, five timed pulses, actual hostile health loss, draugr/blob damage, exclusions, the nonlethal floor, refresh, unequip/death cleanup and actual character-file save/load into a fresh Player. Final scenario: **zero Unity errors or exceptions**. Inventory/tooltip, hip attachment, colored world drop and aura-combat screenshots were inspected.

Release packaging verifies that the ZIP contains the same runtime-tested DLL. This is **not** a fresh mod-manager ZIP-import test, a two-client network test or a dedicated-server test. Release builds retain two existing MSB3277 framework-reference warnings; they are not suppressed.

Original code, Blender model, icon and effects are **MIT licensed, copyright 2026 Jarki**; see `LICENSE`. Developed and packaged with **OpenAI Codex (gpt-6-astra)** assistance and categorized **AI Generated**. No fal-generated assets, third-party recordings, extracted game assets, game assemblies or loader/dependency binaries are included. Valheim, BepInEx, Harmony and Jotunn belong to their respective owners.

[Project repository](https://github.com/Jarki/valheim-mods) · [Report issues](https://github.com/Jarki/valheim-mods/issues)

Rotheart source has not been pushed to the public repository as part of this release preparation.
