# MaDB

MaDB is a database management toolkit built with C# and .NET.

Current workspace includes:
- `MaDB.Core`: provider-based abstraction layer
- `MaDB.Cli`: command-line interface

GUI (`Avalonia`) has a starter scaffold in `MaDB.Desktop` and is planned as a staged follow-up.

## Goals

- Unified multi-database access model
- Provider-based extensibility
- Operational safety via `readonly` / `readwrite` modes
- AI-friendly output formats (`table`, `json`, `jsonl`)

## Project Structure

- `MaDB.Core`
  - Core contracts:
    - `IDatabaseProvider`
    - `IQueryExecutor`
    - `ISchemaReader`
    - `IDatabaseImportExportService`
  - Provider registry:
    - `DatabaseProviderRegistry`
  - Built-in providers:
    - `Sqlite` (implemented)
    - `MySql` (skeleton)
    - `PostgreSql` (skeleton)

- `MaDB.Cli`
  - Interactive CLI (`ma>`)
  - Connection/session management
  - Query/script/schema/import/export operations

- `MaDB.Desktop`
  - Avalonia application shell
  - MVVM base/view locator setup
  - Main window and view model placeholder

## Desktop Roadmap

The desktop app will evolve in small, testable stages:

1. Workspace shell
  - Connection sidebar
  - Active session summary
  - Readonly/readwrite mode indicator

2. Query workspace
  - SQL editor with execution controls
  - Result grid and structured output viewer
  - Row cap and format switching aligned with CLI output modes

3. Schema explorer
  - Tables, views, and column metadata
  - Search/filter for schema objects
  - Object detail panel with quick actions

4. Import/export tools
  - Script import and SQL export flows
  - Provider-aware safety checks
  - Progress and error reporting for long-running tasks

5. Provider expansion
  - Reuse `MaDB.Core` abstractions across SQLite, MySQL, and PostgreSQL
  - Add provider-specific connection and capability indicators
  - Keep UI behavior consistent across dialects

## Build

```bash
dotnet build MaDB.Core/MaDB.Core.csproj
dotnet build MaDB.Cli/MaDB.Cli.csproj
```

## Run CLI

```bash
dotnet run --project MaDB.Cli
```

You can also run a single command:

```bash
dotnet run --project MaDB.Cli -- "ma status"
```

## CLI Commands

```text
ma connect <sqlite|mysql|pgsql> <target> [readonly|readwrite]
ma mode readonly|readwrite
ma format table|json|jsonl
ma query "<sql>"
ma exec "<script-file.sql>"
ma tables
ma schema
ma export "<output-file.sql>"
ma import "<input-file.sql>"
ma status
ma exit
```

### Notes

- SQLite `target` is a DB file path, e.g. `test.db`
- MySQL/PostgreSQL providers are currently skeletons; executor/schema/transfer are not implemented yet
- `readonly` mode blocks write operations (including import and non-query write SQL)

## Output Modes

- `table`: human-readable
- `json`: single structured JSON object
- `jsonl`: one JSON object per line (stream-friendly)

Query output is capped at **100 rows** in all output modes.

## Schema & Import/Export

`MaDB.Core` includes generic abstractions:
- Schema reading: `ISchemaReader`
- Import/export: `IDatabaseImportExportService`

SQLite implementation currently supports:
- Reading table/view and column metadata
- SQL export (DDL + data inserts)
- SQL import (script execution)

## Extending a New Provider

To add a new database provider:

1. Implement `IDatabaseProvider`
2. Implement and wire:
   - `IQueryExecutor`
   - `ISchemaReader`
   - `IDatabaseImportExportService`
3. Register the provider in CLI startup (`DatabaseProviderRegistry`)
4. Add dialect parsing alias in CLI if needed

## Line Endings & Repository Hygiene

- `.gitattributes` enforces normalized line endings (`LF` by default)
- `.editorconfig` enforces `end_of_line = lf`
- `.gitignore` excludes build artifacts and local DB files
