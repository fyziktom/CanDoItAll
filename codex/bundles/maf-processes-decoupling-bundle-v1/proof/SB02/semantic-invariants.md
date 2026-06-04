# SB02 Semantic Invariants

## Invariant `SB02-INV-001`

- Invariant ID: SB02-INV-001

- Source raw note: MAF and Processes must be decoupled without simplifying or omitting behavior, in smaller safe steps.
- Expected behavior: A provider-neutral runtime tool contract exists outside MAF and outside product modules, and both MAF and Processes can reference it without moving process tool behavior yet.
- Disallowed shallow implementation: Put the abstraction in MAF, put it in Processes, or let the new abstraction reference `CanDoItAll.Modules.*`; that would compile but preserve the wrong dependency direction.
- Failing-first test and transcript: The architecture test would fail if Tooling gained a `CanDoItAll.Modules.*` project reference or Tooling source referenced process-specific services; see `bundle://proof/SB02/transcripts/architecture-tests.txt`.
- Passing test and transcript: `bundle://proof/SB02/transcripts/architecture-tests.txt` passes 3 tests proving no Tooling module reference, no Tooling process namespace leakage, and MAF/Processes Tooling references.
- Changed source files and hashes: `bundle://proof/SB02/source-assertions/changed-file-hashes.txt`.
- Production assertions: `bundle://proof/SB02/source-assertions/tooling-project-reference-audit.txt` and `bundle://proof/SB02/source-assertions/tooling-source-forbidden-namespace-audit.txt`.
- Red-team negative case: A fake seam inside MAF or Processes would not satisfy the Tooling project and reference assertions, and a Tooling project reference to `CanDoItAll.Modules.Processes` would be caught.
- Downstream dependency check: SB03 can start because MAF can now reference the provider-neutral contract while the old process builder remains for compatibility.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative test |
| --- | --- | --- | --- | --- |
| N/A | SB02 introduces contracts only, not a production signal, state, record, or event. | N/A | N/A | N/A |
