# Changelog

## 0.1.4

- Add a small treadmill screen showing only cumulative kilometres and approximate calories burned in this server/world; use a fixed estimate of 70 kcal per kilometre.
- Preserve separate totals per character and world across workouts and character saves. Recovery walking does not add running distance; previous workouts are not backfilled.

## 0.1.3

- Replace the main package icon and inherited wood-floor build icon with the original Valheim-style raised-belt treadmill artwork.
- Refresh the native recipe screenshot to show the treadmill icon while retaining all ingredient amounts and the workbench requirement.
- Keep the in-game icon inset below the native recipe title strip, with transparent padding and full-rectangle sprite geometry so the artwork stays inside its allotted slot.

## 0.1.2

- Stay attached and walk in place on treadmill exhaustion; automatically resume running only when native stamina reaches its current maximum.
- Pause Run XP, running stamina costs and sprint strain during recovery walking; heal strain as during ordinary walking. Existing dismount and cancellation controls remain available.
- Rebalance default strain onset from 90 to 60 sprint seconds at Run 0 and recovery from 180 to 600 non-sprinting seconds per load unit, so ordinary sprint breaks no longer erase fatigue as easily. Skill scaling, Run-75 immunity and severity penalties remain unchanged.
- Preserve existing config values; set `SecondsPerLevel = 60` and `RecoverySecondsPerLevel = 600` manually to adopt the new balance.
- Increase default treadmill `StrainBuildupMultiplier` from 1 to 2: active treadmill running builds strain twice as fast as ordinary sprinting, without changing recovery or severity penalties. Existing configs must set `StrainBuildupMultiplier = 2` under `[Treadmill]` to adopt it.

## 0.1.1

- Keep active treadmill training running while inventory is open, including stamina consumption, Run XP and muscle strain.
- Preserve cancellation for other blocking interfaces, damage, exhaustion and deliberate dismounting.

## 0.1.0

- Add a buildable wood-frame treadmill with a moving deer-hide belt, using Wood, Core wood, Bronze nails, Deer hide and Leather scraps.
- Train Run in place with a default 1.5× XP multiplier and native stamina consumption; exhaustion ends training.
- Add three persistent running muscle-strain levels with +15%, +30% and +50% running stamina costs, skill-dependent buildup and gradual recovery.
- Add configurable treadmill XP, stamina-cost and strain-buildup multipliers, strain onset, recovery speed and strain-free Run threshold.
