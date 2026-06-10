# SB003 Semantic Invariants

## Invariant SB003-INV-001
- Invariant ID: `SB003-INV-001`
- Source raw note: "Podivej se realne co udelal v kodu, ne jen jestli to potvrdil v bundle" and "Podivej se jak dopadl realny test."
- Expected behavior: Gate A is closed only when current branch source, prior live/build/unit proof, transient-path guards, and anti-stub scans are represented by durable proof artifacts.
- Disallowed shallow implementation: A populated execution-report row or prose claim that says the baseline passed without source-backed transcripts, critical proof manifest, semantic invariant contract, and a red-team rejection of report-only proof.
- Failing-first test: `bundle://proof/SB003/transcripts/red-team-report-only-proof-rejection.txt` intentionally rejects report-only closure and records `ExitCode: 1`.
- Passing test: `bundle://proof/SB003/transcripts/gate-a-proof-index.txt` verifies required proof artifacts and records `ExitCode: 0`.
- Changed source files: No production source files changed. Bundle and proof file hashes are recorded in `bundle://proof/SB003/manifest.md`.
- Production assertions: `bundle://proof/SB001/transcripts/source-reconciliation.txt` proves the current host remains verification-only and in-memory-audited; `bundle://proof/SB003/transcripts/source-scan-and-anti-stub-audit.txt` proves no execution-capable host or mutation-allowed production drift in the scanned production scope.
- Red-team negative case: `bundle://proof/SB003/transcripts/red-team-report-only-proof-rejection.txt` rejects a fake `Passed` row with no artifacts.
- Downstream dependency check: P02 is allowed only if `bundle://proof/SB003/manifest.md`, `bundle://proof/SB003/semantic-invariants.md`, and all cited transcripts exist.

## Production Behavior Artifact Matrix
| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| `NoNewProductionArtifact` | `bundle://proof/SB001/transcripts/source-reconciliation.txt` shows SB001-SB003 did not add a production producer. | `bundle://proof/SB003/transcripts/gate-a-proof-index.txt` consumes proof artifacts only. | No production lifecycle was introduced by this baseline gate. | `bundle://proof/SB003/transcripts/red-team-report-only-proof-rejection.txt` rejects report-only proof. |
