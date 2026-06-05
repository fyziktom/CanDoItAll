# Not Process Core Yet Cutline

Decision: Do not create `CanDoItAll.Processes.Core` in this bundle.

Rationale:

- The dispatcher still owns orchestration and mutation-heavy runtime behavior.
- The next seam is module-local finalizer decomposition, not durable domain extraction.
- Future Process Core will need stable, dependency-neutral value objects and policies. This bundle can prepare those locally but must not publish them as Core.
- Future process helper drivers need evidence/finalizer vocabulary, but production driver APIs should wait until the module-local boundaries prove stable.

Allowed:

- Module-local helper files under `src/CanDoItAll.Modules.Processes/Automation/Dispatch`.
- Moving nested finalizer value types to module-local internal files if tests prove parity.
- Extracting artifact content readers into module-local internal files.
- Pure transition request builder helper.
- Runtime invariant builder helper that does not persist or mutate by itself.
- Documentation-only driver-readiness map.

Disallowed:

- `CanDoItAll.Processes.Core`.
- `IProcessDriverPack`, `ProcessDriverPack`, or production driver registration.
- Moving EF entities or DbContext usage into Core-like projects.
- Moving UI/Razor/view models.
- MAF/Tooling/product dependency broadening.
