# SB02: 02-maf16-official-version-and-api-inventory

## Goal

Resolve exact MAF 1.6 package versions and API changes from official sources.

## Required work

- Use NuGet/package search and official docs/release notes to identify versions for `Microsoft.Agents.AI`, `OpenAI`, `Workflows`, `A2A`, `Mem0`, Hosting packages if needed.
- Prefer stable `1.6.2` where available; if A2A remains preview/renamed, document and choose the compatible package deliberately.
- Produce a package matrix: current version, target version, stable/preview status, transitive dependency risks, code areas affected.
- Inventory public MAF APIs used by CanDoItAll and map likely breaking changes.

## Required proof

- Failing-first or adversarial proof.
- Passing proof on production code path.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Notes on MAF 1.6 impact if this subbundle touches agent runtime.
- Notes on process core genericity if this subbundle touches Processes.

## Closure criteria

Do not close this subbundle until proof files under `proof/SB02` are updated and the next subbundle can safely depend on it.
