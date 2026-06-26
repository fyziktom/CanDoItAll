# Requirement Traceability

| Requirement | Raw notes | Bundle destinations | Owning subbundle | Planned proof |
| --- | --- | --- | --- | --- |
| `R001` | `N001`, `N006` | `analysis/01-current-state.md`, `architecture/01-target-solution.md` | `SB02` | Source assertions for driver and tests. |
| `R002` | `N002`, `N003`, `N004` | `proof/SB01/`, `reviews/01-execution-report.md` | `SB01` | Live ComfyUI transcript and generated image artifact. |
| `R003` | `N005` | `inputs/sample/ImageGenerationFlux.json`, `proof/SB01/`, `proof/SB02/` | `SB01`, `SB02` | Payload/source assertion cites Flux node ids. |
| `R004` | `N003`, `N010` | `subbundles/01-comfyui-flux-connectivity-gate/README.md`, execution report gate rows | `SB01` | Blocked status if connectivity fails; no downstream code edits. |
| `R005` | `N007` | `architecture/01-target-solution.md`, `proof/SB02/` | `SB02` | Driver/provider tests and source assertions. |
| `R006` | `N005`, `N008` | `proof/SB02/`, seed source/tests | `SB02` | Seeded provider test and configuration JSON assertions. |
| `R007` | `N008` | Feature matrix tests and provider catalog source | `SB02` | Tests proving image-only provider behavior. |
| `R008` | `N007` | Driver tests | `SB02` | Focused failure-mode tests. |
| `R009` | `N009` | Project-structure source assertions | `SB03` | Source assertion that project structure uses `IAgentImageGenerationService`. |
| `R010` | `N004`, `N009` | `proof/SB03/`, execution report | `SB03` | Project-structure asset creation proof and content readback. |
