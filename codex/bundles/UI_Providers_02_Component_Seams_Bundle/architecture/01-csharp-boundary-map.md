# Boundary map
- Canonical registry keeps validation, transactions, ownership and expected-token checks. Add provider-specific commit evidence and a read/repair path that does not replay Save/Delete.
- A provider editor command adapter maps actual backend receipts/exceptions. An editor mutation owner composes that port with the accepted read session, owns immutable submission/pending reconciliation and target cancellation; it has no component, notification service or DialogService references.
- ProvidersSession remains authoritative for target/draft/context. Add explicit committed identity/token reconciliation and metadata-only external change handling, not a second workspace store.
- Typed shared change scope belongs to the provider application contract so source/reconciliation producers return known IDs. Secondary effects cannot erase that receipt.
- Sharing target owner and sources overlay owner are independently testable application/UI sessions; Razor only maps inputs, renders state, and publishes current results. Owners capture request identity/concurrency values before await and check every continuation.
- API adaptation translates expected typed outcomes to stable public responses. It does not invent transactional truth or display upstream exception strings.
