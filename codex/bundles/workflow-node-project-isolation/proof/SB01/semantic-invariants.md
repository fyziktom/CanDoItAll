# SB01 Semantic Invariants

## Status

- Subbundle: `SB01 - Workflow Boundary Inventory And Project Graph`
- Status: `Completed`
- Manifest: `bundle://proof/SB01/manifest.md`

## Invariants

| Invariant | Raw note | Expected behavior | Disallowed shallow implementation | Adversarial negative proof | Semantic positive proof | Changed files / hashes | Production assertions | Downstream check |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| SB01-INV-01 | "all parts must be identified before implementation" | Live workflow, executor, template, UI, Workbench, scheduler, plugin, and feature-module executor surfaces are mapped to owners or explicit downstream proof. | Treating workflow inventory as complete when it only lists built-in MAF executors and plugin executors. | `bundle://proof/SB01/transcripts/semantic-surface-check.txt` simulates the default/plugin-only shallow inventory and records the missing Cognitive Memory, Workbench agent-tool, and Scheduler surfaces. | `bundle://proof/SB01/transcripts/source-assertions.txt` and `bundle://proof/SB01/transcripts/semantic-surface-check.txt` prove repaired rows exist. | `bundle://proof/SB01/changed-file-hashes.txt` | Existing source references are asserted; no production code changed. | SB02 can rely on repaired inventory; SB06/SB09/SB10/SB12/SB13 own added surfaces. |
| SB01-INV-02 | "executors must have own" | Executor source inventory includes default, plugin, runtime package, and feature-module executor sources with stable ids and validation owners. | Moving only MAF default executors while leaving module-provided executors attached to old Core/MAF executor-contract ownership. | `bundle://proof/SB01/transcripts/semantic-surface-check.txt` identifies `cognitive-memory.recall`, `cognitive-memory.probe`, and `cognitive-memory.learning-proposal` as required stable ids. | `bundle://proof/SB01/transcripts/source-assertions.txt` proves Cognitive Memory rows in source/executor inventories and SB06/SB09 contracts. | `bundle://proof/SB01/changed-file-hashes.txt` | Production ids remain in `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryAdvancedContracts.cs`. | SB06/SB09 must add descriptor parity, registration, dependency, cancellation, and diagnostic tests. |
| SB01-INV-03 | Base-up project extraction with no MAF fallback owner | Project graph assigns module-provided executors, Workbench agent workflow tools, and Scheduler workflow input consumers to downstream phases without weakening dependency direction. | Using MAF, UI-owned template DTOs, or Core executor contracts as ambiguous fallback owners. | `bundle://proof/SB01/transcripts/semantic-surface-check.txt` checks dependency-direction text and downstream subbundle contracts. | `bundle://proof/SB01/transcripts/source-assertions.txt` proves `architecture/02-project-map-and-adoption-boundary.md` and SB06/SB09/SB10/SB12 mappings exist. | `bundle://proof/SB01/changed-file-hashes.txt` | No production dependency graph changed in SB01; target dependency rules are explicit. | SB10/SB12/SB13 must prove no UI-owned template DTO or MAF internal fallback remains. |
| SB01-INV-04 | XLSX mapping required for all parts to rework/test | Workbook renders after repair, lists the added surfaces, and has no formula-error matches. | Updating markdown only while leaving the XLSX mapping stale. | `bundle://proof/SB01/transcripts/semantic-surface-check.txt` requires workbook sheet/list and formula scan proof. | `bundle://proof/SB01/transcripts/workbook-render.txt` and `bundle://proof/SB01/workbook-previews/*.png` prove all sheets rendered after update. | `bundle://proof/SB01/changed-file-hashes.txt` | No production state changed; workbook is bundle proof. | SB02 starts from workbook-backed inventory instead of markdown-only claims. |

## Production Behavior Artifact Matrix

N/A. These invariants do not introduce a production signal, state, record, or event. They repair the bundle contract and proof ownership before implementation subbundles change production behavior.

## Raw Note Closure

| Raw note | SB01 result | Proof |
| --- | --- | --- |
| Identify all parts before implementation | Solved for SB01 entry inventory, with downstream owners for newly found surfaces | `bundle://proof/SB01/transcripts/inventory-search.txt`, `bundle://proof/SB01/transcripts/semantic-surface-check.txt`, `bundle://proof/SB01/workbook-previews/source-map.png` |
| Build from base up and avoid ambiguous MAF ownership | Solved for project graph and downstream subbundle ownership; production dependency moves remain later phases | `bundle://proof/SB01/transcripts/source-assertions.txt`, `repo://codex/bundles/workflow-node-project-isolation/architecture/02-project-map-and-adoption-boundary.md` |
| Use XLSX mapping for rework/test surfaces | Solved for SB01 repaired surfaces | `bundle://proof/SB01/transcripts/workbook-render.txt`, `bundle://proof/SB01/workbook-previews/validation-matrix.png` |

## Completed Validator Semantic Contract Addendum

- Invariant ID: SB01-final-closure
- Source raw note: R01-R18 workflow-node project isolation closure evidence for SB01.
- Expected behavior: The SB01 scope remains closed by its recorded proof artifacts and downstream SB14 final regression.
- Disallowed shallow implementation: Do not replace the recorded source/test proof with summary-only closure or silent fallback behavior.
- Failing-first test: N/A - process/no production behavior metadata addendum; adversarial negative proof remains in the SB01 transcript set where applicable.
- Passing test: See bundle://proof/SB01/transcripts/ for the SB01 passing command transcript set and SB14 final regression transcripts.
- Changed source files: See bundle://proof/SB01/manifest.md and bundle://proof/SB14/changed-file-hashes.txt for the final closure hash set.
- Production assertions: Production behavior is asserted by the SB01 proof chain and SB14 final unit/component/integration/browser regression.
- Red-team negative case: SB14 no-fallback, no-generic, anti-stub, and responsibility audits guard the final state.
- Downstream dependency check: SB14 final closure revalidated downstream workflow, executor, plugin, template, MAF adapter, API, UI, Workbench, and process integration paths.
