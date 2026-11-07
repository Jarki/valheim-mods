# Changelog

## Unreleased

- Package the unchanged DLL under `plugins/Jarki-Rotheart/`. Remove older copies from `plugins/Rotheart/` when upgrading; DLL name, plugin ID, prefab identity and save data remain unchanged.

## 1.0.0

- Add Rotheart, a forge-crafted swamp-tier trinket made from Ooze, Guck, Entrails and Iron.
- Grant native 50% poison resistance while equipped, without weakening stronger poison-resistance mead.
- Activate a 10-second miasma automatically at 10 adrenaline: five pulses of 12 base blunt damage within 6 metres, costing 1 wearer health per pulse with a 1-HP floor.
- Exclude other players, tamed creatures, friendly factions, structures and terrain. Stop the aura on unequip/death; refresh without stacking.
- Include an original iron-caged ooze-heart model, inventory/package icons, hip attachment and green aura particles.
- Verify 65 native-client assertions, persistence and actual visuals on Valheim l-1.0.20. Remote multiplayer, dedicated servers and mod-manager ZIP import remain unverified.
