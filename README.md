# RimStats

**RimStats** is a RimWorld mod that keeps a detailed history of your playthrough, saving key events and economic indicators directly into a local SQLite database.

## Key Features

* **Automated Statistics Collection**: The mod automatically records colony status (wealth, colonist count) at a configurable interval.
* **Event Logging**: Tracks important gameplay moments, including:
    * Colonist deaths.
    * Raid arrivals.
    * Successful colonist recruitment.
    * Trade deals.
* **SQLite Database**: All data is saved to a local `Data.db` file in your RimWorld config folder, ensuring reliability and accessibility for external data processing.
* **History Integration**: RimStats automatically creates RimWorld History AutoRecorder groups, allowing you to view wealth graphs directly in-game.

## Settings
You can configure the mod in **Options -> Mod Settings -> RimStats**:
* Enable/disable statistics collection.
* Adjust the data recording interval (in days).
* Enable/disable event logging.
* Logging (Debug mode).

## Data Location
The database is located at:
`.../Ludeon Studios/RimWorld by Ludeon Studios/Config/RimStats/Data.db`.

## For Developers
* **SQLite Integration**: The mod uses `Microsoft.Data.Sqlite`[cite: 11]. To ensure cross-platform compatibility, native libraries (`e_sqlite3.dll`/`.so`/`.dylib`) are loaded dynamically from the mod's `/Bin` folder[cite: 11].
* **Data Structure**: The database uses two main tables: `Events` and `Stats`[cite: 11]. Each entry is tied to the `randSeed` of the specific world, preventing data from different playthroughs from mixing[cite: 11, 12].
* **Harmony**: Used to patch game events (death, raids, trading, recruitment)[cite: 13].
* **Building the project**: To add extra functionality or modify the code, navigate to the main directory of the project and use the .NET SDK tools to build it with the following command:
```bash
dotnet build -c Release
```

## Requirements
* RimWorld 1.6 (tested on this version).
* Basic file system access to reach the `Data.db` file.
