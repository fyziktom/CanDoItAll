# Phase Plan

## Execution Order

- SB01 entry audit, branch hygiene, and baseline proof.
- SB02 execution snapshot contract design.
- SB03 refactor gate A: contracts and guardrails.
- SB04 client mapping foundation.
- SB05 dispatcher result/detail migration.
- SB06 failure normalization boundary.
- SB07 refactor gate B: coupling reduction proof.
- SB08 receipt observation helper foundation.
- SB09 required-tool and artifact-lineage consumer migration.
- SB10 refactor gate C: boundary consistency review.
- SB11 runtime smoke and large-screen policy check.
- SB12 final red-team and next isolation cutline.
## Subbundle Dependency Map

```mermaid
flowchart TD
    SB01[SB01 Entry audit] --> SB02[SB02 Snapshot contract design]
    SB02 --> SB03[SB03 Gate A contracts/guardrails]
    SB03 --> SB04[SB04 Client mapping foundation]
    SB04 --> SB05[SB05 Dispatcher result/detail migration]
    SB05 --> SB06[SB06 Failure normalization]
    SB06 --> SB07[SB07 Gate B coupling proof]
    SB07 --> SB08[SB08 Receipt observation helper]
    SB08 --> SB09[SB09 Required-tool/artifact lineage migration]
    SB09 --> SB10[SB10 Gate C consistency review]
    SB10 --> SB11[SB11 Runtime smoke and large-screen policy]
    SB11 --> SB12[SB12 Final red-team and next cutline]
```

## Critical Subbundles

- SB02 is critical because missing snapshot fields will invalidate dispatcher migration.
- SB04 is critical because it is the only allowed AgentFramework runtime adapter.
- SB07 is critical because downstream receipt/helper work is untrustworthy if dispatcher still consumes AgentFramework detail types.
- SB10 is critical because it checks source size, coupling counts, and scope drift before final smoke.

## Phase Gates

### Gate A after SB03

- Contracts neutrality proven.
- No production behavior movement yet except tests/guardrails.
- No core/driver projects.
- Large-screen-only policy recorded.

### Gate B after SB07

- Dispatcher no longer consumes AgentFramework execution result/detail/exception types.
- Client mapping/failure behavior covered.
- MAF/Tooling product dependency scans clean.

### Gate C after SB10

- Receipt observation helper in place.
- Required-tool/artifact lineage tests pass.
- Dispatcher source-size/coupling review recorded.

### Final Gate after SB12

- Prepared/completed bundle validator passes.
- Full solution build passes.
- Targeted provider/policy/process tests pass.
- Final cutline says whether next work is artifact-validation isolation or still execution-boundary cleanup.

## Execution Cadence

After each refactor gate, Codex must stop and write a short gate review before continuing. Do not continue into downstream subbundles when gate proof is incomplete.
