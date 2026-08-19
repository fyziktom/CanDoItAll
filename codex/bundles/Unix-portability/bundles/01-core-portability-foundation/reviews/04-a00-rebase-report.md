# A00 rebase report

- Program/bundle: `Unix-portability / 01-core-portability-foundation`
- Prepared commit: `62ea8ee0cc42c1c06da934d126a5c18f8237a89f`
- Execution commit: `a2856070e7303de077088fc7f2f7e96a5bcf0e70`
- Branch: `unix-adoption`
- Merge base: `62ea8ee0cc42c1c06da934d126a5c18f8237a89f`
- Ahead/behind: `3/0` relative to the prepared commit
- Initial dirty state: clean
- Submodules: none declared
- SDK/OS: .NET SDK `10.0.302`; Windows `10.0.26200`; `win-x64`

## Changed source references

| Old path | New path/disposition | Owner | Requirements/subbundles affected |
|---|---|---|---|
| Prepared source manifest anchor | Preserved as `prepared_source_anchor`; current `source_anchor` records execution HEAD | A00 | PREP-001 |
| SEC-006/SEC-007/SEC-008 evidence | Paths unchanged; upgraded from Search-confirmed to Inspected | A00/A04 | PREP-002; SEC-006..SEC-008 |

All 31 prepared source-reference paths exist at execution HEAD. The three commits after the prepared anchor change only `codex/bundles/Unix-portability`; there is no intervening product-source delta.

## New/removed findings

The generated scan found 25,479 lexical occurrences. The reviewed artifact classifies every occurrence and leaves zero unowned items. Of 8,217 critical/high occurrences, executable/configuration surfaces are routed to a concrete implementation requirement; tests/fixtures and reference-only files are explicitly separated. No prepared finding was removed.

## Persistence/migration impact

Fourteen durable record families are now mapped in `inventories/persistence-migration-inventory.csv`, including control-plane JSON, storage catalog and locators, Data Protection keys, secret vault payloads, Workbench metadata, capability configuration, process templates, and Manager ownership artifacts.

## Architecture ownership impact

The current graph has 103 projects, 608 project-reference edges, and zero project-level cycles. The pure logical-path value is approved for SharedKernel because both Infrastructure and MAF Core already depend inward on it. Physical filesystem policy remains Infrastructure-owned. Runtime execution, Workbench, Manager, Plugins/FileTools, and Processes boundaries remain separated.

## Invalidated evidence

- Preparation did not run a local build/test and cannot serve as execution proof.
- The disabled historical CI workflow cannot establish Unix support.
- Search-only evidence for SEC-006..SEC-008 is replaced by direct inspection.

## Split/correction/recovery decision

No source split is required for A00-A07. External Components/FileTools changes, if required, will be recorded as child bundles in B00 after exact C4 re-anchoring. The user-authorized temporary project-reference topology is likewise deferred to B00 so core validation remains attributable.

## First eligible subbundle

`A01`, only after Gate C0 is GO.

## Validation

- Portable validator: passed before execution (258 files, no warnings/errors).
- Materialized prepared validator: passed with only the permitted different-commit warning.
- Baseline: Windows build passed; stable test execution exposed a pre-existing Integration test-host stall. Linux Docker baseline is recorded separately. macOS is unavailable locally and cannot be simulated by Docker.
- Reviewer: primary execution agent; architecture gate remains pending until baseline and inventory validation are complete.
