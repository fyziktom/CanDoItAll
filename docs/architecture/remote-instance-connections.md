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
* A user explicitly selects the templates to import. Create an independent local
  definition with a fresh identity, local runtime configuration and local authority.
  The saved connection ID, public publisher ID and source template ID identify an
  import. Repointing a connection to another publisher cannot adopt the first
  publisher's local agent. A dedicated provenance record keeps imports independent
  of public provisioning keys. A changed catalog revision, including a skill-only
  edit, requires fresh discovery before preview.
  Refreshing a catalog must never overwrite a local customization. Future remote
  execution will require its own permission, operation and lifecycle.

## Operator steps

1. Enable API authorization on the publishing instance. Grant the token the read scope for
   each catalog the consumer needs. Use `api.shared-agent-templates.read` for agents,
   `api.shared-simple-chats.read` for chats and `api.shared-workflows.read` for workflows.
2. Mark an agent as a template. For a Simple Chat, open **Sharing** on its catalog card
   and publish the definition. For a workflow, select it in the catalog, open **Share
   workflow** and publish it. Use the same control to withdraw sharing.
3. On the consumer, open **Settings → Remote instances**, add the publisher and select the
   saved workspace secret containing its token. Each import page can also add a connection
   to this list. To use the publisher for inference, synchronize its provider catalog and
   import the desired provider on the consumer. The token also needs the shared provider
   catalog and invocation scopes for those operations.
4. Open the import dialog from the agent, Simple Chat or workflow catalog, choose the
   instance and select one or more definitions. Agent and chat rows include avatar previews.
   Review required capabilities, workflow components and proposed provider/model choices.
   A matching published model from this instance is preselected. For an unavailable or
   ambiguous match, **Import selected** opens **Choose provider and model** over the
   original selection. Choose a compatible local replacement or configure the shared
   provider in a second tab and use **Refresh providers**.
5. For a batch, confirm each item's bindings or use **Apply to all compatible**. A workflow
   may need separate chat, vision or image-generation models. A workflow without a model
   dependency can be imported directly. After import use **Open local copy** to edit the
   draft. A retry from the same source and publisher preserves prior local edits.

The consumer creates each definition through its domain writer. Chat imports use the
native create receipt and start with an empty conversation history. Workflow imports
create the draft and its component copies in one transaction. An unresolved local
agent, subworkflow, private executor setting or unsupported executor must be resolved
on the publisher before that workflow can be shared. Agents copy inline skills and
their resources. Registered tools and filesystem-backed skills must exist locally under
their declared kind and key. Running any imported definition remains a separate local
action. A model bound to a shared provider sends inference to that provider's instance.

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
