# Process Module Rewrite Reference Archive v1

Generated UTC: 2026-06-15T17:18:58.0009003Z

Source branch: `codex/process-module-architecture-v3-implementation`

Source commit: `8adffea46d46df61a5f7f952885b47268ec5292e`

CodeAnalytics snapshot: `snap-20260615171018-d225a84b`

Entry count: 1593

## Category Summary

| Category | Files |
| --- | ---: |
| canvas-service | 14 |
| contract-model | 5 |
| core-rule | 16 |
| driver-abstraction | 18 |
| driver-implementation | 62 |
| integration-reference | 276 |
| persistence-model | 13 |
| process-module-source | 49 |
| runtime-dispatch | 240 |
| runtime-model | 50 |
| solution-reference | 1 |
| template-input | 617 |
| template-service | 15 |
| test-data | 68 |
| test-source | 83 |
| ui-surface | 66 |

## Decision Summary

| Decision | Files |
| --- | ---: |
| adapt-concepts | 377 |
| keep-as-reference | 69 |
| migrate-template-input | 617 |
| port-after-redesign | 83 |
| replace-with-new-architecture | 447 |

## Archive Area Summary

| Area | Files |
| --- | ---: |
| CanDoItAll.slnx | 1 |
| integration-snippets | 276 |
| src | 548 |
| Templates | 617 |
| tests | 151 |

## Completeness Notes

- Complete tracked source files were copied for the legacy Process module, Process core/contracts, and Process driver projects.
- Complete tracked files under `Templates/Processes` were copied as migration input.
- Process-named tracked tests and test data were copied.
- Integration touchpoints were copied as separate snippets when they were outside the complete source/template/test archive scope.
- Hashes were computed from archived files and checked against the source files during generation.

## Manifest Fields

Each `manifest.json` entry contains source path, archive path, SHA-256, file size, line count, category, reuse decision, reason, related requirements, and related future tests.
