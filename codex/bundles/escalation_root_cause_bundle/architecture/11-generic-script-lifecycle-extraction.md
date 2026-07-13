# Generic script lifecycle; isolated technology semantics (2026-07-12)

## Trigger

The earlier repair correctly removed product and scaffold-specific behavior from
the process dispatcher. A follow-up boundary audit found that the remaining
`.NET` setup executor still owns two different responsibilities:

1. .NET-specific provisioning and solution topology operations.
2. Generic managed-script execution: write a script, verify its managed path,
   execute it through the governed workspace, collect receipts, and read back
   declared files under a rooted target.

The second responsibility is reusable for any governed process. Keeping it in
the .NET driver would make new domain drivers duplicate the same lifecycle and
would encourage further technology-specific behavior in the module runtime.

## Decision

Extract a single concrete `WorkspaceManagedScriptPlanExecutor` into the
Processes module's workspace-driver area. It receives a typed request with
managed script content and reference, side-effect manifest, working-directory
alias, output artifact reference, target root, and declared readback checks.

It performs only mediated workspace actions:

- write and stat the managed script;
- invoke `workspace_pwsh_run_script`;
- collect real current-execution receipts; and
- read declared files through `IWorkspaceFileService`, rejecting paths outside
  the declared product root.

It does not interpret a script as .NET, source code, an app, a test project, a
web UI, or a process-step name.

The `.NET` driver remains responsible for its legitimate isolated mechanics:
typed solution-context validation, provisioning-mode selection, `dotnet new`,
solution/project-reference script generation, and interpretation of .NET
readback expectations. The generic process runtime and dispatcher remain
opaque to both the script content and .NET semantics.

## Pattern selection

The selected pattern is a typed command request executed by one cohesive
service. It is deliberately not a new interface or service-locator catalog:
there is one concrete workspace lifecycle and its dependencies are already a
real infrastructure boundary. The existing runtime-owned executor dispatch
contract remains the extension seam for technology drivers.

## Rejected alternatives

- Moving `dotnet new`, solution membership, or project-reference operations to
  the generic executor would leak .NET semantics into generic runtime code.
- Putting executable scripts directly in templates would make templates own
  unsafe command interpolation and mediated workspace behavior.
- Adding a partial class to split the old executor would hide, not remove, the
  responsibility boundary.
- Keeping raw `File.ReadAllText` readback would bypass governed workspace
  receipts and make evidence weaker than an agent's normal execution path.

## Acceptance criteria

- The extracted executor has no `.NET`, app, test, Blazor, Tetris, calculator,
  scaffold, or fixed process-step vocabulary.
- It refuses empty/malformed plans and any readback path outside the rooted
  target; it has no silent fallback.
- It emits receipts for script write, stat, execution, and readback.
- .NET characterization tests still prove create, repair, and completion-gate
  behavior after the extraction.
- Generic script-lifecycle tests cover successful execution, failed execution,
  failed or mismatched readback, and outside-root rejection.

## Follow-up boundary work

The `dotnet-solution-setup` template currently carries stringified per-step
maps in `LaunchDriverActivations.Settings`. That is template-owned data, but it
duplicates typed per-step execution/completion contracts. After this extraction
is proven, migrate required product paths and file-content checks into typed
per-step completion policy and retire the map parser. Do not broaden the
generic runtime to accommodate that migration.

## Target-framework propagation correction (2026-07-12)

The observed setup failure was not an application defect: the architecture
contract selected a target framework for the test project, while the app
creation command silently used the installed SDK default. The result was an
incompatible app/test pair before feature work began.

The correction is a typed optional `targetFramework` parameter of the existing
generic `workspace_dotnet_new` command contract. The command builder validates
and translates that parameter to the `dotnet new --framework` CLI argument.
The .NET setup driver passes the authoritative bootstrap value only when it
creates the application project; its generated test-project script already
uses the same value. This preserves a narrow responsibility boundary:

- generic process runtime and dispatcher remain unaware of .NET framework
  selection;
- the workspace command remains a generic, reusable .NET CLI adapter rather
  than a scaffold or product policy;
- the .NET driver remains the owner of solution-context propagation; and
- the process template owns exact framework readback evidence.

No product names, scaffold-template assumptions, or UI-stack conditions are
introduced in code.
