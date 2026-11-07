# Changelog

## Unreleased

- Package the unchanged DLL under `plugins/Jarki-MobThreat/`. Remove older copies from `plugins/MobThreat/` when upgrading; DLL name, plugin ID and config path remain unchanged.

## 1.0.1

- Hide labels when damage is unknown, zero or otherwise has no finite hit estimate, instead of displaying `~? hits`.
- Hide previously visible pooled text when a mob's estimate becomes unknown; numeric estimates and warning colors are unchanged.

## 1.0.0

- Initial MobThreat package for the **Jarki** team.
- Packages the current-health hit-estimate overlay, including exercised ghost-label cleanup and scaled window-space positioning fixes.
- Damage estimation and tiny-DOT approximation are intentionally unchanged.
- Adds public installation, safety, testing-limit, licensing and AI-assistance disclosures plus an original package icon. Existing plugin identity, DLL name and version are retained.
