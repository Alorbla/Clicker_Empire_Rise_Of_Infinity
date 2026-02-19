# Patch Notes v0.4.6

Dátum: 2026-02-17
Projekt: Idle_hra (Unity)

## Prehľad session
Session bola zameraná na TownHall progression loop: Talents UI, per-resource storage/capacity, Harmony indikátor, systém ér (Next Era), podmienky postupu a gating budov podľa éry. Súčasťou bol aj rebalance Village ekonomiky.

## UI a navigácia

- Pridané `Talents` tlačítko v menu (pod `WorldMap`) + Talents panel s backgroundom.
- Talents panel používa lock PanCamera controllerov (správanie ako pri details obrazovke).
- Doplnený `Harmony` ukazovateľ do top baru vedľa `Menu`.

### Dotknuté súbory

- `Idle_hra/Assets/UI Toolkit/GameHUD.uxml`
- `Idle_hra/Assets/UI Toolkit/GameUI.uxml`
- `Idle_hra/Assets/UI Toolkit/GameHUD.uss`
- `Idle_hra/Assets/Scripts/UI/GameHUDController.cs`

## Resource storage a resource bar

- Zrušený koncept globálneho storage capu; zavedený per-resource cap.
- `ResourceType` má `baseStorageCapacity`.
- `ResourceManager` používa `GetCapacity(type)` a `GetAvailableStorage(type)`.
- Resource bar zobrazuje `current/max` (napr. `0/500`, `0/1.5K`).
- Nastavené capy:
  - Food: 500
  - Wood: 1000
  - Stone: 1000
  - Gold: 1500

### Farebné pásma resource textov

- Zavedené 6 pásiem s thresholdmi (nastaviteľné v inspectore):
  - 0-20% žltá
  - 20-40% svetlo zelená
  - 40-60% modrá (harmony pásmo)
  - 60-80% svetlo zelená
  - 80-90% žltá
  - 90-100% červená

## Harmony systém

- Harmony je presunutá do samostatného skriptu na `UIDocument`:
  - `Idle_hra/Assets/Scripts/UI/HarmonyIndicatorController.cs`
- Výpočet Harmony je vážený podľa pásiem:
  - modrá = najvyššia váha,
  - zelená = stredná váha,
  - žltá = nízka váha,
  - červená = 0.
- Harmony label mení farbu podľa percent:
  - >=70% modrá
  - >=50% svetlo zelená
  - >=20% žltá
  - <20% červená

## Era progression (TownHall)

- Pridaný samostatný manager ér:
  - `Idle_hra/Assets/Scripts/Core/EraProgressionManager.cs`
- Podporené podmienky pre prechod do ďalšej éry:
  - `consumeOnAdvance` (suroviny sa minú),
  - `requiredToKeep` (suroviny sa neminú),
  - `requiredHarmonyPercent`.
- Podmienky sa vyhodnocujú voči cieľovej (next) ére.
- `currentEraIndex` sa persistuje v save/load:
  - `Idle_hra/Assets/Scripts/Core/SaveGameManager.cs`

### TownHall inspector

- Pridané tlačítko `Next Era`.
- Tlačítko je pod `BUILD MENU`; `DETAILS` je nad `Next Era`.
- Pridaná sekcia `Next Era Requirements`:
  - `Spend` (samostatný riadok + položky v ďalšom riadku),
  - `Have` (samostatný riadok + položky v ďalšom riadku),
  - Harmony požiadavka.
- Requirements ikony zväčšené na `32px`, texty zväčšené.
- Nadpis TownHall v inspectore je vo formáte `City - CurrentEra`.

## Building era gating

- Do `BuildingType` pridané era polia:
  - `requiredEraIndex`
  - `maxUnlockedDisplayLevelAtRequiredEra`
- Build menu karty sa zobrazujú len ak je splnený era requirement.
- Upgrade systém rešpektuje era lock (v prvej ére je možné max lvl 3 tam, kde je to nastavené).

### Dotknuté súbory

- `Idle_hra/Assets/Scripts/Buildings/BuildingType.cs`
- `Idle_hra/Assets/Scripts/Buildings/BuildingUpgradable.cs`
- `Idle_hra/Assets/Scripts/UI/GameHUDController.cs`

## Village rebalance (éra 0)

- Dostupné v prvej ére:
  - Lumberjack
  - Minercamp
  - Farm
  - Houses (na požiadanie povolené aj v ére 0)
- Town budovy + Houses gating boli upravené podľa era polí.
- Pre Lumberjack/Minercamp/Farm znížené build/upgrade ceny (lvl2-lvl3) a odstránené gold náklady z early upgradu.
- Pre tieto 3 budovy je v ére 0 aktívny lock na max lvl 3.
- Houses upravené tak, aby používali iba `Food` (build + upgrady + upkeep), bez timber/gold.

### Dotknuté assets

- `Idle_hra/Assets/Data/BuildingTypes/Village/Lumberjack.asset`
- `Idle_hra/Assets/Data/BuildingTypes/Village/Minercamp.asset`
- `Idle_hra/Assets/Data/BuildingTypes/Village/Farm.asset`
- `Idle_hra/Assets/Data/BuildingTypes/Village/Houses.asset`
- `Idle_hra/Assets/Data/BuildingTypes/Town/*.asset`

## Stav na konci session

- TownHall progression loop je funkčný: Harmony + Next Era + requirements + save/load éry.
- Build menu rešpektuje aktuálnu éru.
- Village má zmysluplný early-game flow a era gating.
- Houses v prvej ére fungujú len na Food ekonomike.
