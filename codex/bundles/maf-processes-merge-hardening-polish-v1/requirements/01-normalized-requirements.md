# Normalized Requirements

| ID | Requirement | Observable success criteria |
| --- | --- | --- |
| R01 | Remove transient Codex work-package artifacts from tracked repo content. | `git ls-files` returns no `codex/bundles/**`, no `codex/bundle-exports/**`, no root `01-execution-report.md`, and no transient ZIP proof files. |
| R02 | Tighten ignore rules without harming bundle-preparation skill tooling. | `.gitignore` ignores transient `codex/bundles/**`, `codex/bundle-exports/**`, and Codex ZIP/log outputs; `codex/skills/bundles/**` remains tracked and usable. |
| R03 | Remove bundle/subbundle/SB/INV naming from active tests. | Source scan over tracked active files finds no `SB###`, `INV###`, historical bundle slugs, or bundle/subbundle wording in test method names outside allowed bundle skill tooling. |
| R04 | Add durable repository guardrails for future leaks. | Unit tests or a deterministic scan fail if tracked transient artifacts or forbidden test naming patterns return. |
| R05 | Preserve MAF -> Processes decoupling. | Existing MAF boundary test still passes and no MAF project/source references `CanDoItAll.Modules.Processes`. |
| R06 | Keep Process Core deterministic and dependency-clean. | Process Core references only allowed contracts; source scan finds no Modules/Drivers/Infrastructure/AgentFramework/EF/UI/plugins dependencies. |
| R07 | Move software-delivery proof/runnable-app stack logic behind domain ownership. | `.NET`/Blazor/JavaScript/runnable-app/product-path proof heuristics are moved from generic dispatcher partials into a verification-only domain driver or explicit domain adapter seam, with dispatcher delegating through that seam. |
| R08 | Keep drivers verification-only. | Driver packages have no runtime host/registry/selector/DI discovery/file IO/network/process mutation/manager/scheduler/workflow surfaces. |
| R09 | Keep verification gateway explicit and typed. | Gateway exposes explicit typed methods only; no `Verify(lane, object)`, no `dynamic`, no generic dispatch map, no reflection/discovery. |
| R10 | Preserve working process behavior. | Process-focused unit/integration tests pass; live multi-team app delivery smoke is preserved or rerun when environment exists. |
| R11 | Keep changes merge-safe. | No broad dispatcher-runtime isolation, no UI rewrite, no template rewrite beyond necessary references and tests. |
