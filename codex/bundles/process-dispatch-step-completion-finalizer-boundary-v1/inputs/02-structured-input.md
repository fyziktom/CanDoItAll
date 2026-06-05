# Structured Input

## Primary objective

Prepare the next implementation bundle for incremental dispatcher decomposition after the completed tool-validation/recovery boundary work.

## Hard constraints

- Do not create `CanDoItAll.Processes.Core`.
- Do not create production process driver packs or `IProcessDriverPack`.
- Do not introduce MAF -> Processes/Projects/Workbench references.
- Do not rename process runtime tools.
- Do not weaken artifact validation, required-tool, recovery, finalizer, access, or approval behavior.
- Do not move EF entities, DbContext access, Razor components, UI view models, storage implementations, MAF composition, or Tooling contracts.
- Do not create small/medium/mobile proof artifacts.
- Browser validation is `N/A` for runtime/service-only work. If UI unexpectedly changes, use large desktop/PC only.

## Next target

`ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs`.

## Driver preparation position

The next bundle may improve driver readiness through module-local vocabulary and documentation only. It must not implement production driver APIs.
