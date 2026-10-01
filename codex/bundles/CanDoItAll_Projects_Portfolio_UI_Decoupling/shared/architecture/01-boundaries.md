# 1 · Boundaries and contract choices

## Target dependency direction

```text
Production Web / composition
    -> module host / owner-facing adapter
        -> feature rendering library
            -> required feature contracts and light models
            -> required product-neutral/shared rendering libraries

Feature sandbox
    -> the SAME feature rendering library
    -> deterministic scenario data and narrow fake read ports
```

This is a dependency diagram, not a requirement to create one project for every box. The production module can legitimately keep routes, sessions, application-service calls, commands and effect adapters. UI extraction does not require splitting every module backend first. New direct persistence access belongs in its real application/data owner, never in the extracted renderer or sandbox. A pre-existing backend limitation outside the selected seam is recorded, not silently promoted to the architectural ideal.

Current placement is documented in `docs/architecture/ui-component-seams.md` [S01]. Feature-specific contracts are not made product-neutral by being used by multiple modules. The existing CRM/HR slice of `Projects.Contracts` is a useful precedent, not proof that all Projects contracts or UI are already extracted [S05, S26].

## Place code by meaning

| Responsibility | Placement | Do not introduce |
|---|---|---|
| Product-neutral layout, fields, dialogs, charts, file UI | Owning Components or FileTools sibling | Application-specific business semantics in a shared library |
| Feature-neutral application shell/navigation adaptation | `src/UI/CanDoItAll.AppComponents` or an established narrower family | References from the neutral shell to concrete module implementations |
| Shared paged records/pickers | Existing `CanDoItAll.AppComponents.RecordBrowsing` | A new duplicated record-browser family per module |
| Feature markup, presentation helpers, cohesive view contract | Existing/justified lightweight feature UI library | EF, repositories, production runtime hosting, provider secrets, Web composition |
| Stable feature value types and owner read/write contracts | Feature-owned contracts assembly or an existing appropriate abstraction | Duplicate DTO families with only identity mapping, or moving all contracts into SharedKernel |
| Navigation, read sessions, durable operation dispatch, authority checks | Module host/application owner/adapters, as appropriate | A UI controller that takes over domain authority |
| Scenario selector and deterministic fixtures | Development sandbox | Production routes or feature flags that select fake business services |

Existing rendering libraries also live under `src/MAF/Workflows` and `src/MAF/SimpleChats` [S17, S18]. Their path is not a reason to duplicate or relocate a working boundary. Reuse existing primitives after inspecting their contracts; use the Components MCP when available for non-WebGL shared component work, per current engineering instructions [S02]. Do not introduce Radzen.

## Two principal seams; neither is universal

**Presentation + intent** suits read surfaces and bounded editors. A host projects authoritative state into useful presentation values; children emit typed intent that identifies the record the user actually acted on. Do not send a row index that may name a different object after a refresh. Avoid serializing the entire page merely because a record is convenient.

**Workspace view contract** suits a complex editor with many related sections. The production page can implement a UI-declared interface with state/drafts and cohesive actions. The sandbox implements the same UI-facing contract. This is an intentional boundary even where implementation members forward to host methods: it replaces the renderer's dependency on a concrete page/module. It must not expose service instances, `IServiceProvider`, DB contexts, runtime objects or arbitrary “execute anything” commands. The actual CRM/HR implementation is a worked example [S11–S14].

**Narrow read ports** are a permitted variation for reusable catalogs/pickers. Read-only means the port's semantics, not merely a name starting with `Get`. Declare it in its appropriate light owner/consumer contract, pass a typed query and explicit context, return bounded materialized values, and provide deterministic sandbox implementations. Business mutation still passes to the real owner through the host/intent seam. A feature contracts assembly may contain command interfaces without that making the renderer itself a command owner; inspect what it uses and exposes [S04, S23].

Rendering-local state, element references, focus and bounded JS interop may remain in a component. Do not create an interface for each CSS calculation or event handler. Conversely, moving I/O into a code-behind partial does not create a compilation boundary.

## Contracts must actually remove the expensive edge

Move the smallest dependency-closed family of stable DTOs/enums where their implementation assembly is the obstruction. Preserve names/namespaces and payload shapes unless a real change is authorized. Namespace preservation alone is not binary compatibility: moving an assembly changes assembly-qualified identity. Inspect dynamic loading, reflection, plugin references and serialized type names when applicable; rebuild all affected source consumers and address required forwarding/versioning explicitly.

Do not expose EF entities/proxies, `DbContext`, `DbSet`, `IQueryable`, SQL/provider connections, live services, scoped repositories or unbounded persistence iterators. Copying an entity's entire graph into an identical DTO is not automatically useful either. Use bounded owner queries, correct permissions, pagination, cancellation and a meaningful snapshot/projection.

A view contract may mention browser rendering types such as `RenderFragment` and editor drafts. Domain contracts should not acquire Blazor or UI state merely to make that convenient. Retain pure rendering helpers in UI; backend policies remain enforceable without a component instance.

## Four separate graphs

1. **Evaluated build graph:** includes imported targets and package-to-project conversions, not only text in the `.csproj`.
2. **Deployed/runtime reference graph:** resolved assemblies, packages, dynamic loads and native requirements.
3. **Rendered/effect closure:** children, slots, delayed overlays and the services they actually reach.
4. **Asset/watch graph:** CSS, JS, fonts referenced by the application, Tailwind inputs and files watched by the selected command.

All four matter. A renderer may have no `[Inject]` and still compose a backend-bound descendant. An assembly reference guard may pass while an unused project reference still slows the build. A `*.Abstractions` name is not permission to ignore its transitive contents.

Current CRM guards inspect useful roles but also catch `FileNotFoundException` during reference traversal [S24]. Such a result is incomplete evidence, not a proof of the absent assembly's closure. For new or changed guards, reject unexpected unresolved references, or name precisely the intentionally excluded framework boundary. Complement reflection with evaluated references and resolved package/native assets. Test guards with a forbidden transitive dependency and an unresolved relevant dependency, not only a clean fixture.

## Cross-module and host boundaries

Reuse a real existing owner service in-process when appropriate. API-only migration is not required. Equally, preserve the existing Processes and Project Structure HTTP control plane; “not API-only” is not permission to bypass its admission and authorization [S02]. Keep optional provider unavailability explicit rather than quietly injecting a working fake in production.

Do not turn the application shell into an aggregate of module services. Do not create dependency cycles by placing a module-facing adapter in its renderer library. Host-owned slots are legitimate for lifecycle/effect ownership, but the slot's useful renderer must also be extractable and exercised in the sandbox, not replaced with a decorative placeholder.
