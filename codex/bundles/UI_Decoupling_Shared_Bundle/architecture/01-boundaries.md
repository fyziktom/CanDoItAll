# Boundaries and the four graphs

```text
Production Web / composition
  -> native feature host / presentation session / effect adapter
     -> actual feature renderer
        -> dependency-closed light feature contracts
        -> shared product-neutral rendering libraries

Independent scenario host
  -> the same feature renderer and meaningful children
  -> deterministic view implementations and narrow scenario ports
```

These boxes describe responsibilities, not mandatory separate projects. Native modules may retain routes, backend services, authority, sessions and durable command dispatch. A long native class is not automatically an unfinished renderer; a short renderer with a backend-bound descendant is not automatically decoupled.

## Placement and seam choice

Product-neutral fields, overlays, canvas, charts and file rendering belong in the appropriate Components/FileTools sibling. Application-neutral composition belongs in the existing AppComponents/conversation families. Product-specific views and contracts belong to their feature. Do not put feature policy into SharedKernel merely to remove a project reference. Reuse existing RecordBrowsing and existing rendering projects under `src/MAF` as well as `src/UI`.

Presentation snapshots plus typed intents fit read views and bounded editors. A cohesive typed workspace view contract can fit a complex editor. Narrow read ports fit reusable paged pickers. Use the actual feature's needs rather than one universal state/DTO/controller abstraction. Focus, DOM references and bounded presentation-only JS can remain local. Business writes do not become UI responsibilities.

A UI-facing contract must not expose EF entities/proxies, DbContext, IQueryable, IServiceProvider, live repositories, runtime workers, credential-bearing requests, arbitrary delegates that resolve backend services, or a generic execute-anything bus. Passing these through an object or RenderFragment bag does not fix the dependency. Conversely, an established light value type or narrow file-content contract is not forbidden merely because its name contains a domain word.

When an implementation assembly owns otherwise suitable view values, extract the smallest dependency-closed stable family or introduce a meaningful projection. Do not copy a complete object graph one-to-one just to rename it a DTO. Preserve public namespaces, serialized payloads and identifiers unless explicitly authorized otherwise. Moving an assembly can change reflection/plugin/type identity even when its namespace stays the same: inspect dynamic consumers and use narrowly required compatibility forwarding.

## Inspect all four graphs

| Graph | Required evidence |
| --- | --- |
| Evaluated compile/build | Imported targets, source/package substitutions and recursive project references, not only `.csproj` text |
| Deployed/runtime | Resolved assemblies, packages, relevant dynamic loads and native assets |
| Rendered/effect | Actual children, slots, deferred/nested dialogs, event callbacks, file/voice/JS adapters |
| Asset/watch | Scoped CSS, JS imports, `_content` URLs, Tailwind inputs, generated styles and watched source |

A missing relevant dependency is an unresolved graph, not proof of its absence. A new/changed boundary guard must reject a forbidden transitive edge and an unexpected unresolved relevant edge. Do not catch FileNotFoundException and report the remaining traversal as a clean complete closure. Do not reject a legitimate content-only CSS link to Web as though it were a compile reference to Web.

Host slots are legitimate for authority/lifecycle composition, but the useful renderer inside an in-scope slot must also be represented by the same rendering family in the independent host. A placeholder saying “native dialog here” is not final completion. An intermediate slot is allowed only when tracked and removed/classified before the child's final closure.

Preserve actual invocation mechanisms. For example, the reviewed ProcessWorkspaceProjectionClient is an in-process adapter [P10], while separate process/project APIs have their own control-plane authority. Do not convert the former to HTTP, bypass the latter, or redesign authentication merely to extract UI. Verify native and HTTP consumers when a shared contract changes.

Use the configured Components MCP for non-WebGL primitive selection according to current repository rules. Do not introduce Radzen, copy sibling component internals into the application, or update package/runtime versions opportunistically.
