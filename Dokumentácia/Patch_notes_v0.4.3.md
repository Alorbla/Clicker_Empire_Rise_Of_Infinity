# Patch Notes v0.4.3

## Session 2026-02-15

### TownHall / Building Details (modulárne)
- Pridanı `DETAILS` flow v inspectore TownHallu (in-scene overlay, bez prepínania do novej scény).
- Pridanı `BuildingDetailsController` pre otvorenie/zatvorenie detail reimu, zobrazenie backgroundu a blokovanie world inputu poèas detailu.
- Pridanı full-screen `BuildingDetailsPanel` do UI Toolkit (`GameHUD.uxml`, `GameUI.uxml`) vrátane top baru a close tlaèidla.
- Opravené napojenie `TownHallDetailsButton` v `GameHUDController` (bind/unbind, vidite¾nos, handler).
- Rozšírené na modulárny systém pre všetky budovy:
  - `BuildingType` má nové pole `detailsBackgroundSprite`.
  - `BuildingType` má nové property `DetailsBackgroundSprite` a `HasDetailsView`.
  - `GameHUDController` drí `selectedBuildingType` a `DETAILS` button sa zobrazuje aj pre netownhall budovy, ak `HasDetailsView == true`.
  - `BuildingDetailsController` má generickú metódu `EnterBuildingDetails(BuildingType type)`.
- Nastavenie obrázkov je cez Inspector (iadne hardcoded cesty/názvy v kóde).

### Details UI content
- Do všetkıch detail view pridanı dolnı panel s textom:
  - `More Content Coming Soon`
- Pridané štıly v `GameHUD.uss`:
  - `.building-details-bottom-panel`
  - `.building-details-bottom-text`

### Hudba medzi scénami (IdleMain <-> WorldMap)
- Pridanı `PersistentMusicBootstrap`:
  - nájde `Music Player` v scéne,
  - spraví ho perzistentnım (`DontDestroyOnLoad`),
  - zabráni duplikátom po návrate do `IdleMain`,
  - hudba pokraèuje bez resetu tracku pri prepínaní scén.
- `IdleMain.unity`: na `Music Player` AudioSource vypnuté `Play On Awake`, aby sa eliminoval reštart/blip po loadnutí scény.
- Opravená compile chyba v bootstrapi:
  - `DontDestroyOnLoad` -> `Object.DontDestroyOnLoad`.

### Stabilita / cleanup
- Opravené viaceré rozbité literal newline artefakty (`` `r`n ``) v dotknutıch C#/UXML súboroch, ktoré spôsobovali zlepené riadky a compile problémy.
