# SB003 Semantic Invariants

## Status
Completed.

## Invariant SB003_INV_001
- Invariant ID: `SB003_INV_001`
- Source raw note: "Podívej se reálně co udělal v kódu, ne jen jestli to potvrdil v bundle" and "Podívej se jak dopadl reálný test..."
- Expected behavior: Gate A must resume from current source and test evidence, preserve the prior bundle's incomplete SB013-SB048 state, and prevent downstream phases from relying on report-only proof.
- Disallowed shallow implementation: Updating execution-report rows or bundle status without reading current process code, tests, prior report pending rows, and no-transient path scans.
- Failing-first test: Historical failing-first inventory at `repo://codex/bundles/process-runtime-live-e2e-openai-hardening-v1/proof/SB002/transcripts/transient-path-classification-scan.txt` shows the earlier concrete bundle-path coupling that SB003 must not reintroduce.
- Passing test: Focused Gate A unit tests passed with 89 tests in `bundle://proof/SB003/transcripts/gate-a-focused-unit-tests.txt`.
- Changed source files: No production source changed in SB003. Current source hashes are captured in `bundle://proof/SB003/manifest.md`.
- Production assertions: `bundle://proof/SB003/transcripts/gate-a-source-assertions.txt` cites current typed process-service runtime surfaces and the prior report's pending SB013-SB048 rows.
- Red-team negative case: `bundle://proof/SB003/red-team/report-only-proof-rejection.txt` rejects status-only/report-only closure.
- Downstream dependency check: P02 may start only because SB001-SB003 now prove current baseline, clean bundle path scan, and prepared validator pass.

## Shallow-Pass Trap
A fake Gate A closure could declare "previous bundle completed" or copy old status text. SB003 rejects that by requiring current source scans, focused tests, prior report pending-row evidence, and the red-team report-only rejection artifact.

## Semantic Positive Proof
- `bundle://proof/SB003/transcripts/gate-a-focused-unit-tests.txt`
- `bundle://proof/SB003/transcripts/gate-a-source-assertions.txt`
- `bundle://proof/SB003/transcripts/prepared-validator-after-sb003.txt`

## Adversarial Negative Proof
- `bundle://proof/SB003/red-team/report-only-proof-rejection.txt`
- `repo://codex/bundles/process-runtime-live-e2e-openai-hardening-v1/proof/SB002/transcripts/transient-path-classification-scan.txt`

## Anti-Stub Audit
- `bundle://proof/SB003/transcripts/anti-stub-and-runtime-host-drift-scan.txt`
- Matches are documentation and negative test assertions, not an execution-capable process-driver runtime host, process-driver registry, selector, or production `NotImplemented` path.

## Production Behavior Artifact Matrix
| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Source-backed resume baseline | `bundle://proof/SB001/source-inventory.md` and `bundle://proof/SB003/transcripts/gate-a-source-assertions.txt` | P02/P03 runtime lifecycle and dispatch subbundles | Blocks downstream runtime work until current source, tests, and prior pending report state are reconciled | `bundle://proof/SB003/red-team/report-only-proof-rejection.txt` |
| No transient bundle-path guard | `repo://tests/CanDoItAll.Tests.Unit/ProcessDriverFakeProofResistanceTests.cs` and `bundle://proof/SB003/transcripts/no-transient-bundle-path-scan.txt` | Architecture/fake-proof unit tests and final validators | Keeps long-lived `src` and `tests` independent of concrete bundle folders | `repo://codex/bundles/process-runtime-live-e2e-openai-hardening-v1/proof/SB002/transcripts/transient-path-classification-scan.txt` |
