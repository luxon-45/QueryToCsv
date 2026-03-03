# QueryToCsv Project Rules

## Language

- This is a public repository. Use English as the primary language for all user-facing strings, comments, documentation, and commit messages.

## Versioning

### When to Bump

- New feature → minor (e.g., 1.0.0 → 1.1.0)
- Bug fix → patch (e.g., 1.1.0 → 1.1.1)
- Breaking change → major (e.g., 1.1.0 → 2.0.0)
- Docs-only changes or refactoring with no behavior change → do not bump

### Where to Update

Both files must be updated together:

1. `QueryToCsv/QueryToCsv.csproj` — `<Version>` element
2. `Build/Setup.iss` — `#define MyAppVersion`

## Release

When creating a GitHub release, build and attach both assets:

1. Build the executable: `powershell -ExecutionPolicy Bypass -File Build/Build.ps1`
2. Build the installer: `"C:/Program Files (x86)/Inno Setup 6/ISCC.exe" Build/Setup.iss`
3. Create the release with both files:

```
gh release create v{version} \
  "Build/Installer/QueryToCsv-Setup-{version}.exe" \
  "Build/QueryToCsv/QueryToCsv.exe" \
  --title "v{version}" --notes "..."
```
