---
status: active
created: 2026-07-27
---

# QueryToCsv Rules

Application-specific rules for QueryToCsv. Write only deltas and overrides against the
shared rules (`standard.md`, `dotnet.md` in this directory) — never copy shared rule
text here. On conflict, this file wins.

## Repository Language

- This is a public repository. English is the language it publishes in: console output,
  every other user-facing string, and all documentation
- `AGENTS.md`, this file, and all other agent instruction files in this repository are
  written in **English only**
- Keep rules concise and declarative. Do NOT include concrete code examples unless
  absolutely necessary — reference the relevant source file/method instead

## Required Input

Overrides Configuration Values in `standard.md`, which has every configurable value fall
back to a built-in default.

- **`Connections` is required input, not a defaulted value.** No built-in value can name
  the server an operator means to query, so a configuration with no entry — or with an
  entry whose `Name` or `ConnectionString` is blank — ends the run with exit code 1.
  Every other setting follows the shared rule and falls back to its default
- **An absent setting is not a rejected value.** The shared rule's report names what was
  rejected, so only a value that is present and unusable is reported; a key the operator
  left out takes its default silently

## Application Rules

- **Only SELECT statements may reach the server.** `QueryExecutor.IsSelectOnly` strips
  comments and string literals, then rejects the statement if any data-modifying or
  out-of-scope keyword remains. A change that widens what the tool may execute is a
  specification change first (`docs/specs/QueryToCsv.md`), never an inline relaxation of
  the keyword list

## Release Constraints

- Every `<Version>` change in `Directory.Build.props` must be published as an annotated
  tag `v{version}` on `main` and a matching GitHub Release; the procedure is
  `docs/guides/release.md`
- Every release tag must have a corresponding GitHub Release
- Release assets must be produced with `build/Build.ps1` and `build/Installer.ps1`, and
  every GitHub Release must include both `build/QueryToCsv/QueryToCsv.exe` and
  `build/Installer/QueryToCsv-Setup-{version}.exe`
- Only the current version stays published: once a new release is verified, every
  previous version's tag and GitHub Release must be deleted as pairs, locally and on
  `origin`, so that no tag outlives its release
