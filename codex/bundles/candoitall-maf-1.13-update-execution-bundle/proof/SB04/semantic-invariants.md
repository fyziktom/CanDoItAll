# SB04 Semantic Invariants

## INV-SB04-NO-ARCHITECTURE-DRIFT

Raw note owned: use the prepared architecture guardrails and keep this MAF update conservative.

Expected behavior: package compatibility changes stay inside existing MAF adapter seams and do not introduce process-domain runtime tools, process API expansion, central package management, new project-reference direction, or new runtime partial files.

Disallowed shallow implementation: accepting a compile fix that moves process orchestration into MAF, adds direct `processes_*` tools, creates new API routes, or expands large runtime classes through new partial files.

Semantic positive proof: `bundle://proof/SB04/transcripts/source-scans.md` and `bundle://proof/SB04/transcripts/partial-class-policy.md` show the forbidden source and partial-class gates pass.

Adversarial negative proof: `bundle://proof/SB04/transcripts/diff-stat.md` and `bundle://proof/SB04/transcripts/dependency-and-partial-policy.md` show the diff is constrained to package references, one existing MAF call site, and focused tests; no `ProjectReference` changes exist.

CodeAnalytics proof: `bundle://proof/SB04/transcripts/codeanalytics-summary.md` records scoped snapshot `snap-20260708010020-ca7eff1f`, no blocking errors, and no dependency cycles in the scoped dependency query.

Anti-stub audit: `bundle://proof/SB04/transcripts/anti-stub.md` found no placeholder or silent-fallback patterns in the architecture-reviewed source changes.

Downstream dependency check: `SB05` may start because the architecture checkpoint passed.

## Production Behavior Artifact Matrix

No new production signal, state record, external route, process tool provider, workflow surface, or project boundary was introduced in `SB04`.
