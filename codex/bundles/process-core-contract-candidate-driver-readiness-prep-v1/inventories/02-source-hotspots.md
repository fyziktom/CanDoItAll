# Source Hotspot Inventory

| Area | Source | Remaining concern | Risk | Owning subbundles |
| --- | --- | --- | --- | --- |
| Route models | ProcessDispatchRouteModels.cs | Source payload bridge remains for candidate/claim/outcome | High | SB004-SB006 |
| Finalizer app | ProcessDispatchFinalizerApplicationService.cs | Dispatcher aliases and delegate-based bridge | High | SB007-SB009 |
| Hydration | ProcessDispatchCandidateHydrationService.cs | EF + workspace + binding + recovery + cooperation in one service | High | SB010-SB012 |
| Materialization | ProcessDispatchRouteServices.cs / pre-exec handlers | Pure facts and journal/rerun side effects still close | Medium | SB013-SB015 |
| Subprocess | ProcessDispatchSubprocessRuntimeService.cs | Lifecycle and projection persistence mixed | High | SB016-SB018 |
| Direct agent | ProcessDispatchDirectAgentRuntimeService.cs | Delegate still accepts dispatcher candidate and returns dispatcher outcome | High | SB019-SB021 |
| Projection/validation | ProcessProjectionModels.cs / ArtifactValidation helpers | Full execution details still leak into some DTOs | Medium | SB022-SB024 |
| Static wrappers | ProcessRunAutomationDispatchService.* | Pure wrappers not fully classified | Medium | SB025-SB027 |
| Driver readiness | architecture docs | No production API yet; needs safe lane map | Medium | SB028-SB030 |
