# 00 Snapshot Integrity and Secret Stop-the-Line

## Goal

Fix the committed secret and make future false completion reports impossible.

## Tasks

1. Remove provider key values from `src/CanDoItAll.Web/appsettings.json` and any other tracked config/payload files.
2. Replace with empty value, placeholder without secret pattern, or documented environment variable lookup.
3. Add a tracked-file secret scanner test and/or script.
4. Add a snapshot integrity test that verifies every claimed file/test in `01-execution-report.md` exists.
5. Add documentation for external rotation/revocation.
6. Ensure secret scans redact matches and never print raw values.

## Acceptance criteria

- `git grep -nE 'sk-(proj-)?[A-Za-z0-9_-]{20,}'` returns no tracked source/config/doc/test matches.
- `SecretScanningTests` exists and fails on representative OpenAI/Azure key patterns.
- `01-execution-report.md` exists and is validated by a test or script.
- Report includes a rotation/revocation note.
