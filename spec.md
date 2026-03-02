# SQLクエリCSV出力ツール 仕様書

## 1. 概要

SQL Serverに接続し、指定フォルダに保存された `.sql` ファイルを選択・実行して、結果をCSVファイルとして出力するCLIツール。

---

## 2. 動作環境

| 項目 | 内容 |
|------|------|
| 言語 | C# |
| フレームワーク | .NET 10.0 |
| 対応DB | Microsoft SQL Server |
| 実行形式 | コンソールアプリケーション（CLI） |
| アーキテクチャ | x64 |
| 配布形式 | self-contained 単一ファイル（.NETランタイムのインストール不要） |
| バージョン | 1.0.0（セマンティックバージョニングに従う） |

---

## 3. ディレクトリ構成

### ソース構成（リポジトリ）

```
/                             # リポジトリルート
  ├── QueryToCsv/             # プロジェクトフォルダ
  │   ├── QueryToCsv.csproj
  │   ├── Program.cs
  │   ├── appsettings.sample.json
  │   └── ...
  ├── Build/                  # ビルド・インストーラースクリプト
  │   ├── Build.ps1
  │   ├── Menu.bat
  │   └── Setup.iss
  └── spec.md
```

### 実行時構成（インストール後）

```
{インストール先}/
  ├── QueryToCsv.exe            # 実行ファイル
  ├── appsettings.json          # 設定ファイル
  ├── queries/                  # SQLファイル格納フォルダ（設定で変更可）
  │   ├── sales_report.sql
  │   └── user_list.sql
  └── output/                   # CSV出力先フォルダ（設定で変更可）
```

---

## 4. 設定ファイル仕様

### ファイル名
`appsettings.json`

### 内容

```json
{
  "ConnectionString": "Server=localhost;Database=MyDB;User Id=sa;Password=yourpassword;TrustServerCertificate=True;",
  "QueryFolder": "./queries",
  "OutputFolder": "./output",
  "QueryTimeout": 30,
  "SqlFileEncoding": "UTF-8",
  "CsvSettings": {
    "Delimiter": ",",
    "NullValue": "",
    "NewLine": "CRLF",
    "DateFormat": null
  }
}
```

### 各項目

| キー | 型 | 必須 | デフォルト | 説明 |
|------|----|------|-----------|------|
| ConnectionString | string | ✅ | - | SQL Serverへの接続文字列（SQL Server認証・Windows認証の両方に対応） |
| QueryFolder | string | ✅ | - | .sqlファイルを格納するフォルダパス |
| OutputFolder | string | ✅ | - | CSV出力先フォルダパス |
| QueryTimeout | int | - | `30` | クエリ実行タイムアウト（秒）。1以上の整数 |
| SqlFileEncoding | string | - | `"UTF-8"` | .sqlファイルの読み込みエンコード。.NETの `Encoding.GetEncoding()` が受け付ける名前を指定可能 |
| CsvSettings.Delimiter | string | - | `","` | CSV区切り文字。1文字のみ。タブは `"\t"` で指定（JSONパース後の1文字で検証） |
| CsvSettings.NullValue | string | - | `""`（空文字） | NULL値の出力文字列 |
| CsvSettings.NewLine | string | - | `"CRLF"` | 改行コード。有効値: `"CRLF"`, `"LF"` |
| CsvSettings.DateFormat | string | - | `null`（`InvariantCulture`による既定変換） | 日付型の出力フォーマット（例: `"yyyy-MM-dd HH:mm:ss"`） |

### パス解決ルール

`QueryFolder`・`OutputFolder` に相対パスを指定した場合、**実行ファイル（exe）の配置ディレクトリ**を基準に解決する。

---

## 5. 機能仕様

### 5.1 SQLファイル一覧表示

- 起動時に `QueryFolder` に存在する `.sql` ファイルを一覧表示する
- ファイルはファイル名の昇順で表示する
- ファイルが存在しない場合はエラーメッセージを表示して終了する

**表示例：**
```
=== Select a query ===
1. sales_report.sql
2. user_list.sql

Enter number:
```

### 5.2 クエリ選択

- ユーザーが番号を入力してクエリを選択する
- 無効な入力（数字以外、範囲外の番号、空Enter）の場合は再入力を促す

### 5.3 ヘッダー出力設定

- クエリ選択後、ヘッダー行の出力有無をユーザーが選択する

**表示例：**
```
Include header row? (y/n):
```

- `y`/`n` 以外の入力は再入力を促す

### 5.4 エンコード選択

- CSV出力時の文字エンコードをユーザーが選択する

**表示例：**
```
=== Select encoding ===
1. UTF-8
2. UTF-8 with BOM
3. UTF-16 LE
4. Shift-JIS

Enter number:
```

| 番号 | エンコード | 備考 |
|------|-----------|------|
| 1 | UTF-8 | BOMなし。世界標準 |
| 2 | UTF-8 with BOM | Excelで開く場合に推奨 |
| 3 | UTF-16 LE | Windows系ツール連携 |
| 4 | Shift-JIS | 日本語レガシーシステム向け |

### 5.5 クエリ実行

- 選択された `.sql` ファイルの内容を `SqlFileEncoding` で指定されたエンコードで読み込む
- `SqlDataReader` を使用してストリーミング処理する（全件をメモリに読み込まない）
- SQL Serverで実行する
- `.sql` ファイルに複数のステートメントが含まれる場合、**最初の結果セットのみ**を使用する
- `QueryTimeout` で指定された秒数を `CommandTimeout` に設定する
- 実行中はプログレスメッセージを表示する
- 接続エラー・SQLエラー・タイムアウト発生時はエラーメッセージを表示して終了する

> **注意:** 本ツールはSQLファイルの内容をそのまま実行する。UPDATE/DELETE等の更新系クエリの制限は行わない。利用者の責任で適切なクエリを配置すること。

### 5.6 CSV出力

- 実行結果を `OutputFolder` にCSVファイルとして出力する
- ファイル名は `{SQLファイル名（拡張子なし）}_{タイムスタンプ}.csv` とする
- クエリ結果が0件の場合、ヘッダー有りならヘッダーのみのCSVを出力し、ヘッダー無しなら空ファイルを出力する

**ファイル名例：**
```
sales_report_20260302_153045.csv
```

**タイムスタンプフォーマット：** `yyyyMMdd_HHmmss`

**ファイル名の衝突：** 同名ファイルが既に存在する場合、末尾に連番を付与する（例: `sales_report_20260302_153045_2.csv`）

### 5.7 CSV書式

CSV出力は **RFC 4180** に準拠する。CsvHelperライブラリを使用して準拠を保証する。

| 項目 | 仕様 |
|------|------|
| 区切り文字 | `CsvSettings.Delimiter` の値（デフォルト: カンマ） |
| クォート | フィールド内に区切り文字・改行・ダブルクォートが含まれる場合、ダブルクォートで囲む |
| クォートのエスケープ | ダブルクォートを `""` に置換する |
| NULL値 | `CsvSettings.NullValue` の値（デフォルト: 空文字） |
| 日付フォーマット | `CsvSettings.DateFormat` の値（デフォルト: `null` = `InvariantCulture`による既定変換） |
| 数値フォーマット | `InvariantCulture` で変換（小数点は `.`、桁区切りなし） |
| 改行コード | `CsvSettings.NewLine` の値（デフォルト: CRLF） |

---

## 6. エラーハンドリング

| エラー種別 | 対応 |
|-----------|------|
| appsettings.json が存在しない | エラーメッセージを表示して終了 |
| appsettings.json のJSON構文エラー | `Error: Failed to load appsettings.json.` と表示して終了 |
| QueryFolder が存在しない | エラーメッセージを表示して終了 |
| .sqlファイルが0件 | `No query files found.` と表示して終了 |
| SQL Server接続失敗 | `Error: <詳細>` を表示して終了 |
| SQLクエリ実行エラー | `Error: <詳細>` を表示して終了 |
| OutputFolder が存在しない | 自動作成して処理を継続 |
| クエリタイムアウト | `Error: Query timed out.` と表示して終了 |
| クエリ結果が0件 | CSV出力後 `Rows: 0` と表示して正常終了 |
| appsettings.json の設定値が不正 | `Error: <項目名と理由>` を表示して終了（例: QueryTimeoutが0以下、NewLineが無効値） |

---

## 7. 実行フロー

```
Start
  │
  ├─ Load appsettings.json
  │
  ├─ List .sql files in QueryFolder
  │
  ├─ Select query (enter number)
  │
  ├─ Include header row? (y/n)
  │
  ├─ Select encoding (enter number)
  │
  ├─ Connect to SQL Server & execute query
  │
  ├─ Write CSV output
  │     └─ {filename}_{yyyyMMdd_HHmmss}.csv
  │
  └─ Display result & exit
```

---

## 8. 使用ライブラリ

| ライブラリ | 用途 |
|-----------|------|
| `Microsoft.Data.SqlClient` | SQL Server接続・クエリ実行 |
| `Microsoft.Extensions.Configuration` | appsettings.json読み込み |
| `Microsoft.Extensions.Configuration.Json` | JSON設定ファイルサポート |
| `System.Text.Encoding.CodePages` | Shift-JIS等の追加エンコードサポート |
| `CsvHelper` | CSV出力（RFC 4180準拠） |

---

## 9. 実行例（画面遷移）

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

Done: C:\Program Files\QueryToCsv\output\sales_report_20260302_153045.csv
Rows: 1,234
```

---

## 10. パブリックリポジトリ運用上の注意

本ツールはGitHubのpublicリポジトリで公開することを前提とする。
接続文字列などの機密情報をリポジトリに含めないよう、以下の対策を取ること。

### 対策方針

| 対策 | 内容 |
|------|------|
| `appsettings.json` を `.gitignore` に追加 | 接続文字列をリポジトリにコミットしない |
| `appsettings.sample.json` を提供 | 値をダミーにしたサンプルをリポジトリに含める |

### .gitignore に追加する項目

```
appsettings.json
output/
```

### appsettings.sample.json（リポジトリに含めるファイル）

Windows認証の場合は `Integrated Security=True` を使用し、`User Id`/`Password` を省略する。

```json
{
  "ConnectionString": "Server=YOUR_SERVER;Database=YOUR_DB;User Id=YOUR_USER;Password=YOUR_PASSWORD;TrustServerCertificate=True;",
  "QueryFolder": "./queries",
  "OutputFolder": "./output",
  "QueryTimeout": 30,
  "SqlFileEncoding": "UTF-8",
  "CsvSettings": {
    "Delimiter": ",",
    "NullValue": "",
    "NewLine": "CRLF",
    "DateFormat": null
  }
}
```

### README への記載事項

- `appsettings.sample.json` をコピーして `appsettings.json` を作成し、接続情報を設定する手順を明記する

---

## 11. 終了コード

| コード | 意味 |
|--------|------|
| `0` | 正常終了 |
| `1` | 異常終了（設定エラー、接続エラー、SQLエラー等） |

---

## 12. ビルドとインストーラー

### ビルド

`Build/Menu.bat` を実行し、メニューからビルド・インストーラー作成を選択する。

| 操作 | 内容 |
|------|------|
| Build | `dotnet publish` で self-contained 単一ファイルを生成 |
| Create Installer | Inno Setup 6 でインストーラーを生成 |
| Build + Create Installer | 上記を一括実行 |

### インストーラー

| 項目 | 内容 |
|------|------|
| ツール | Inno Setup 6 |
| インストール先 | `{Program Files}\QueryToCsv` |
| 管理者権限 | 必要 |
| PATH登録 | インストール時にオプションとして選択可能 |
| appsettings.json | 初回インストール時のみ作成（更新時はユーザー設定を保持） |
| queries / output フォルダ | インストール時に作成 |

---

## 13. 今後の拡張案（スコープ外）


- 複数クエリの一括実行
- パラメータ付きクエリのサポート
- スケジュール実行（タスクスケジューラ連携）
- 接続先プロファイルの複数管理
- ログファイル出力
