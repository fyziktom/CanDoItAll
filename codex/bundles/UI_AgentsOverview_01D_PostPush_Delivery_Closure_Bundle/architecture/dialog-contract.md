# Dialog contract frozen before the follow-up implementation

Default navigation keeps existing close-with-null semantics. With at least one live instance lease, URI-canonical authority and path are compared ordinally; query and fragment are ignored. Different scheme/authority, path case or trailing slash is conservatively a page departure. Query/fragment navigation keeps the exact reference, result task and cancellation registration. Explicit close, token cancellation and service disposal retain their existing semantics.

Leases are per service, reference counted and idempotent. Disposing one cannot disable another. Service disposal detaches navigation once, cancels pending results and permits outstanding lease disposal safely. No route service, global owner or event bus is needed.

The actual page owns only its Overview dialog group. Scope change and leaving Overview cancel that group; unrelated same-page overlays remain. Catalog and capability top-level dialogs use their host token; AgentDetails owns its nested confirmations/wizard through its editor token. Defaults is page-owned. Provider/shared overlays are component-owned with cancellation and generation fences. Team icon picker is the newly identified unowned nested overlay; its public regression must fail before a correction. A pre-canceled token may also reveal an orphan in the existing service registration order; the new direct test adjudicates this independently of the accepted lease design.

Components MCP libraries/recommendation/contract/examples calls returned Transport closed. The real sibling DialogService, DialogHost and publishing tests are the source of truth for this audit; the unavailable connector is not represented as successful evidence.
