# MAF 1.15 Execution Readiness Gate

## Decision

`GO` for SB01 baseline work. `HOLD` for package edits until `proof/SB01/a1-decision.md` records a passing A1 result.

The bundle is usable under the compatibility-first validator policy, but it does not conform to the current initiative-bundle schema without manual interpretation.

## Validation Evidence

- The bundle-owned validator passes all 76 committed bundle files.
- The current initiative validator reports 99 preparation findings:
  - 3 stale or missing paths;
  - 31 current-schema heading mismatches;
  - 57 non-portable source-reference findings;
  - 8 gate-format findings.
- Stale `.codex` paths used by executable instructions were corrected to `codex`.
- The pinned bundle SHA `59f558bc866d39d438b53f5f743dd5e87c2a6253` is an ancestor of execution head `797d7ce11205d630756ec9335b1b84295257a315`.
- Drift between those SHAs consists of bundle material only; package owners and product sources were unchanged.
- The manifest must be regenerated after execution evidence is complete because the bundle itself is being repaired and populated.

## Manual Semantic Gate

The following interpretations govern execution:

- subbundle order remains SB01 through SB08 with architecture gates A1 through A4;
- proof tiers in each subbundle remain binding;
- architecture and security assertions must be verified against the current repository, not accepted from the pinned bundle as facts;
- proposed redesigns are conditional on failing characterization evidence;
- missing current-schema headings do not waive any proof or acceptance criterion;
- no live 1.13 approval is migrated by rewriting private MAF JSON;
- rollback restores the pre-deployment state snapshot instead of promising 1.15-to-1.13 approval continuation.

## Architecture Baseline

CodeAnalytics snapshot `snap-20260728042508-0d7f96ce` loaded 100 projects, 3,156 documents, 1,985 types, 14,329 members, and 733 DI registrations without a blocking load error.

- There are no project or module dependency cycles.
- Microsoft Agent Framework SDK types are contained by `AgentFramework.Maf`, `Workflows.MafAdapter`, hosting, and tests.
- Canonical runtime and workflow contracts do not expose SDK types.
- No new project reference, façade project, or application-layer interface is authorized for this upgrade.
- The existing nested type cycle in `AgentReferenceDataCache` is unrelated and must not be refactored incidentally.

## Baseline Conditions

- Direct MAF references remain at stable `1.13.0` and A2A preview `1.13.0-preview.260703.1` until A1.
- An explicit restore succeeds when the process can read the user NuGet configuration.
- The restore baseline contains pre-existing `NU1903` warnings for `System.Security.Cryptography.Xml` `10.0.7`; these warnings are recorded and are not caused by the MAF upgrade.
- The first sandboxed restore failure is retained as environment evidence and is not classified as a product failure.

## Gate Owner

The primary implementation agent owns A1. SB02 remains blocked until the baseline package graph, discovery classification, targeted build/test result, state compatibility evidence, and rollback procedure are materialized under `proof/SB01`.
