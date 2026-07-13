## Process Repair Merge Closure

Status: merge-ready for the completed process/runtime/template repairs; full terminal E2E deliberately deferred.

### What this closure proves

The process core and dispatcher remain generic. The .NET-specific behavior is
contained in the workspace command contract, the isolated .NET process driver,
and .NET process templates/agent instructions. No product-specific game or
calculator behavior was added to generic process runtime/application code.

| Root cause | Repair boundary | Evidence |
|---|---|---|
| Architecture emitted human display labels into `dotnet new` fields. | Core workspace CLI catalog, early .NET launch-contract validation, and architect/template guidance. | Display-label, topology-role, option-compatibility, and target-framework tests. |
| Application scaffolding silently used the installed SDK default framework. | Explicit `targetFramework` workspace command parameter propagated from the typed solution context. | App command and generated-project readback tests. |
| Current SDK generated `.slnx` although the declared primary file was `.sln`. | Isolated .NET setup driver accepts any authoritative solution candidate before running its already candidate-aware helper. | Bounded run `05823eab-3c07-48b4-95c2-bcc0db810291` passed `create-dotnet-project`; executor test covers the alternative extension. |
| .NET project-reference readback rejected the valid Windows path separator form. | `dotnet-solution-setup` template now supplies forward- and backslash-relative reference alternatives in one required-text group. | Template projection test verifies both `DotNetAppProjectReferenceRelativePath` variants for setup and repair. |

### Bounded E2E result

The last run was intentionally stopped after it reached the next previously
uncovered setup gate. It confirmed that the `.sln`/`.slnx` candidate repair
works. The following `add-test-project` failure was a deterministic completion
contract mismatch: the generated test project contained a valid backslash
`ProjectReference`, while the template required only the forward-slash form.
The template correction is included in this closure; a fresh terminal run was
not started so this work can be merged without another long observation loop.

Do not present this as a completed end-to-end delivery run. It is a merge-ready
repair increment with a clear next validation boundary.

### Architecture and validation evidence

- CodeAnalytics snapshot `snap-20260712090223-783d9d64`: no dependency cycles
  or blocking architecture diagnostics for Core, MAF, and Processes Modules.
- Generic runtime/application static scan: no `Tetris`, `Calculator`, `Blazor`,
  or `IndexedDb` matches.
- Web build: succeeds with no errors.
- Focused tests: 174 passed in isolated test processes across the .NET setup,
  launch contract/catalog, template projection/history, generic process-boundary,
  completion-contribution, and managed-script suites.

The only recurring build diagnostic is the existing `NU1903` warning for
`Microsoft.OpenApi` 2.0.0. It is unrelated to this repair and is not hidden.

### Next validation cut

After this increment is merged or otherwise deployed to the 5032 host:

1. Start the host with process template pack
   `2.1.51-template-path-variants` and agent seed `v55`.
2. Remove only the prior Tetris process-run projection and generated output,
   retaining the workflow/process-definition input.
3. Launch one fresh E2E run and observe from `add-test-project` through QA,
   repair, and browser-proof gates.
4. Treat a new blocker as a new root cause; do not use blind retries.

The combined parallel unit invocation can contend for the Windows global
`subst` drive alias used by workspace-path tests. The affected tests pass when
run in isolated test processes; this is recorded as test-harness follow-up, not
masked as a product or process-runtime fallback.
