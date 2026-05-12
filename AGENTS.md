# MaDB Agent Guidelines

## Quick Commands

```bash
# Build
dotnet build MaDB.Core/MaDB.Core.csproj
dotnet build MaDB.Cli/MaDB.Cli.csproj
dotnet build MaDB.Desktop/MaDB.Desktop.csproj

# Run CLI (interactive)
dotnet run --project MaDB.Cli

# Run CLI (single command)
dotnet run --project MaDB.Cli -- "ma status"
dotnet run --project MaDB.Cli -- "ma connect sqlite test.db"
dotnet run --project MaDB.Cli -- "ma query \"SELECT * FROM users\""

# Run Desktop
dotnet run --project MaDB.Desktop
```

## Architecture

**3-project solution** (net10.0):
- `MaDB.Core` - Provider abstractions + SQLite/MySQL implementations
- `MaDB.Cli` - Interactive CLI (`ma>` prompt) + single-command mode
- `MaDB.Desktop` - Avalonia GUI (MVVM, CommunityToolkit.Mvvm)

**Core contracts** (`MaDB.Core`):
- `IDatabaseProvider` → factory for all provider services
- `IQueryExecutor` - ExecuteNonQuery/Query/QueryStream + transactions
- `ISchemaReader` - Database metadata
- `IDatabaseImportExportService` - SQL import/export
- `DatabaseProviderRegistry` - Resolves provider by `DatabaseDialect`

**Provider implementations**:
- SQLite: Full (query, schema, import/export) - uses `Microsoft.Data.Sqlite`
- MySQL: Full (query, schema, import/export) - uses `MySqlConnector`
- PostgreSQL: Skeleton only (throws `NotSupportedException`)

## Key Patterns

**Adding a new provider**:
1. Create `MaDB.Core/{ProviderName}/{ProviderName}DatabaseProvider.cs`
2. Implement `IDatabaseProvider` with dialect-specific validation
3. Implement `IQueryExecutor`, `ISchemaReader`, `IDatabaseImportExportService`
4. Register in `MaDB.Cli/Program.cs` startup

**CLI connection syntax**:
```
ma connect sqlite <file.db> [readonly|readwrite]
ma connect mysql <host:port/db> [readonly|readwrite]
ma connect pgsql <connection-string> [readonly|readwrite]
```

**Output modes**: `table` (default), `json`, `jsonl` (stream-friendly)

**Row cap**: Query output limited to 100 rows in all modes

## Conventions

- C# records for data models (`sealed record`)
- Primary constructors for service classes
- `async/await` throughout, `CancellationToken` passed explicitly
- SQLite: `SqliteOpenMode.ReadOnly` for read-only access
- MySQL: `MySqlConnectionStringBuilder` for connection strings
- Parameters use `@` prefix, auto-prefixed if missing
- `.editorconfig`: LF line endings, 4-space indent, UTF-8
- No CI/CD configured yet

## Commit Message Format

```
<type>(<scope>): <description>
```

**Type**: `feat`, `fix`, `docs`, `refactor`, `test`, `chore`

**Scope**: `core`, `cli`, `desktop` (matches project name)

**Examples**:
- `feat(core): add PostgreSQL provider implementation`
- `fix(cli): handle empty connection string`
- `feat(desktop): add schema explorer panel`
- `refactor(core): extract common SQL parsing logic`
- `docs: update README with new CLI commands`
