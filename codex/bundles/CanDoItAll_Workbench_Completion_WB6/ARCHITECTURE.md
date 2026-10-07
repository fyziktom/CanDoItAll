# Architecture: finish without reconnecting completed modules

Follow current canonical UI component seams and preserved shared v3. The purpose is faster
independent UI development and safe in-process composition, not an HTTP-only rewrite.

Preferred new family: `src/UI/CanDoItAll.Workbench.Execution.UI` and
`src/Sandboxes/CanDoItAll.Workbench.Execution.UiSandbox` for Workflow/Process integration.
A minimal stable `Workbench.Execution.Contracts` is optional if actual shared value types
require it. The final delete confirmation may instead complete existing Structure/Content UI.
Use a cohesive view contract for this large stateful workspace or presentation+typed intents
for bounded subviews. Do not build duplicate records containing the whole native page.

The module owns mutable sessions, exact selected target, accepted snapshots, current actor,
project admissions, native reads/writes, prepared requests and durable run identities. The
renderer owns actual markup, typed event wiring, display calculations, focus and bounded UI
interop. Shared small definitions/enums may be reused only after evaluating their transitive
graph. An Abstractions filename is not permission to reference all Process application services.

Keep `ProcessLaunchAuthority`, `PreparedRequest`, owner services, credential values, live EF
entities, factories, `IServiceProvider` and long-running runtime objects out of the leaf.
Candidate role/provider display data are projections; use friendly model names while retaining
native routing IDs only in the owner request. No automatic model/provider fallback.

Use actual neutral BaseLib/CanvasLib/RecordBrowsing and already-extracted renderers. Product
meaning stays out of Components and feature-neutral AppComponents. Inspect selected component
contracts through the available Components MCP for non-WebGL work; lack of the MCP is not a
reason to skip source inspection and evaluated graph validation.

Preserve all four graphs: evaluated MSBuild, runtime/assembly, rendered/effect and asset/watch.
Measure the existing protected roots and the new family. Unexpected unresolved references are
not a successful negative test. Include forbidden-transitive-reference controls.

Preserve public compatibility where types are moved: namespace alone is not assembly identity.
Use justified type forwarding/rebuilds and exact static-web-asset paths. Do not redesign all
package/build wiring or add test projects to the product solution.

Handler-level per-opening admission, immutable submission, original input/version/role IDs,
current checks after every await and independent finally cleanup are required. Cancellation
retires the view; it does not undo an accepted external effect or native commit. Readback retry
must not silently perform provisioning, matching, model invocation, graph create or Start again.

Keep code/comments, product UI and maintained engineering documentation English. The separate
human review supplied outside this package may remain Czech. Historical sealed shared files
are not rewritten to satisfy a new style or census.
