# Shared provider UI sandbox

This independent Blazor host renders the production Sharing, source connections,
catalog, confirmation and refresh components. It references the SharedProviders UI
leaf and its neutral dependencies only. It registers BaseLib and Blazor; it has no
module, database, vault, provider driver, worker or external HTTP client.

`SharedProviderScenarioStore` holds immutable accepted values independently of each
view's editable draft. Two editor sessions share this store and keep separate view,
read, draft and confirmation identities. Scenarios cover held writes, rejection,
uncertain receipts, failed read-back/delivery, concurrent edits, missing metadata,
unavailable models and a large catalog. This is deterministic UI proof, not native
owner or multi-instance proof.

Run the project with `dotnet run --project` and this project path. The root and
`/sharing/{Scenario}` routes use the same production components and scoped styles.
Publish this project independently to validate deployment assets. The required
application parity stylesheet is linked as content; no Web assembly is referenced.
Use 1920×1080 at 100% zoom for validation.
