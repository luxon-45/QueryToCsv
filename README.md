# QueryToCsv

A CLI tool that connects to Microsoft SQL Server, executes `.sql` files, and exports the results as CSV.

## Features

- Interactive query selection from a folder of `.sql` files
- Streaming execution via `SqlDataReader` (constant memory usage regardless of result size)
- RFC 4180 compliant CSV output (powered by CsvHelper)
- Configurable delimiter, null representation, newline, and date format
- Choice of CSV encoding: UTF-8, UTF-8 with BOM, UTF-16 LE, Shift-JIS
- Optional header row
- SELECT-only enforcement (INSERT, UPDATE, DELETE, and other non-SELECT statements are rejected)
- Logging to file with daily rotation and configurable retention
- Self-contained single-file executable (.NET runtime not required)

## Requirements

- Windows x64
- Microsoft SQL Server (any supported version)
- .NET 10.0 SDK (for building from source)

## Download

Pre-built binaries are available on the [Releases](https://github.com/luxon-45/QueryToCsv/releases/latest) page.

| File | Description |
|------|-------------|
| `QueryToCsv-Setup-*.exe` | Installer (creates folders, optional PATH registration) |
| `QueryToCsv.exe` | Standalone executable |

## Getting Started

### 1. Build (from source)

Run `Build/Menu.bat` and select **Build** from the menu. This produces a self-contained executable in `Build/QueryToCsv/`.

Alternatively, build directly:

```
dotnet publish QueryToCsv/QueryToCsv.csproj -c Release -f net10.0 -r win-x64 --self-contained true -p:PublishSingleFile=true
```

### 2. Configure

Copy `appsettings.sample.json` to `appsettings.json` in the same directory as the executable, then edit the connection string and paths.

```
copy appsettings.sample.json appsettings.json
```

#### Connection String

**SQL Server Authentication** (username and password):

```json
{
  "ConnectionString": "Server=myserver;Database=mydb;User Id=myuser;Password=mypassword;TrustServerCertificate=True;"
}
```

**Windows Authentication** (uses the current Windows login):

```json
{
  "ConnectionString": "Server=myserver;Database=mydb;Integrated Security=True;TrustServerCertificate=True;"
}
```

> With Windows Authentication, `User Id` and `Password` are not needed. The tool connects using the credentials of the Windows user running the application.

### 3. Add SQL Files

Place `.sql` files in the `queries/` folder (or whichever folder `QueryFolder` points to). Only SELECT statements are allowed — the tool will reject files containing INSERT, UPDATE, DELETE, DROP, or other data-modifying statements.

### 4. Run

```
QueryToCsv.exe
```

### Open Folders / Config

The `--open` option opens folders or files directly from the command line, useful when the install directory is not easily accessible (e.g., `%LOCALAPPDATA%\Programs\QueryToCsv`).

```
QueryToCsv --open queries   # Open queries folder in Explorer
QueryToCsv --open output    # Open output folder in Explorer
QueryToCsv --open config    # Open appsettings.json in default editor
QueryToCsv --open log       # Open logs folder in Explorer
```

The application exits immediately after opening the target. If the target does not exist, an error message is displayed (exit code 1).

## Usage Example

```
=== QueryToCsv ===

=== Select a query ===
1. sales_report.sql
2. user_list.sql

Enter number: 1

Include header row? (y/n): y

=== Select encoding ===
1. UTF-8
2. UTF-8 with BOM
3. UTF-16 LE
4. Shift-JIS

Enter number: 2

Connecting...
Executing query...
Writing CSV...

Done: C:\Users\you\AppData\Local\Programs\QueryToCsv\output\sales_report_20260302_153045.csv
Rows: 1,234
```

## Configuration Reference

All settings are in `appsettings.json`.

| Key | Type | Required | Default | Description |
|-----|------|----------|---------|-------------|
| `ConnectionString` | string | Yes | - | SQL Server connection string |
| `QueryFolder` | string | Yes | - | Folder containing `.sql` files |
| `OutputFolder` | string | Yes | - | Folder for CSV output |
| `QueryTimeout` | int | No | `30` | Query timeout in seconds (must be > 0) |
| `SqlFileEncoding` | string | No | `"UTF-8"` | Encoding for reading `.sql` files |
| `LogRetentionDays` | int | No | `30` | Number of days to keep log files |
| `CsvSettings.Delimiter` | string | No | `","` | Single character. Use `"\t"` for tab |
| `CsvSettings.NullValue` | string | No | `""` | String to output for SQL NULL values |
| `CsvSettings.NewLine` | string | No | `"CRLF"` | `"CRLF"` or `"LF"` |
| `CsvSettings.DateFormat` | string | No | `null` | Date format string (e.g. `"yyyy-MM-dd HH:mm:ss"`) |

### Path Resolution

Relative paths in `QueryFolder` and `OutputFolder` are resolved relative to the executable's directory.

### Full Example

```json
{
  "ConnectionString": "Server=localhost;Database=SalesDB;Integrated Security=True;TrustServerCertificate=True;",
  "QueryFolder": "./queries",
  "OutputFolder": "./output",
  "QueryTimeout": 60,
  "SqlFileEncoding": "UTF-8",
  "LogRetentionDays": 30,
  "CsvSettings": {
    "Delimiter": "\t",
    "NullValue": "NULL",
    "NewLine": "CRLF",
    "DateFormat": "yyyy-MM-dd HH:mm:ss"
  }
}
```

## CSV Output

### File Naming

Output files are named `{query}_{timestamp}.csv`:

```
sales_report_20260302_153045.csv
```

If a file with the same name already exists, a suffix is appended: `_2`, `_3`, etc.

### Format

- **Standard**: RFC 4180 compliant
- **Quoting**: Fields containing the delimiter, newlines, or double quotes are enclosed in double quotes
- **Escaping**: Double quotes within fields are escaped as `""`
- **Numbers**: Formatted with `InvariantCulture` (decimal point `.`, no thousands separator)
- **Dates**: Formatted with `DateFormat` if specified, otherwise `InvariantCulture` default
- **NULL**: Replaced with the configured `NullValue` (empty string by default)
- **Empty results**: Outputs header-only CSV (if headers enabled) or an empty file

### Encoding Options

| Option | Description |
|--------|-------------|
| UTF-8 | No BOM. Universal standard |
| UTF-8 with BOM | Recommended when opening CSV in Excel |
| UTF-16 LE | For Windows tool integration |
| Shift-JIS | For Japanese legacy systems |

## Error Handling

| Scenario | Behavior |
|----------|----------|
| `appsettings.json` missing | Error message, exit code 1 |
| Invalid JSON in config | `Error: Failed to load appsettings.json.`, exit code 1 |
| Invalid config values | `Error: <detail>`, exit code 1 |
| `QueryFolder` does not exist | Error message, exit code 1 |
| No `.sql` files found | `No query files found.`, exit code 1 |
| `OutputFolder` does not exist | Automatically created |
| Connection failure | `Error: <detail>`, exit code 1 |
| SQL execution error | `Error: <detail>`, exit code 1 |
| Query timeout | `Error: Query timed out.`, exit code 1 |
| Non-SELECT statement detected | `Error: Only SELECT statements are allowed.`, exit code 1 |
| Query returns 0 rows | CSV written (empty or header-only), exit code 0 |

### Exit Codes

| Code | Meaning |
|------|---------|
| `0` | Success |
| `1` | Error |

## Building the Installer

Requires [Inno Setup 6](https://jrsoftware.org/isinfo.php).

1. Run `Build/Menu.bat`
2. Select **Build + Create Installer**

The installer:
- Installs to `%LOCALAPPDATA%\Programs\QueryToCsv` (per-user, no admin required)
- Creates `queries/` and `output/` folders
- Optionally adds the install directory to user `PATH`
- Preserves `appsettings.json` on upgrades (only created on first install)
