# SB12 Compatibility Decision Report

Observed at: 2026-06-15

## Implemented Decision Options

- `FullMigration`
- `ArchiveExport`
- `ReadOnlyLegacyProjectionPlusArchive`
- `DropAfterExplicitApproval`

## Default Decision

For existing legacy history records with no product-owner deletion approval and no mandatory full migration, SB12 selects:

`ReadOnlyLegacyProjectionPlusArchive`

This option blocks runtime actions against legacy runs and requires UI labels that mark legacy runs as read-only.

## Blocking Conditions

The template compatibility report marks manual review as required when:

- A migration plan fails.
- A canonical definition is missing.
- A sidecar is missing source hash metadata, unreadable, or mismatched.
- Branch outcomes are ambiguous.

## Follow-Ups For SB13

- Surface template compatibility status in UI projections.
- Label legacy run projections as read-only.
- Expose manual branch-resolution and sidecar-regeneration status without using old runtime services.

## Evidence

- Focused tests: `test-unit-sb12.txt`
- Sidecar report: `sidecar-drift-report.md`
- Branch report: `branch-migration-diagnostics.md`
- Runtime history report: `runtime-history-inventory.md`
