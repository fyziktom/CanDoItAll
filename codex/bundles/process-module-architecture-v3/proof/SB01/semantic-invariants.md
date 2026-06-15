# SB01 Semantic Invariants

## Invariant Summary

| Invariant | Expected behavior | Disallowed shallow implementation | Proof |
| --- | --- | --- | --- |
| SB01-INV-001 | Every legacy Process source root named by SB01 has archived entries. | Copy only `src/CanDoItAll.Modules.Processes` and ignore core/contracts/drivers. | `bundle://proof/SB01/transcripts/hash-verification.txt` |
| SB01-INV-002 | `Templates/Processes` is preserved as migration input before any active removal. | Treat templates as disposable generated output. | `repo://codex/bundles/process-module-rewrite-reference-v1/inventories/template-pack-inventory.md` |
| SB01-INV-003 | Process-related tests and test data are inventoried for later porting or retirement. | Remove or quarantine tests without an evidence baseline. | `repo://codex/bundles/process-module-rewrite-reference-v1/inventories/test-inventory.md` |
| SB01-INV-004 | Integration references outside the complete Process source roots are inventoried. | Search only tracked Process projects and miss Web, Workbench, SchedulerPlanner, tooling, or ignored evidence-source references. | `bundle://proof/SB01/transcripts/search-coverage.txt` |
| SB01-INV-005 | Archived files have reproducible hashes and line counts. | Copy files without source-to-archive hash verification. | `repo://codex/bundles/process-module-rewrite-reference-v1/manifest.json` |
| SB01-INV-006 | SB01 does not alter active product behavior. | Mix archive work with product edits or removal. | `bundle://proof/SB01/transcripts/active-product-diff.txt` |

## Raw Input Closure

| Raw input | Closure result | Proof |
| --- | --- | --- |
| REQ-048: copy old Process implementation into reference material before deletion. | Solved for SB01 | `repo://codex/bundles/process-module-rewrite-reference-v1/manifest.json` |
| REQ-049: remove old Process projects/tests only after archive proof. | Solved for SB01 prerequisite | `bundle://proof/SB01/transcripts/hash-verification.txt` and `bundle://proof/SB01/transcripts/active-product-diff.txt` |
| Phase 0 split: archive-only work belongs in SB01; active removal belongs in SB02. | Solved for SB01 | `bundle://proof/SB01/manifest.md` |

## Negative And Positive Proof

- Negative case: `bundle://proof/SB01/transcripts/negative-tracked-only-archive-gap.txt` shows a tracked-files-only archive misses integration/source evidence discovered by `rg`.
- Positive case: `bundle://proof/SB01/transcripts/search-coverage.txt` shows 0 missing manifest entries for source roots, templates, process-named tests, and integration search matches.
- Hash fidelity: `bundle://proof/SB01/transcripts/hash-verification.txt` shows 0 missing files and 0 hash mismatches across 1593 entries.

## Production Behavior Artifact Matrix

SB01 creates no production runtime artifact. The reference archive is a build-independent evidence artifact consumed by later bundle gates.

| Artifact | Producer | Consumer | Lifecycle | Negative-test citation |
| --- | --- | --- | --- | --- |
| `repo://codex/bundles/process-module-rewrite-reference-v1/manifest.json` | `bundle://proof/SB01/scripts/create-reference-archive.ps1` | SB02 removal gate; SB12 template/history compatibility; SB28 final closure | Generated before active deletion, then treated as immutable reference evidence | `bundle://proof/SB01/transcripts/negative-tracked-only-archive-gap.txt` |

## SB02 Handoff

SB02 may start only after confirming:

- `repo://codex/bundles/process-module-rewrite-reference-v1/manifest.json` exists.
- `bundle://proof/SB01/transcripts/hash-verification.txt` still reports 0 missing files and 0 hash mismatches.
- `bundle://proof/SB01/transcripts/search-coverage.txt` still reports 0 missing source, template, test, and integration matches.
- Active product diff remains empty for SB01 scope.
