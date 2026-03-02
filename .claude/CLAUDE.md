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
