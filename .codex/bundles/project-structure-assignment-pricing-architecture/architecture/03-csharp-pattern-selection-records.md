# C# Pattern Selection Records

## Strategy — resource cost estimation

### Problem force

Four resource kinds use different algorithms, data owners, failure modes, and future extension paths. The current service switch owns all implementations and incorrectly treats an AI agent as CRM workforce.

### Selected pattern

Strategy, registered as one implementation per `ProjectStructureTaskResourceKind`, with a thin exact-match dispatcher.

### Rejected alternatives

- A larger switch is closed to extension and retains the current responsibility concentration.
- A dictionary of delegates in the old service is testable but keeps construction/provider dependencies centralized.
- A factory plus strategies is unnecessary; the dispatcher already performs exact strategy selection.
- Service location is forbidden.

### Dependency direction and test seam

Workbench owns the contract. AgentFramework contributes its implementation in the existing dependency direction. Every strategy is instantiated with fakes directly; the dispatcher is tested with arbitrary fake strategies.

### Anti-fake-separation proof

The old service must not reference CRM, workflow, or process estimator dependencies and must not contain resource-specific quote methods.

## Policy/service — authoritative estimate refresh

### Problem force

The same lifecycle rule must apply to UI preview, Gantt/agent services, and canvas submission paths. A pure helper cannot obtain a quote, while embedding the rule in each caller would duplicate it. Existing progress, schedule, and free-text status cannot reliably prove whether work happened.

### Selected pattern

A small application service composing the strategy dispatcher with explicit `ProjectTaskExecutionState`. It returns the normalized estimate and quote outcome. `NotStarted` refreshes, historical states preserve, and legacy `Unknown` fails closed.

### Rejected alternatives

- Client-only refresh leaves API paths inconsistent.
- A decorator around each mutation service would duplicate resource discovery and obscure transactional ordering.
- A state-pattern hierarchy is excessive for the enum policy.
- Inferring execution from progress or scheduled dates risks repricing historical work.

### Test seam

Direct tests fake the quote service and prove new/`NotStarted` refresh, unavailable clearing, historical preservation, and `Unknown` fail-closed behavior.

## Resolver — multi-assignee scalar projection

### Problem force

Three callers duplicate mapping and assume the canonical assignment set has at most one row, while repository architecture explicitly permits person/agent overlap.

### Selected pattern

A cohesive pure resolver class returning an immutable resolution record. No interface or factory is added.

### Rejected alternatives

- Another partial helper perpetuates fake modularity.
- Silently taking `First()` inside each caller is nondeterministic and leaves compensation broken.
- Redesigning the dialog as multi-select is broader than the reported defect.

### Test seam

Direct tests cover empty, single, unique-primary mixed, ambiguous mixed, unsupported, and input-order independence. Component/service tests prove mixed direct mutation is unavailable and unchanged saves preserve the complete set.
