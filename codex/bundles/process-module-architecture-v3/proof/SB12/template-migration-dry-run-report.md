# SB12 Template Migration Dry Run Report

Observed at: 2026-06-15

## Scope

- Template pack: `Templates/Processes`
- Manifest entries scanned: 24
- `definition.json` files found: 24
- Definition parse errors: 0
- Definitions missing `schemaVersion` or `SchemaVersion`: 24

## Dry-Run Decision

The migration scanner treats `definition.json` as canonical. Missing schema version is mapped to `process-definition/current-module-legacy` and planned through `ProcessTemplateMigrationRegistry`.

The dry run does not mutate files. `ProcessTemplateCompatibilityReport.MigrationDryRun.WouldMutateFiles` is always `false`.

## Compatibility Findings

- All 24 current definitions require legacy schema handling before publication as the target canonical schema.
- Invalid manifest entries now fail predictably with `InvalidDataException`; they are not silently skipped.
- Sequential migration planning is exercised by `ProcessTemplateCompatibilityHistoryTests`.

## Evidence

- Raw pack scan: `template-pack-summary-scan.txt`
- Focused tests: `test-unit-sb12.txt`
- Process regression slice: `test-unit-sb12-process-slice.txt`
- Build: `build-solution-sb12.txt`
