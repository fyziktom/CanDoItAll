# Senior C# architect review

This pass reviewed the bundle through the lens of **module boundaries, persistence patterns, future mergeability, and C# implementation safety**.

## Main findings

### 1. Shared `AppDbContext` is still the right first move
The current CanDoItAll repo already uses assembly-driven EF registration and dual SQLite/PostgreSQL migration projects.  
A first process module should follow that pattern instead of introducing a per-project database early.

### 2. A future AgentFramework merge needs an adapter, not contamination
The Processes module should not gain a compile-time dependency on the uploaded AgentFramework repo in the first merge.  
Instead, it should define bridge contracts and correlation models so a later adapter module can implement them cleanly.

### 3. Registry ownership must be explicit
The current repos make it too easy to accidentally keep:

- CRM-HR business templates
- Workspace provider profiles
- AgentFramework research templates/providers/capabilities

alive as competing permanent sources of truth.

That would be a long-term maintenance problem and must be blocked now.

### 4. Process vs project separation needed to be tightened
The earlier bundle already avoided Workbench-as-truth, but it needed a clearer rule that Processes owns collaboration orchestration while Projects owns scope and delivery context.

## Actions taken

- Added `ADR-PROC-022`, `ADR-PROC-024`, and `ADR-PROC-026`.
- Added `PRM-F23` for cross-repo convergence rules and process-bound runtime correlations.
- Added Workspace to the integration model explicitly.
- Added new entities for executor correlations and typed process context links.
- Kept the bundle on the shared `AppDbContext` pattern with later extraction seams rather than premature storage fragmentation.

## Final architectural verdict

The bundle is now significantly safer to merge into CanDoItAll first and converge with AgentFramework later.
