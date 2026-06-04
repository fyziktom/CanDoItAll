# Assumptions And Risks

## Assumptions

- The provider badge is correct because it reflects the merged AgentFramework catalog; the providers tab is the inconsistent surface.
- "TagsEditor" refers to the existing shared `TagEditor` component used elsewhere in the app.
- Local Ollama should be seeded as an additional provider, not a replacement for the existing remote fallback used by earlier repair work.
- MCP/Skill wizard can save catalog definitions; runtime testing of arbitrary new MCP servers remains dependent on user-provided commands and allowed tool names.
- File upload for a Skill should support uploading `SKILL.md` into an inline skill draft without implementing a separate persistent file-management feature.

## Critical Path Risks

- If provider tags are only a UI concern and not persisted in the catalog/editor model, tree grouping and future tag-based workflows will regress on reload.
- If capability tags are synthesized only in the UI, detail dialogs and wizard-created capabilities will not have durable metadata.
- If the details dialog allows editing built-in tool identity fields too freely, default runtime tool capabilities can be broken by accidental key/path changes.
- If MCP configuration editing is raw JSON only, the request for arguments/path editing is not actually solved.

## Validation Risks

- Component tests can prove rendering and persistence paths, but large-screen layout still needs browser proof for overflow, dialog clipping, and card density.
- New wizard upload behavior needs at least component-level proof that uploaded `SKILL.md` content maps into saved inline skill configuration.
- Arbitrary MCP commands cannot be live-tested safely as part of this repair; validation should prove catalog save/edit behavior and keep runtime verification explicit.

## Reopen Triggers

- Provider tab still shows fewer providers than the provider badge after reset or reload.
- Tags disappear after saving a provider or capability.
- Capability cards return to a single-column vertical list on desktop.
- Details dialog clips content, cannot edit MCP command/arguments, or blocks tag edits for built-in tools.
- Wizard-created MCP/Skill capability cannot be found in the capability inventory after save.
