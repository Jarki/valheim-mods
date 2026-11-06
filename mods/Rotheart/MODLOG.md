# Rotheart development log

## 2026-11-05 — swamp trinket

- User requests original swamp-tier trinket, polished art, small self-damage and stronger surrounding creature damage; additionally passive poison resistance and activation at 10 of the trinket resource.
- Owner explicitly approved isolated game launch/automation with new disposable profile, character and world. No existing saves/profiles, servers, publication or paused projects are authorized.
- Route: BepInEx 5 + Jotunn 2.30.2, native Linux owned Valheim, Unity 6000.0.75f1. Read-only existing references; original Blender meshes and PNG embedded in one DLL. Blender already installed.
- Owned game source inspected privately outside the repository. Native ItemType.Trinket uses m_fullAdrenalineSE at full adrenaline and consumes the meter. Rotheart uses this flow with a 10-point maximum while equipped, rather than adding a keybind or a second charge system.
- Passive poison resistance uses SE_Stats (native Resistant), because Player equipment damage modifier collection omits trinket/utility slots. Activation grants a 10-second aura: every 2 seconds, pay 1 health (floor 1), deal 12 base blunt damage within 6 metres to hostile nonplayer/nontamed creatures. Native damage resistance/difficulty still applies. Blunt avoids making the swamp charm useless against poison/spirit-immune creatures. Unequip removes the active aura.
- Native StatusEffect clones are shallow; mutable per-wearer pulse state must be initialized in Setup. StatusEffect start-effect cleanup expects a ZNetView on its visual prefab.
- Universal Modder KB search timed out after 60 seconds; local game/source evidence used. Sibling operational repository is absent; no remote or existing-profile operations attempted. No language servers configured.
- Final native and visual verification is recorded below; compilation alone was not treated as runtime proof.

### Initial native verification

- Release plugin and in-game harness compile with zero errors; two existing MSB3277 framework-reference conflicts (`System.Net.Http`, `System.IO.Compression`) remain visible. No dependency binaries are copied into mod output.
- Original art: 2,048 triangles in eight material batches, transparent 256×256 icon. Export parsed independently: finite geometry, unit normals, valid material/index ranges, exact stream consumption. Eevee render inspected; Cycles was unavailable due to a local crash, so the reproducible generator uses Eevee.
- Native game confirms Valheim `l-1.0.20`, Unity `6000.0.75f1`, BepInExPack `5.4.2351`, Jotunn `2.30.2`; Rotheart loads and registers its recipe without plugin exceptions.
- Initial runtime failures were test-fixture errors: nonexistent `wood_wall` (correct prefab `woodwall`), wooden floor pieces not providing roof cover (replaced with thatch roof), and forge origin at exactly 2m while native interaction requires strictly less than 2m (moved to 1.2m). No production workaround or free-crafting bypass was introduced.
- Each attempt uses a fresh local world/character, separate XDG tree and copied loader/dependencies. Before-launch backups: `rotheart-before-runtime/20261105-203223.zip`, `rotheart-runtime-run2/20261105-203503.zip`, `rotheart-runtime-run3/20261105-203753.zip`, `rotheart-runtime-run4/20261105-204131.zip` in the local Universal Modder backup store. Existing gameplay profiles/worlds and servers remain untouched.
- Reusable explicit-opt-in launcher is retained at `tests/Rotheart.InGameTests/run.py`; generated profiles, saves, screenshots and logs remain ignored under `.dev/rotheart-lab/`.

### Completed native and visual verification

- Run4 successfully crafted Rotheart through the native forge UI and verified passive poison resistance/10-adrenaline capacity. Its persistence fixture failed because `Player.Load` expects a fresh Player and adds known-station dictionary entries; changed the fixture to load the actual saved character file into a fresh real Player, not to clear/patch production dictionaries.
- Run5 passed 64 gameplay assertions, including native 50% poison resistance and stronger poison-mead precedence, exact 9/10 activation boundary, consumed meter, pulses at approximately 2/4/6/8/10 seconds, 5 self-health cost versus 60 greydwarf health loss, actual draugr/blob damage, exclusions, floor1, refresh, unequip/death and saved custom-item restoration.
- Visual inspection and Unity logs, despite passing gameplay checks, exposed two production issues. Particle X/Z velocity axes used Constant while Y used TwoConstants; all three now use TwoConstants. The model fallback selected the cloned item's first Renderer (a particle renderer); selecting its MeshRenderer supplies the proper solid-item shader. Final runtime material diagnostics confirm `Custom/Creature` with the original eight palette colors.
- The initial equipped close-up was a fixture camera issue: native Player hides itself when the camera is less than 2m from its root. Capture now uses a greater distance with a narrower FOV; the actual hip attachment is visible. Test-scene placement also follows terrain height rather than freezing the player above a slope.
- Run6, `.dev/rotheart-lab/runs/20261105205139565502/evidence/`, passes **65 assertions**, including a new no-Unity-errors/exception guard. Inspected the native inventory tooltip, hip-mounted charm, colored world drop and live aura/combat captures. No game behavior was mocked or production safety check bypassed.
- Final DLL SHA-256 `93ce43500cfc24e3b2c737778d8a7f46029e580f7e8ab81f4cd4bc5f4c3293be` matches the exact DLL copied into run6. Build: zero errors, two known MSB3277 warnings. Blender **5.2.3 LTS**, Eevee, generated the original art.
- Additional before-launch backups: `rotheart-runtime-run5/20261105-204608.zip` and `rotheart-runtime-run6/20261105-205139.zip`. Game exited automatically after each completed run. Remote multiplayer, dedicated servers, controllers, every armor outfit and unrelated-mod combinations remain unverified.
- No publication, deployment, upstream field-note submission, existing profile/save changes, or paused-project development occurred. This journal retains the local field findings without exporting proprietary source/assets.

## 2026-11-06 — Thunderstore 1.0.0 publication

- Added Rotheart to the existing release wrapper, requiring its Jotunn dependency, and created public metadata, README/changelog and a reproducible package-icon composition from the original inventory render.
- Checked MobThreat metadata as required, then built Rotheart with pinned tcli 0.2.4 against read-only owned references. Six-file ZIP and extracted-payload lint pass; no dependencies, tests, saves or private development files are bundled.
- The packaged DLL matches the final 65-assertion runtime-tested DLL byte-for-byte. No production code changed during publication. Two existing MSB3277 build warnings remain visible.
- Original MIT art/code and OpenAI Codex (`gpt-6-astra`) assistance are disclosed. Current rules/category slugs checked; public README and icon visually reviewed before upload.
- Owner explicitly approved the exact candidate and remaining runtime limitations. Uploaded under Jarki using the private external credential through a child-process-only environment variable, without printing or persisting it.
- Published: <https://thunderstore.io/c/valheim/p/Jarki/Rotheart/>. Public ZIP SHA-256 matches the approved artifact: `e0c940623679ac88ffc4ba6e48827295cf297625d92ae5495c0cdb87a46d3222`.
- Public metadata confirms active 1.0.0, both expected dependencies, Mods/AI Generated/Gear categories and NSFW off; moderation status is unreviewed. Actual live listing and disclosures inspected in Chromium.
- No GitHub push, live profile change, new game launch, server change or paused-project operation. Multiplayer, Windows, controllers, mod-manager ZIP import and uninstall/reinstall remain unverified and publicly disclosed.

### Source publication

- The owner subsequently requested the source push. Source, original assets, generators, test harness and release metadata are included; ignored builds, private evidence, game assemblies and saves are excluded.
- The immutable Thunderstore 1.0.0 README retains its accurate-at-packaging source-not-pushed disclosure. Source publication does not rebuild or replace the approved package.
