# Remote instance connections and agent templates

## Current ownership

The provider-management module stores each endpoint in `Workspace_SharedProviderSources`.
Its stable ID, canonical base URI (including reverse proxy prefix), secret reference,
network policy, enabled flag and concurrency token already describe a reusable connection.
Provider catalog identity, ETag, last sync and publication selection have provider-specific
meaning. An imported provider is identified by its source and publication. Its local
profile forwards inference to the source. The provider writer already coordinates profile
updates, deletion guards, edit authorization and change notifications.

The Agents catalog owns local definitions, capabilities and editor lifetimes. Its ZIP
export supports backup and restore, including history, memory and workspace-specific
configuration. Its export API returns a producer-local result. The package clone path can
drop unresolved prerequisites. A remote template consequently needs a definition-only
protocol with explicit dependency resolution before it can produce a local agent.

## Decision

* Expose the existing records through a feature-neutral connection contract. Preserve
  existing IDs, secret references, storage schema and provider update semantics. Only
  the persistence adapter knows that the backing rows originated in Provider Management.
  The Settings host and inline creation use the same writer. Connection creation has no
  implicit discovery, import or execution side effects.
* Keep one selected connection per feature interaction. The workspace has no global
  current remote instance. Provider publication selection stays independent of template
  selection. All remote reads bind to the selected connection and its current revision.
* Use the existing destination policy at actual socket connection time, canonical
  base paths, per-request bearer authentication, bounded responses and deadlines.
  Resolve vault credentials on the server for the specific connection. A renderer
  receives selection metadata and callbacks. Raw credentials never enter the browser.
* A read-only publishing API exposes template summaries and a selected versioned
  definition. Require an appropriate read permission and enforce template visibility
  on direct lookup. Send explicit instruction and capability fields. Omit run data,
  source credentials, local filesystem paths, executable configuration and permission
  grants. Materialize portable inline skills and resolve installed prerequisites
  using kind and stable key. Reject unresolved prerequisites before creating an agent.
* A user explicitly imports one selected template. Create an independent local
  definition with a fresh identity, local runtime configuration and local authority.
  The connection and source template identity distinguish companies that reuse keys.
  Refreshing a catalog must never overwrite a local customization. Future remote
  execution will require its own permission, operation and lifecycle.

## Extension points

| Consumer | Remote selection | Local owner and binding |
| --- | --- | --- |
| Providers | Selected published models | Existing import reconciliation, profile and relay |
| Agents | Selected template and portable skill content | Local agent, locally registered tools, local execution |
| Simple Chats | Definition, system instructions | Simple Chat definition owner binds a local provider. Conversations stay local |
| Workflows | Template graph and component identities | Workflow draft service resolves local bindings before creating the graph |
| Processes | Process template, role and step contracts | Process owner checks compatibility and maps local roles and drivers |
| Projects | Structure and template relationships | Project owner allocates local identities and maps ownership and dependencies |

Each new consumer supplies its own payload, publication rules and import semantics.
The registry does not deserialize arbitrary domain objects or expose a generic execution API.

## Validation

| Boundary | Focused verification |
| --- | --- |
| Registry and providers | Existing rows become visible in Settings. Create from either surface, select from the other, test two sources and local profiles, stale revision, disable and guarded deletion. Re-run source sync and hybrid selection integration tests |
| Transport and publication | Test base-path preservation, allowed destinations, credential isolation, disabled source, non-success, timeout, malformed and oversized response, permission and direct non-template lookup |
| Local import | Test equal source template IDs across instances, existing local definitions, mapped tools, portable skill content, missing prerequisite, repeat import and all-or-nothing writes. Verify source history, credentials and executable configuration are excluded |
| UI | Test Settings deep links and deferred rendering, creation from a feature, selected-source changes during a read, failed discovery, selection and local readback. Exercise the production navigation and compare the provider dialog regression suite |
| Repository | Build affected projects in standard configurations, run affected tests locally and enforce the portability baseline. Use GitHub CI for the broader suite |
