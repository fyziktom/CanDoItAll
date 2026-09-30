# W3 — Residual configuration rendering and final Workspace census

## Separate neutral rendering from trusted resolution

`ConfigurationSchemaFallbackRenderer` is currently a reusable schema loop in Workspace. It already delegates each field to `CanDoItAll.Configuration.UI.ConnectorConfigFieldEditor` but accepts module `SecretListItem`. Move the neutral schema-rendering responsibility into the appropriate existing Configuration.UI leaf, using its safe secret-reference projection. Avoid introducing another generic schema framework. [WS20]

`SettingsRendererHost` resolves an explicitly requested renderer using key, owner, trust level and schema version. That resolution is a legitimate production-host responsibility. Do not move its registry or arbitrary component types into a backend-free sandbox. Preserve the registered renderer's expected parameter names/types through a narrow trusted host adapter. [WS21]

The correct branches are:

- no renderer requested at all: generic neutral fallback;
- valid fully resolved trusted request: that exact approved component;
- incomplete, missing, wrong owner, wrong trust or wrong schema request: explicit failure, no permissive fallback.

Renderers cannot gain authority by changing their displayed key. Do not introduce `Assembly.Load`, name-based type probing, reflection service location, insecure URI loading or a host callback that bypasses registry validation. Do not bundle private secret values into the neutral projection; use reference options only.

## Inventory and implementation

Search actual current consumers of both components, the registry, descriptor/resolution types and contributed settings renderers. Trace Workflow and Plugins configuration callers where present. Do not assume that every file in the directory is used or that every wrapper is disposable. Update all owning imports, tests, static styles and registration paths affected by a move.

An existing host may stay as a narrow façade if it preserves a real consumer contract and owns trust/lifetime adaptation. Record why it stays. It must delegate the actual fallback renderer to the neutral UI library; retaining a complete copied renderer behind an interface is not decoupling.

Preserve raw configuration state, field-level issues, explicit callbacks and form lifetime across rerenders. A new state object passed by a different editor is not an instruction to overwrite the original submission after an await. Cache/project secret options at a reasonable ownership boundary; do not add backend reads during rendering.

Use the existing Configuration.UI/shared component testing pattern. Add a scenario host for its actual fallback if no independent representative one exists; don't claim a placeholder custom renderer tests real trusted composition. The registered custom-renderer production test and negative registry cases remain necessary.

## Acceptance

Prove ordinary text/number/bool/reference fields, validation and raw error text, missing secret references and non-English input content without changing English UI labels. Prove parameter round-trip to at least one real registered renderer and all explicit mismatch failures. Exercise the owning Workflow/Plugins UI configuration path through a production browser and read back its exact saved definition without executing external mail/remote effects.

Then complete the final recursive Workspace census from WORKSPACE_COMPLETION_MAP.md. Every remaining file gets an extracted, justified host, retired or blocked disposition. Preserve route discovery and all existing Settings entries. No remaining true render surface can be marked finished solely because it still compiles in the module. Stop new feature extraction after Workspace; the next work is the application regression campaign.
