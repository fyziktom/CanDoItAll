# Workspace API Access UI

Shared API Access renderers and presentation controllers over the dedicated API contracts
and real BaseLib controls. The production Workspace host and independent API sandbox use
this same tree. Generic Workspace Core does not reference this leaf.

The session owns status/access activation; issuance, token listing/confirmation and account
editing have separate lifetimes. Page reads retain accepted query identity. A bounded safe
ledger admits writes inside handlers and preserves unresolved outcomes across view refresh.
Sensitive disclosure/password state is retired with its original authorized editor.

The library does not sign tokens, hash passwords, grant permissions or access storage.
Production owners validate every request; projected scope choices are presentation data.
See [the boundary record](../../../docs/architecture/workspace-api-access-ui-boundary.md)
and [sandbox instructions](../../Sandboxes/CanDoItAll.Workspace.ApiAccess.UiSandbox/README.md).
