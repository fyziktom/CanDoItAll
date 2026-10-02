# Reference graph, public boundary and artifact delivery

Preserve existing PP1, Agent Editor, Workspace, Projects, Resources and their sandbox roots.
Snapshot evaluated MSBuild before/after (not only text ProjectReference lines). Include package
source rewriting by Directory.Build.targets, transitive assembly closures, public signatures,
scoped CSS/static web assets, Tailwind inputs, published assets and the actual dependency pair.
Shared protocol abstractions are not the same as provider implementation or runtime executors.

A small read-only UI projection is appropriate when current records live in ProviderManagement's
implementation assembly. Do not move service interfaces with EF entities to a feature leaf or
introduce an Infrastructure/Core -> UI reference. A neutral reusable component goes to its rightful
Components/FileTools/AppComponents owner; do not copy code or create a feature-aware common shell.

Retain namespace/enum token/public caller behavior when moving rendering-only types. Use assembly
forwarding where existing consumers need it and rebuild actual callers. Keep existing replay,
optimistic concurrency, publication lifecycle, imported restrictions and snapshot validation.
The current client materializer forces an OpenAI-compatible runtime shape while preserving
published models/capabilities; this is not a reason to apply a local OpenAI preset fallback (R21).

Read current .github/workflows/ci.yml: Components branch selection and FileTools pin have specific
semantics; do not assume the same manually checked-out branch is used by CI. Compare effective
content when commits differ before claiming a stale dependency. A reproducible final image needs
recorded source/dependency hashes, tags/digests and actual assemblies/static assets. Do not claim
success from an old pre-PP2 image or `--no-build` artifacts before rebuilding the owning projects.

Publish the independent sandbox locally into a task-owned directory and launch it without Web DI,
production DB, network credentials or original project content leakage. Verify BaseLib styles,
fonts and nested dialog scripts. Retained Request History remains product-host composition; it
must not enter the leaf dependency graph accidentally just because it is the sixth tab.
