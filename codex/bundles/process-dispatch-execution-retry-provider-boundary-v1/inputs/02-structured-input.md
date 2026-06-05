# Structured Input

## Raw Note Closure Seeds

| Raw note | Literal wording | Owning subbundles | Planned proof |
| --- | --- | --- | --- |
| RN-001 | Continue smaller isolation steps toward a future Process Core and future process helper drivers. | SB01-SB44 | Gate manifests, line-count proof, source assertions |
| RN-002 | Do not rush Process Core unless it is clearly ready. | SB04, SB44 | No-core/no-driver scans |
| RN-003 | Preserve all original functionality; this is refactoring and architecture hardening only. | SB04, SB08, SB12, SB16, SB22, SB28, SB35, SB40, SB44 | Focused tests and semantic gate proof |
| RN-004 | Plan more phases/subbundles and force refactor gates every few subbundles. | SB01-SB44 | Dependency map, critical gate rows, execution report |

## Normalized Constraint Seeds

- Keep all helper/coordinator extraction module-local to `CanDoItAll.Modules.Processes`.
- Do not create public Process Core contracts, production process driver APIs, driver registries, or driver packages.
- Do not change retry counts, provider fallback policy, recovery journals, no-progress compression, or completion decisions.
- Treat UI/browser/mobile proof as prohibited unless implementation unexpectedly touches UI files.
