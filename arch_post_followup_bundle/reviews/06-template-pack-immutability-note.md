# Template pack immutability note

## Decision
- Keep `ProcessTemplatePackLoader` scoped for now.

## Why
- `ProcessTemplatePack` still exposes nested mutable graphs through `List<>`-backed models such as `ProcessTemplateDefinition.LocalRoles`, `ProcessTemplateDefinition.LocalArtifacts`, `ProcessTemplateDefinition.Steps`, and the mutable resource objects loaded from JSON.
- The loader itself is already thread-safe within scope because it uses `Lazy<ProcessTemplatePack>` with `ExecutionAndPublication`.
- Promoting the loader or pack to a shared singleton would widen the lifetime of a mutable object graph without deep immutability or defensive cloning. That would be a real thread-safety regression, not an optimization.

## Safe boundary
- `ProcessTemplateEditorModelFactory` now owns the shared template-to-editor mapping rules for catalog, library, and projection paths.
- `ProcessesModuleServiceCollectionExtensions.cs` explicitly keeps the loader scoped until the pack graph becomes deeply immutable.

## Follow-up bar for broader caching
- Only revisit singleton or cross-scope caching after the loaded pack graph becomes deeply immutable or the loader returns defensive clones instead of shared mutable instances.
