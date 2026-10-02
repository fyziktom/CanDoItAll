# Retained production integrations

## Sharing and source connections

The parent still composes `SharedProviderManagementPanel`, `SharedProviderSourcesDialog` and the actual SharedProviderRefreshButton through typed integration points. Keep their source/publication/credential owners and `SharedProviderChangeDelivery.ReconcileAsync` semantics. An event handler that merely invokes Refresh twice is not an equivalent implementation. [R12, R13]

Record scope, provider identity, activation and source revision before awaits. Successful metadata completion must not force an unrelated selected provider or replace a dirty local form. Imported/retired profiles keep their canonical source identity and constraints. `UnknownScope`/unconfirmed changes retain uncertainty rather than claiming nothing happened.

Maintain all six visible tabs. For source-managed profiles, render the same supported local tabs as read-only with unavailable/retired states. Do not silently convert an import into a local provider or relax its exact model identifiers, network policy, source ownership or deletion blockers. A source credential remains reference metadata; no renderer may resolve it. [R23]

The independent sandbox labels Sharing/History/source administration as retained integrations instead of shipping a fake success screen. It can demonstrate slot rendering and captured callbacks, but not certify their native behavior. Production tests verify the actual hosts still open, the correct identity is delivered, and a completed event does not steal a newer selection.

## Request History

Keep the actual request-history host, scoped to the exact saved provider. A New draft has no saved-provider history. Changing sections must not accidentally query another provider or reuse another scope's cached history. Do not extract request payload viewers, retention, export, purge or authorization in PP1. Read history metadata/content only as the existing host allows; no weaker read port is introduced just to populate the sandbox.

## Agent and Workflow consumers

Existing technical-agent/Core A2, Simple Chat and Workflow provider/model controls must consume the saved catalog through their original adapters. New Providers.UI is not a dependency of these consumers. Tests select the saved provider and prove identity/model/effort settings reach the actual native runtime. A passed dropdown test with a local mock list is not that proof.

Image-generation and Voice configuration may be inspected but no actual generated image/audio call is needed. Do not copy provider-policy rules into the renderer to make consumers appear compatible. Existing unknown/unavailable restrictions and default/explicit selections remain truthful.
