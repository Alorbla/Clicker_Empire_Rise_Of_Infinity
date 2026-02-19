# Patch Notes v0.4.5

Dátum: 2026-02-17
Projekt: Idle_hra (Unity)

## Preh¾ad session
Táto session obsahovala opravy produkcie, isometrického sortingu, ekonomickı rebalance budov/resources a viacero UI úprav Build Menu + Inspector + Market.

## Opravy gameplay logiky

- Opravenı Workshop, ktorı nevyrábal Timber.
- Potvrdenı a nastavenı pomer Workshop konverzie: `2 Wood = 1 Timber`.
- Opravené správanie `SortByY` pre TownHall (TownHall u nie je vdy navrchu).
- Opravené poradie renderingu pod¾a poiadavky zo screenshotu (`sorthall.png`), aby kostol (church) bol vpredu, keï má by.

## Ekonomika a balancing (buildings + upgrades do L5)

Prebehla kompletná rebalance ekonomiky pod¾a poadovaného progression flow:

- Štart: manuálne klikanie `Wood`, `Stone`, `Food`.
- Následne `Gold` cez `Market`.
- Potom `Workshop` (Timber + manual click) a `Stonecutter` (Ashlar).
- `Timber`/`Ashlar` ako gate pre neskoré budovy (`Amphitheatre`, `Library`, `Church`).
- `Houses` bez Timber/Ashlar v základe, ale s pouitím vo vyšších upgrade leveloch.
- `Barracks` vyaduje Timber/Ashlar u v základe.

### Konkrétne upravené assets

- `Idle_hra/Assets/Data/BuildingTypes/Lumberjack.asset`
- `Idle_hra/Assets/Data/BuildingTypes/Minercamp.asset`
- `Idle_hra/Assets/Data/BuildingTypes/Farm.asset`
- `Idle_hra/Assets/Data/BuildingTypes/Market.asset`
- `Idle_hra/Assets/Data/BuildingTypes/Workshop.asset`
- `Idle_hra/Assets/Data/BuildingTypes/Stonecutter.asset`
- `Idle_hra/Assets/Data/BuildingTypes/Houses.asset`
- `Idle_hra/Assets/Data/BuildingTypes/Barracks.asset`
- `Idle_hra/Assets/Data/BuildingTypes/Amphitheatre.asset`
- `Idle_hra/Assets/Data/BuildingTypes/Library.asset`
- `Idle_hra/Assets/Data/BuildingTypes/Church.asset`
- `Idle_hra/Assets/Data/BuildingTypes/Storage.asset`

### Dôleité vısledky balancu

- Workshop drí konverznı pomer 2:1 naprieè levelmi.
- Stonecutter drí konverznı pomer 3:1 (Stone -> Ashlar).
- Ceny a upgrady sú nastavené tak, aby hra nebola príliš ¾ahká ani príliš grindy.

## Build Menu / TownHall UI úpravy

### Build cards

- Karta bola upravená pre lepšiu èitate¾nos nákladov.
- Názov budovy je väèší a centrovanı.
- Ikony nákladov v Build Menu sú nastavené na `32px`.
- Zavedené dynamické prispôsobenie vıšky karty pod¾a poètu surovín:
  - menej surovín = nišia karta,
  - viac surovín = vyššia karta,
  - funguje aj pre 6+ a 8+ ikon.
- Nastavené maximum 3 karty v riadku.
- Opravenı spacing medzi kartami v riadku (stabilné správanie cez code fallback, nie iba USS gap).

### Dotknuté UI súbory

- `Idle_hra/Assets/UI Toolkit/BuildCard.uss`
- `Idle_hra/Assets/UI Toolkit/GameHUD.uss`
- `Idle_hra/Assets/Scripts/UI/GameHUDController.cs`

## Inspector + Market ikony

- V inšpektore upgradov (`inspector-cost-icon`) nastavené ikony na `32px`.
- Upravené aj vıšky riadku/itemu, aby sa ikony neorezávali.
- V Markete (`trade-icon`) tie nastavené ikony na `32px`.

## Technické poznámky

- Poèas úprav prebehli viaceré korekcie USS/C# formátovania kvôli newline token artefaktom; finálne bloky sú validné a preèistené.
- V aktuálnom pracovnom prieèinku nebol dostupnı `.git` repozitár, take zmeny boli verifikované priamou kontrolou súborov.

## Stav na konci session

- Workshop produkcia funguje.
- Sorting TownHall/Church správanie opravené.
- Economy rebalance aplikovanı.
- Build Menu karty fungujú pod¾a poiadaviek (dynamická vıška, max 3 na riadok, vidite¾nı spacing).
- Inspector aj Market pouívajú 32px resource ikony.
