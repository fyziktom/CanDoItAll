# Dependency direction

Current exact project files show:

- Projects.Contracts -> SharedKernel.
- Projects.UI -> Projects.Contracts and existing BaseLib package/framework.
- Infrastructure -> Infrastructure.Abstractions and SharedKernel.
- Projects -> Contracts, Infrastructure, SharedKernel, existing AgentFramework
  Core/Models, Projects/Files UI, AppComponents/Navigation and FileTools integration.
- Existing Workbench adapters consume Projects-owned source authority ports.
- Processes.Runtime does not reference product modules, MAF or persistence.
- AgentFramework.Core does not reference product modules.

CodeAnalytics `snap-20261016142537-4d8bda80` loaded four projects/224 documents but
reported workspace diagnostics including NuGet audit errors and partial DI interpretation.
It cannot establish complete absence of dependencies. The direct Infrastructure Release
build succeeded with the existing NU1902/NU1903 warnings; no audit setting was disabled.
Existing Infrastructure module-level cycles are recorded, not repaired incidentally.

Target: retain current directions. No new contract project is required for P01a or the
Projects data-only command additions. If shared policy vocabulary is required by all
Agent/Process/Workflow models, first verify their existing neutral contract dependencies;
extract only those immutable types, never an implementation service, to an appropriate
existing inward layer. A new project requires an evaluated graph justification.

Forbidden new edges: Contracts/UI -> Infrastructure/EF/Web/runtime implementation;
Projects -> Workbench/Processes implementation; Process Core/Runtime -> MAF/provider
SDK/product modules; generic Infrastructure -> Projects/Workbench; any cycle solved by
moving domain services into SharedKernel. Existing outward integrations do not license
additional inward coupling.

Proof: build each changed production project, run compiled Projects boundary guards,
refresh scoped evaluated graph where references/types move, inspect the actual csproj
diff, and run composition/API/tool smoke. P01a has zero project-reference changes and
needs direct helper tests plus affected build; later graph changes reopen this record.
