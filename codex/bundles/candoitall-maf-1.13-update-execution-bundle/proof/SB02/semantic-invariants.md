# SB02 Semantic Invariants

## INV-SB02-CONSERVATIVE-PACKAGE-GRAPH

Raw note owned: use the prepared Microsoft Agent Framework update information and keep the update conservative.

Expected behavior: package references move to the MAF 1.13 line without unrelated package-family upgrades, new central package management, or guessed preview versions.

Disallowed shallow implementation: updating every package to latest, guessing a Mem0 replacement, or suppressing restore warnings instead of addressing proven floors.

Semantic positive proof: `bundle://proof/SB02/transcripts/restore-after-floor.md` shows restore passes after only MAF/A2A target versions and NU1605-proven dependency floors.

Adversarial negative proof: `bundle://proof/SB02/transcripts/restore.md` shows restore failed before the dependency-floor correction; this proves the floor changes were required and not version chasing.

Production assertions: package-only diff is captured in `bundle://proof/SB02/transcripts/dependency-floor.md`; no production source files changed.

Anti-stub audit: `bundle://proof/SB02/transcripts/anti-stub.md` found only an existing prompt line warning against placeholder evidence, not a stubbed package-update path.

Downstream dependency check: `SB03` may start because restore now passes and remaining risk is compile/API compatibility.

## Production Behavior Artifact Matrix

No new production signal, state, record, or event was introduced in `SB02`.
