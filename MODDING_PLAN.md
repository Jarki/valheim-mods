# Firearm expansion — local development

**Paused by the owner (2026-11-05).** Preserve this historical plan and assets;
do not resume firearm development, testing or publication without an explicit
owner decision. MobThreat is the only active development/publication target.

- Engine/route: Unity Mono 6000.0.75f1 confirmed from the verified reference download; BepInEx 5.4.2351 + Jotunn 2.30.2.
- Host: Arch Linux x86_64 VM, QEMU CPU exposing SSE4.2 but no AVX. GTX 1050 Mobile enumerates; X11 :1 actually uses accelerated virgl/Intel UHD 620. Latest native ILSpy and packaged Blender hit instruction-set incompatibilities; managed ILSpy 9 and verified portable Blender 3.6.23 both work.
- Game: Valheim Steam app 892970, owned account login required for client download. Steam installed and self-updating. Public server distribution 896660 was downloaded/validated for read-only references; never launched.
- Development assets: original Blender-authored meshes exported to a small runtime mesh format, embedded recorded WAVs with attribution, procedural skeletal arm poses integrated after vanilla animation. Avoid requiring Unity Editor licensing for this pipeline.
- Gameplay: maintain semi-auto pistol; add automatic AKM and single-shot RPG. Use native projectile collision/damage handling where possible. Respect ward access for splash damage and normal PVP rules.
- Lab: `.dev/pistol/profile` and isolated config/saves. Back up before launching. Disposable local worlds only.
- Production deployment, publishing and upstream submissions remain outside scope.
