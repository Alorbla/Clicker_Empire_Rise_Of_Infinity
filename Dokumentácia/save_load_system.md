# Save/Load System Session Notes

Date: 2026-02-15

## What was implemented in this session

1. Added persistent save system core
- Created `SaveGameManager` as `DontDestroyOnLoad` singleton.
- Save file path uses `Application.persistentDataPath`.
- Added primary + backup JSON save files.
- Added periodic autosave and save on pause/quit.

2. Runtime state persistence for Idle scene
- Resources + storage capacity
- Purchased research IDs
- Purchased blessing IDs
- Placed buildings
- Town hall custom name

3. Scene transition save/load flow
- Saving is triggered before switching from HUD/WorldMap.
- On loading Idle scene (`IdleMain` / `idle_main`), saved state is applied automatically.

4. Tutorial persistence fix
- Tutorial completion is persisted.
- After finishing tutorial once, it no longer auto-restarts on return to Idle scene.

5. Runtime menu in HUD
- Added top-bar `Menu` button.
- Added centered runtime menu panel.
- Added `Reset Progress` button in menu.
- Added `Close Menu` button (`X`) in top-right corner of menu card.

6. Reset progress at runtime
- `ResetProgressAndRestart()` deletes save files and related legacy/tutorial PlayerPrefs keys.
- Scene reloads to allow clean fresh start during Play Mode.

7. WorldMap persistence by seed (latest update)
- `SaveGameManager.SaveData` now stores:
  - `hasWorldMapSeed`
  - `worldMapSeed`
- Added API:
  - `TryGetWorldMapSeed(out int seed)`
  - `SetWorldMapSeed(int seed)`
- `WorldMapManager.GenerateMap()` now:
  - Uses saved seed if available.
  - Falls back to `WorldMapConfig` seed logic if no saved seed exists.
- After map generation, effective seed is saved immediately (`SetWorldMapSeed` + `SaveNow`).
- Result: switching between `WorldMap` and `IdleMain` no longer regenerates a different map unless progress is reset.

## Notes / expected behavior

- If `WorldMapConfig.useRandomSeed` is enabled, random seed is used only for first generation when no saved world seed exists.
- Once generated, world map should stay stable across scene switches due to saved seed.
- `Reset Progress` should clear both Idle progress and persisted world seed.

## Follow-up ideas

- Store full world map tile data snapshot (optional), not only seed, if deterministic generation ever changes.
- Add versioned migration in save schema when introducing new persistent fields.
