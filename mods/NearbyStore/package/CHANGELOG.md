# Changelog

## 1.0.0

- Initial NearbyStore: inventory Store nearby button, 10m configurable search, matching-chest priority and free-space fallback.
- Left Alt + F favorites hovered player-inventory stacks; gold F markers; favorites, equipped, queued-for-equip and quest items stay carried.
- Uses vanilla chest ownership requests with privacy, ward and busy checks; skips tombstones and dropped loot.
- Waits for ownership synchronization after access grants; already-owned chests need no request. Timed-out remote requests remain safely quarantined; normal-open recovery is documented.
- Native-font UI initialization avoids loading missing default font assets. Removed an unused tooltip component without its required prefab.
- 13 standalone cases, 3 adversarial groups (20,480 generated deposits) and 38 native-client checks pass. Real keyboard/mouse workflow, character-file favorite persistence and 70%/130% UI scale were exercised.
- Multiplayer, denied wards, uninstall/reinstall and mod-manager ZIP-import UI remain unverified.
