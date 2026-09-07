# QueryToCsv

A CLI tool that connects to Microsoft SQL Server, executes `.sql` files, and exports the results as CSV.

## Features

- Interactive query selection from a folder of `.sql` files, or direct SQL input from the console
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

Pre-built binaries are available on the [Releases](https://github.com/elysion-ii/QueryToCsv/releases/latest) page.

| File | Description |
|------|-------------|
| `QueryToCsv-Setup-*.exe` | Installer (creates folders, optional PATH registration) |
| `QueryToCsv.exe` | Standalone executable |

## Getting Started

### 1. Build (from source)

Run `build/Menu.bat` and select **Build** from the menu. This produces a self-contained executable in `build/QueryToCsv/`. The build runs a configuration-file check, a code format check, and the test suite first, and stops if any of them fails.

### 2. Configure

Copy `appsettings.template.json` to `appsettings.json` in the same directory as the executable, then edit the connection string and paths.

```
copy appsettings.template.json appsettings.json
```

#### Connection Strings

Define one or more named connections in the `Connections` array. At startup, you choose which connection to use (or it is selected automatically if only one is defined).

**SQL Server Authentication** (username and password):

```json
{
  "Connections": [
    { "Name": "Dev Server", "ConnectionString": "Server=192.168.1.10;Database=SalesDB;User Id=myuser;Password=mypassword;TrustServerCertificate=True;" },
    { "Name": "Prod Server", "ConnectionString": "Server=sql-prod.local;Database=OrderDB;User Id=myuser;Password=mypassword;TrustServerCertificate=True;" }
  ]
}
```

**Windows Authentication** (uses the current Windows login):

```json
{
  "Connections": [
    { "Name": "Local", "ConnectionString": "Server=myserver;Database=mydb;Integrated Security=True;TrustServerCertificate=True;" }
  ]
}
```

> With Windows Authentication, `User Id` and `Password` are not needed. The tool connects using the credentials of the Windows user running the application.

### 3. Add SQL Files

Place `.sql` files in the `queries/` folder (or whichever folder `QueryFolder` points to). Only SELECT statements are allowed — the tool will reject files containing INSERT, UPDATE, DELETE, DROP, or other data-modifying statements.

### 4. Run

```
QueryToCsv.exe
```

### Cancelling

To exit the application at any time, press **Ctrl+C**.

At any input prompt, **Ctrl+Z** followed by Enter also exits (exit code 1). Exception: in direct SQL input mode, Ctrl+Z ends the SQL entry and proceeds with execution instead of quitting.

### One-Liner Mode

Run a query non-interactively with CLI options. Useful for scripts, scheduled tasks, and quick one-off commands.

Inline query with defaults (single connection, UTF-8, with header):

```
QueryToCsv --query "SELECT * FROM Users"
```

SQL file with defaults:

```
QueryToCsv -f sales_report.sql
```

All options specified:

```
QueryToCsv -c "Dev Server" --query "SELECT * FROM Users WHERE Active = 1" --no-header -e utf-8-bom
```

| Option | Long | Required | Default | Description |
|--------|------|----------|---------|-------------|
| `-c` | `--connection` | If multiple connections | Auto-select if only one | Connection name from appsettings.json |
| - | `--query` | One of `--query`/`--file` | - | Inline SQL string |
| `-f` | `--file` | One of `--query`/`--file` | - | SQL file name (resolved in QueryFolder) or absolute path |
| `-e` | `--encoding` | No | `utf-8` | CSV encoding: `utf-8`, `utf-8-bom`, `utf-16`, `shift-jis` |
| | `--header` | No | (default) | Include header row |
| | `--no-header` | No | - | Exclude header row |

Long-form values accept both `--name value` and `--name=value`. The `-q` short name is
not accepted because it is reserved for quiet mode. When `--query` or `--file` is
present, the tool runs in one-liner mode and skips all interactive prompts. With no
arguments, the tool runs in interactive mode. Supplying one-liner options without either
`--query` or `--file` is a usage error.

### Help

```
QueryToCsv -h
QueryToCsv --help
```

### Version

```
QueryToCsv -V
QueryToCsv --version
```

Both forms print `QueryToCsv X.Y.Z` and exit without loading configuration.

### Open Folders / Config

The `--open` option opens folders or files directly from the command line, useful when the install directory is not easily accessible (e.g., `%LOCALAPPDATA%\Programs\QueryToCsv`).

```
QueryToCsv --open queries              # Open queries folder in Explorer
QueryToCsv --open output               # Open output folder in Explorer
QueryToCsv --open config               # Open appsettings.json in default editor
QueryToCsv --open log                  # Open logs folder in Explorer
QueryToCsv --open "C:\path\to\file.csv"  # Open a specific file with its default app
```

The application exits immediately after opening the target. If the target does not exist, an error message is displayed (exit code 1).

## Usage Example

### File Selection Mode

```
=== QueryToCsv ===

=== Select connection ===
1. Dev Server (192.168.1.10 - SalesDB)
2. Prod Server (sql-prod.local - OrderDB)

Enter number: 1

=== Select a query ===
0. Enter query directly
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

### Direct Input Mode

Select `0` to enter SQL directly from the console. End the input with Ctrl+Z.

```
Enter number: 1

Enter number: 0

Enter SQL query (end with Ctrl+Z):
  > SELECT TOP 10
  > *
  > FROM Users
  > ^Z

Include header row? (y/n): y

...

Done: C:\Users\you\AppData\Local\Programs\QueryToCsv\output\20260302_153045.csv
Rows: 10
```

## Configuration Reference

All settings are in `appsettings.json`.

| Key | Type | Required | Default | Description |
|-----|------|----------|---------|-------------|
| `Connections[]` | array | Yes | - | Array of named connection entries |
| `Connections[].Name` | string | Yes | - | Display name shown in the selection menu |
| `Connections[].ConnectionString` | string | Yes | - | SQL Server connection string |
| `QueryFolder` | string | No | `queries` next to the executable | Folder containing `.sql` files |
| `OutputFolder` | string | No | `output` next to the executable | Folder for CSV output |
| `QueryTimeout` | int | No | `30` | Query timeout in seconds (must be > 0) |
| `SqlFileEncoding` | string | No | `"UTF-8"` | Encoding for reading `.sql` files |
| `LogRetentionDays` | int | No | `30` | Number of days to keep log files |
| `CsvSettings.Delimiter` | string | No | `","` | Single character. Use `"\t"` for tab |
| `CsvSettings.NullValue` | string | No | `""` | String to output for SQL NULL values |
| `CsvSettings.NewLine` | string | No | `"CRLF"` | `"CRLF"` or `"LF"` |
| `CsvSettings.DateFormat` | string | No | `null` | Date format string (e.g. `"yyyy-MM-dd HH:mm:ss"`) |

### Path Resolution

Relative paths in `QueryFolder` and `OutputFolder` are resolved relative to the executable's directory.

### Unusable Values

`Connections` is the only required setting: without one, QueryToCsv has no server to
query and stops. Every other setting has a built-in default and falls back to it when
the file is missing, its JSON cannot be parsed, or the value is unusable. Each rejected
value is reported once on standard error and written to the log, naming the setting, the
value that was rejected, and the default applied — the run then continues on that
default.

### Full Example

```json
{
  "Connections": [
    { "Name": "Dev Server", "ConnectionString": "Server=192.168.1.10;Database=SalesDB;User Id=myuser;Password=mypassword;TrustServerCertificate=True;" },
    { "Name": "Prod Server", "ConnectionString": "Server=sql-prod.local;Database=OrderDB;Integrated Security=True;TrustServerCertificate=True;" }
  ],
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

For direct input mode, the query name is omitted and the file is named `{timestamp}.csv`:

```
20260302_153045.csv
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
| `appsettings.json` missing or unparseable | Reported as a warning; the run continues on the built-in defaults |
| Unusable config value | Reported as a warning naming the setting and the default applied; the run continues |
| No connection configured | `QueryToCsv: Connections must contain at least one entry.`, exit code 1 |
| Connection entry left incomplete | `QueryToCsv: Connections[<index>].Name is required.` (or `.ConnectionString`), exit code 1 |
| `QueryFolder` does not exist | Reported as a warning; `queries` next to the executable is used |
| No `.sql` files found | Only "Enter query directly" (option 0) is shown; direct input is still available |
| `OutputFolder` does not exist | Automatically created |
| Connection or SQL execution failure | `QueryToCsv: query execution failed.`, exit code 1 |
| Query timeout | `QueryToCsv: query timed out.`, exit code 1 |
| Non-SELECT statement detected | `QueryToCsv: only SELECT statements are allowed.`, exit code 1 |
| `--query` and `--file` both specified | Usage error with a `--help` hint, exit code 2 |
| `-c` name not found | Usage error with a `--help` hint, exit code 2 |
| `-c` omitted with multiple connections | Usage error with a `--help` hint, exit code 2 |
| `-f` file not found | `QueryToCsv: SQL file not found: <path>`, exit code 1 |
| Unknown `-e` encoding | Usage error with a `--help` hint, exit code 2 |
| Unknown CLI option or positional argument | Usage error with a `--help` hint, exit code 2 |
| Query returns 0 rows | CSV written (empty or header-only), exit code 0 |

### Exit Codes

| Code | Meaning |
|------|---------|
| `0` | Success |
| `1` | Runtime error |
| `2` | Usage error |

## License

MIT License. See [LICENSE](LICENSE) for details.

## Building the Installer

Requires [Inno Setup 6](https://jrsoftware.org/isinfo.php).

1. Run `build/Menu.bat`
2. Select **Full Build**

The installer:
- Installs to `%LOCALAPPDATA%\Programs\QueryToCsv` (per-user, no admin required)
- Creates `queries/` and `output/` folders
- Optionally adds the install directory to user `PATH`
- Preserves `appsettings.json` on upgrades (only created on first install)
