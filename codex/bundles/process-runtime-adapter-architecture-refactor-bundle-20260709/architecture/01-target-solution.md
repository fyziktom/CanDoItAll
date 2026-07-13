# Target Solution

See:

- `architecture/01-csharp-boundary-map.md`
- `architecture/02-csharp-dependency-direction.md`
- `architecture/03-csharp-pattern-selection-records.md`
- `architecture/04-csharp-testability-plan.md`

Target summary:

- `AgentFrameworkProcessExecutionAdapter` becomes a thin orchestration facade.
- Completion gates, receipt matching, managed artifact materialization, subprocess state resolution, recovery classification, and result conversion become top-level testable services.
- .NET/software-delivery lifecycle and runtime-owned tool-plan behavior move behind driver or tool-classifier contracts.
- Generic runtime/dispatcher/MAF receipt writer remain domain-free except for allowed external tool protocol/catalog ownership.

