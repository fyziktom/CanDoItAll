# Dependency, assets and delivery proof

Record evaluated MSBuild graphs before and after, including package-to-sibling substitution in
Directory.Build.targets. Record resolved runtime assembly and static-asset origins, rendered child
effect closure, and actual watch files. Textual .csproj references alone are insufficient.

Verify the new SharedProviders UI has no ProviderManagement implementation, EF, Core/runtime,
provider executors, Web or vault service dependency. Verify existing PP1, A2, Workspace, Projects,
Files and Resources UI/sandbox closures do not newly acquire that implementation or a reverse edge
into the new feature. Reflection/public signature guards must reject forbidden transitive and
unexpected unresolved references and cycles. A different namespace is not an assembly boundary.

Move real CSS/JS/assets with renderers, register only needed BaseLib/theme/interop in the sandbox,
check Tailwind discovery and source/published files. Retain source refresh as a composable effect
without making PP1 depend on a module. Inspect all call sites and serialized/assembly type names
before moving public types; use supported compatibility forwarding when actually needed.

Record exact application/Components/FileTools commit + uncommitted delta, build configuration,
compiled hashes, final app image digest and sandbox publish manifest. An image tagged latest or a
matching assembly version is not source identity. Local source mode and CI/ref selection must agree
on how an eventual consumer obtains changed dependencies. Do not edit siblings merely to increase
scope; the current Components/FileTools were not changed by S0.

One final-source image is reused by all three app instances. Any executable or asset repair after
that build requires affected rebuild/retest. Documentation-only additions may reuse executable
proof after a checked diff. A post-extraction PASS cannot cite only the baseline S0 digest.
