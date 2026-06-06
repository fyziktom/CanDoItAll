# Source Artifacts

| Artifact | Bundle reference | Purpose |
| --- | --- | --- |
| Raw architect/user request | `bundle://inputs/00-original-request.md` | Preserves the literal source request, including no-Core/no-driver and no-functionality-removal constraints. |
| Current state review | `bundle://analysis/01-current-state.md` | Captures branch state and the previous route-pipeline proof baseline. |
| Source hotspots | `bundle://inventories/01-source-hotspots.md` | Identifies production and test files that anchor the route-handler facet boundary. |
| Route-stage matrix | `bundle://inventories/02-route-stage-matrix.md` | Captures canonical route order and route-stage side-effect ownership. |
| Target architecture | `bundle://architecture/01-target-solution.md` | Defines the top-level handler and route-facet target shape. |