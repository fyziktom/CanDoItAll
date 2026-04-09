# Phase Plan

## Phase Sequence

1. Phase 00:
   source-of-truth convergence and realistic seed baseline
2. Phase 01:
   module shell, authoring domain, staffing authoring, and authoring UI
3. Phase 02:
   runtime state machine, governance, work briefs, trust, journals, and replay-ready imports
4. Phase 03:
   cross-module projections, future bridge seams, and management/runtime UI
5. Phase 04:
   metrics, economics, decision intelligence, conformance, and improvement loop

## Subbundle Dependency Map

```mermaid
flowchart TD
    S01["01 Canonical ownership and cross-repo convergence"]
    S02["02 Development seed packs and scenario baseline"]
    S03["03 post-implementation-bundle-phase00 generation"]
    S04["04 Process module shell and storage foundation"]
    S05["05 Process definition lifecycle and governance model"]
    S06["06 Role templates, contracts, and staffing authoring"]
    S07["07 Canvas authoring and component-first UI foundation"]
    S08["08 post-implementation-bundle-phase01 generation"]
    S09["09 Runtime state machine, approvals, and decision rights"]
    S10["10 Work briefs, decision records, and artifact trust"]
    S11["11 Journal, forensics, operating modes, and import/export"]
    S12["12 post-implementation-bundle-phase02 generation"]
    S13["13 Project, activity, validation, and process projections"]
    S14["14 AgentFramework bridge and registry convergence"]
    S15["15 Live runtime canvas and management governance UX"]
    S16["16 post-implementation-bundle-phase03 generation"]
    S17["17 Metrics, economics, capability gaps, and decision intelligence"]
    S18["18 Conformance, learning, and improvement loop"]
    S19["19 post-implementation-bundle-phase04 generation"]

    S01 --> S02 --> S03
    S03 --> S04 --> S05 --> S06 --> S07 --> S08
    S08 --> S09 --> S10 --> S11 --> S12
    S12 --> S13 --> S14 --> S15 --> S16
    S16 --> S17 --> S18 --> S19
    S05 --> S09
    S06 --> S09
    S07 --> S15
    S10 --> S14
    S11 --> S17
    S13 --> S15
    S14 --> S17
```

## Critical Subbundles

| Subbundle | Why it is critical | Extra proof required |
| --- | --- | --- |
| `01-canonical-ownership-and-cross-repo-convergence` | Locks single-source-of-truth boundaries before code is written. | Explicit cross-repo ownership matrix and no unresolved duplicate-registry decision. |
| `02-development-seed-packs-and-scenario-baseline` | All later validation quality depends on realistic fixtures. | Seed scenarios, data ownership, and fixture strategy reviewed and accepted. |
| `04-process-module-shell-and-storage-foundation` | Every later process entity, migration, and route depends on it. | Build, migrations, and module registration proof. |
| `05-process-definition-lifecycle-and-governance-model` | Defines canonical process identity, lifecycle, and version truth. | Domain, persistence, and publication guardrail proof. |
| `06-role-templates-contracts-and-staffing-authoring` | Locks the role-first model and staffing semantics. | Cross-module integration proof with CRM-HR and role-template snapshots. |
| `09-runtime-state-machine-approvals-and-decision-rights` | All live execution and later analytics depend on this state model being correct. | Integration tests plus dependent-flow smoke before downstream work proceeds. |
| `10-work-briefs-decision-records-and-artifact-trust` | Explainability and trust structures must exist before external bridge or analytics work. | Journaled decision records and artifact metadata proof. |
| `14-agentframework-bridge-and-registry-convergence` | Protects long-term mergeability and prevents duplicate registries. | Explicit bridge contracts, ownership tests, and no compile-time dependency proof. |

## Phase Gates

- Phase 00 gate:
  subbundles `01` and `02` closed, then `03` must generate and validate `post-implementation-bundle-phase00`.
- Phase 01 gate:
  subbundles `04` through `07` closed, UI proof recorded for authoring surfaces, then `08` must generate and validate `post-implementation-bundle-phase01`.
- Phase 02 gate:
  subbundles `09` through `11` closed with runtime, journal, and trust proof, then `12` must generate and validate `post-implementation-bundle-phase02`.
- Phase 03 gate:
  subbundles `13` through `15` closed with cross-module and UI proof, then `16` must generate and validate `post-implementation-bundle-phase03`.
- Phase 04 gate:
  subbundles `17` and `18` closed with analytics and conformance proof, then `19` must generate and validate `post-implementation-bundle-phase04`.
- Universal gate:
  the next phase may not start while the previous post-phase repair bundle still contains `Ready` or `In progress` repair subbundles.
