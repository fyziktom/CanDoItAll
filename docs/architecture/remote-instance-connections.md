# Remote instance connections and shared definitions

## Importing agents, Simple Chats and workflows

Each feature publishes a definition and materializes a local draft through its own
application writer. The shared connection owns the authenticated HTTP read. The import
dialog owns the selected rows and an optional child dialog for choosing a provider
and model. A database switch or a changed source invalidates that opening.

The provider that authored a template can describe a preferred model using its public
publication identity and routing model. The consumer matches these against enabled
imports from the selected connection and publisher. A label shared by two companies
cannot identify a provider. The consumer stores its own provider profile ID and model
in the draft. Each model-dependent workflow step retains its own requirement, including
whether it needs image generation or structured output.

The parent dialog retains the catalog and selection when a user opens, cancels or
confirms a model choice. A batch records choices and import results per definition.
Applying one choice to several definitions requires compatibility with each affected
requirement. An accepted item is never resubmitted because a later item failed.
The local writer validates the binding again before saving. Imported definitions have
independent local identities and lifecycles. Discovery and import perform no inference.

The publisher exposes each resource through its own read scope. The definition contract
omits conversations, execution history, API keys, grants and local database references.
Avatar previews belong to the read contract for agents and chats. A missing avatar uses
the same placeholder as the local list. The consumer must validate image references
before rendering them.

Acceptance covers two publishers with identical provider names, unavailable models,
an explicitly selected local substitute, stale catalog data, permission checks and
retry after a partial batch. Component and browser proof exercise pre-import avatars,
stacked dialogs, cancellation, individual choices and applying a compatible choice to
the rest of a batch. Persistence tests read back the provider and model from the saved
agent, chat revision and workflow graph.

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
  The saved connection ID, public publisher ID and source template ID identify an
  import. Repointing a connection to another publisher cannot adopt the first
  publisher's local agent. A dedicated provenance record keeps imports independent
  of public provisioning keys. A changed catalog revision, including a skill-only
  edit, requires fresh discovery before preview.
  Refreshing a catalog must never overwrite a local customization. Future remote
  execution will require its own permission, operation and lifecycle.

## Extension points

In the product, open **Settings → Remote instances** or choose **Agents → Import template
→ Add instance**. Select a saved workspace secret containing the publisher's API token.
On the publisher enable API authorization, grant the token `api.shared-agent-templates.read`
and mark the desired agents as templates. Each token with this scope can read the draft
and active templates in that publisher workspace. On the consumer, choose an instance, review a
template's requirements and click **Import locally**. Open the local agent to select its
provider, configure runtime permissions and activate it for execution. Inline skill
instructions and embedded resources are copied. Registered or filesystem-backed skills,
tools and other runtime dependencies must be present locally under their declared kind
and key. Repeating an import from the same connection and publisher returns the existing
local agent for that template. An imported agent continues to use local
capabilities if its connection is later disabled or removed. A local provider selection
can itself point to an explicitly imported remote provider.

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
