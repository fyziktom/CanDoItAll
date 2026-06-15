# SB12 Branch Migration Diagnostics

Observed at: 2026-06-15

## Counts

- Definition files scanned: 24
- Steps with a `BranchOutcomes` property: 149
- Non-empty branch outcome sections: 21
- Branch outcomes found: 45
- Branch outcomes without typed route target: 45

## Decision

SB12 does not infer routes from free text such as display titles or labels. Every branch outcome without a typed route target receives a `ProcessBranchMigrationDiagnosticKind.AmbiguousRouteTarget` diagnostic and must be resolved manually.

## Evidence

- Raw pack scan: `template-pack-summary-scan.txt`
- Free-text shortcut scan: `branch-routing-shortcut-scan.txt`
- Focused tests: `test-unit-sb12.txt`
