# Current State

This compatibility file mirrors `analysis/01-current-state-review.md` for validator tooling.

The previous bundle successfully created the first narrow `CanDoItAll.Processes.Core` seed and kept it limited to route order, route planner, route eligibility, and trigger/route snapshots.

Current verified boundaries:
- `repo://src/CanDoItAll.Processes.Core/CanDoItAll.Processes.Core.csproj` references only `CanDoItAll.Processes.Contracts`.
- `repo://src/CanDoItAll.Processes.Core/Routing/ProcessDispatchRoutePipeline.cs` contains route pipeline and order assertion types.
- `repo://src/CanDoItAll.Processes.Core/Routing/ProcessDispatchRouteSnapshot.cs` contains route trigger facts, snapshot facts, and eligibility rules.
- `repo://src/CanDoItAll.Processes.Core/Routing/ProcessDispatchRoutePlanner.cs` contains pure route decision rules.
- No production process-driver API or runtime driver registry was introduced.

The next safe Core candidates are subprocess lifecycle facts, subprocess artifact mapping rules, artifact expectation snapshots/read models, and pure expectation matching/satisfaction descriptors.
