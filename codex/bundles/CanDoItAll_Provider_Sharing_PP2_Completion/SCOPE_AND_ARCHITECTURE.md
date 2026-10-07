# Finish PP2, not another provider family

## Physical rendering census to complete

| Current producer family | Required extracted UI | Production responsibility retained |
|---|---|---|
| SharedProviderManagementPanel | Local/imported/runtime-only state; warnings and verification; actual confirmation overlay | Native reads/writes, recovery, publication/import identity and parent delivery |
| SharedProviderLocalPublicationContent | Eligibility/reasons, status, publish/unpublish actions | Native eligibility and stable public publication identity |
| SharedProviderImportedProfileContent | Alias/enabled draft, remote facts/models, retirement controls | Import/provider expected tokens and authorized native mutation |
| SharedProviderSourcesDialog | List, add/edit, credentials metadata, Test, discovery, selection, sync, disable/delete, nested outcomes | Source reads, endpoint validation, identity/network policy, dispatch/recovery |
| SharedProviderRefreshButton | Busy/result/retry/delivery rendering used by Thinking/consumers | Original source selection, actual synchronization and acknowledged delivery |

A compatibility wrapper may remain for public component identity, DialogService or DI ownership,
but it must compose the new real renderer. Do not leave a large child under the old module and
call an outer RenderFragment slot a completed extraction. Inventory every descendant and scoped
CSS/static asset; preserve existing CSS isolation and production route discovery.

## Preferred dependency direction

Production AgentFramework host -> PP1 + new SharedProviders.UI
New SharedProviders sandbox -> same SharedProviders.UI + deterministic scenario adapters
SharedProviders.UI -> necessary neutral BaseLib/SharedProviders abstractions and narrow UI values

No arrow from PP1, Agent Editor, Workspace/Projects/Resources or their sandboxes to the new feature
implementation. No arrow from Foundation, Core, domain/shared runtime or ProviderManagement to a
Blazor renderer. Runtime contracts stay runtime contracts. Source command DTOs currently live in
an implementation assembly; use narrow projections/bridges or a justified small closed contract
family rather than lifting the entire module. Do not duplicate policies or move EF entities.

One cohesive view interface or presentation + typed intent is enough where appropriate. It must
not expose IServiceProvider, a bag of unrelated services, arbitrary Func execution, live EF/IQueryable
or owner credentials. Presentation-only state may share a separate cohesive assembly only if real
production/sandbox consumers need it. Avoid a new project per dialog.

## Deliberate exclusions

Request History, provider test-chat/model maintenance, capability/team authoring, residual
conversation UI, Workflow authoring, Workbench and Processes are not extracted in this bundle.
History is a mandatory regression consumer. Workspace/API Access still owns credential issuance
and vault editing; PP2 only chooses metadata references. No new remote endpoint or driver feature
is necessary to move these renderers.

## Preserve completed work

Keep PP1 four-tab editor, native snapshot coherence, whole-agent A2, Projects P1/P2, Workspace,
Memory continuation and neutral picker/RecordBrowsing improvements. Independent graph guards
must check them, not merely the new root. Current canonical seams guidance wins over historical
counts and paths [S16].
