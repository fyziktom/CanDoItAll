# Current repository drift versus the older overlay

The current repository is not simply a stale copy of the prior bundle overlay. It already contains some later fixes or alignment changes.

## Preserve current repository versions of these files
- `README.md` — Current repository contains later fixes or alignment changes and should not be reverted by the remediation bundle.
- `src/CanDoItAll.Modules.Processes/ProcessTemplatePackLoader.cs` — Current repository contains later fixes or alignment changes and should not be reverted by the remediation bundle.
- `src/CanDoItAll.Modules.Processes/ProcessTemplateProjectionService.cs` — Current repository contains later fixes or alignment changes and should not be reverted by the remediation bundle.
- `src/CanDoItAll.Modules.Processes/ProcessesModuleServiceCollectionExtensions.cs` — Current repository contains later fixes or alignment changes and should not be reverted by the remediation bundle.
- `src/CanDoItAll.Modules.Processes/ProcessDevelopmentSeedService.RuntimeSeeds.cs` — Current repository contains later fixes or alignment changes and should not be reverted by the remediation bundle.

## Practical decision
The remediation bundle intentionally does **not** overwrite these files. It focuses on materializing the missing template-pack tree, adding audit helpers, and strengthening tests while preserving the newer repository state.
