## C# Architecture Gate Result

Status: Pass with live-validation follow-up

### Scope

The setup driver must carry the architecture-selected target framework into
application scaffolding without teaching generic process runtime or dispatcher
about .NET project files.

The first fresh run also showed that an agent can write a human-facing template
display name into a machine-readable solution-context field. That is a contract
authoring failure, not a product failure: `dotnet new` needs a short identifier
such as `webapi`, while a display label is not executable input.

### Findings

| Severity | Finding | Evidence | Required action |
|---|---|---|---|
| Pass | Framework selection is an explicit workspace command argument, not an opaque template-string fragment. | `IWorkspaceCommandExecutionService`, `WorkspaceCommandExecutionService`, and `WorkspaceCommandPlanBuilder` carry one optional `targetFramework` parameter. | None. |
| Pass | The .NET driver only propagates its authoritative launch value to the application scaffold. | `DotNetSolutionSetupRuntimeExecutor` passes `null` for `sln` and the contract value for the app; test-project generation already uses the same contract value. | None. |
| Pass | Product-specific policy remains template-owned. | The setup template checks the generated app file for `<TargetFramework>${DotNetTargetFramework}</TargetFramework>`. | None. |
| Pass | CLI template catalog has one owner. | `WorkspaceDotnetNewTemplateCatalog` is consumed by both the workspace command builder and the isolated .NET launch-contract factory. | None. |
| Pass | Invalid display labels fail before child launch and no catalog is duplicated in Processes. | The .NET driver validates only the catalog contract; template and specialist-agent guidance explain the required machine fields. | None. |
| Follow-up | The fresh end-to-end run must reach and pass nested setup. | Root run `41be6a3e-bc6d-4064-a686-f53c3b854e39` was launched after rebuild and clean-state verification. | Observe to terminal state; reopen only on real diagnostic evidence. |

### Dependency direction

No `.csproj` or project-reference changes were made. CodeAnalytics snapshot
`snap-20260712074725-783d9d64` covers Core, MAF, and Processes Modules and
reports no cycles or blocking errors. Its only diagnostics are the pre-existing
`Microsoft.OpenApi` 2.0.0 vulnerability warnings.

### Partial-class policy

No partial class or nested architecture boundary was added. The existing
workspace command adapter remains the CLI protocol boundary; the process
driver remains the technology-specific propagation boundary.

The small static catalog is deliberately a workspace-command contract, not a
process-runtime policy or a product/scaffold rule. It replaces duplicated
allow-list knowledge without a service locator, new provider, or new project.

### Testability proof

- `DotNetSolutionSetupRuntimeExecutorTests` proves the app command receives
  `--framework net8.0` and that its template-owned readback needs the emitted
  project framework.
- `WorkspaceCommandExecutionServiceTests` proves valid explicit arguments,
  rejects invalid values, and rejects a free-form inline `--framework` value.
- Template and prompt projection tests prove the source-of-truth guidance and
  exact completion policy survive loading.

### Closure decision

The architecture change passed its focused validation and may proceed to the
next live-validation cut. The later merge closure, including the bounded E2E
evidence and remaining terminal-run boundary, is recorded in
`13-merge-closure-e2e-handoff.md`.
