# Implementation Prompt

You are implementing `process-dispatch-artifact-satisfaction-evidence-boundary-v1` on branch `maf-processes-refactor`.

- Execute subbundles in numeric order from SB01 through SB32.
- Do not skip entry gates, closure gates, or critical gate proof.
- Do not create Process Core, production process-driver APIs, driver registries, or driver packages.
- Preserve behavior exactly; do not simplify artifact satisfaction by changing branch order.
- Keep helpers module-local under `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/`.
- Keep side effects in existing orchestration paths; pure helpers must not perform file, storage, DbContext, service-scope, transition, or agent mutation side effects.
- Record proof under `bundle://proof/SBxx/` and update `reviews/01-execution-report.md` while evidence is fresh.
- Browser validation is `N/A` unless UI files are unexpectedly changed; do not create small/medium/mobile proof artifacts.

