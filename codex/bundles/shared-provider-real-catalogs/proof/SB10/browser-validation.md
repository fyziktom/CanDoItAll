# SB10 actual UI validation

Playwright MCP, Chromium, 1920x1080. Configuration used rendered controls, not hidden
state, database writes or management API shortcuts. Provider search commits on blur;
locators wait for current Blazor render before accepting model choices.

## Verified behavior

- 5214 stale Sol/Medium draft initially shows unavailable. Refresh shared provider
  replaces the persisted catalog and preserves the current model and effort without
  saving the user's agent. Choices: Provider default, None, Low, Medium, High, Extra high, Max.
- Mini choices omit Max; Terra matches Sol; GPT-4.1 only permits Provider default and
  disables the selector. Final image repeats Sol, Mini and GPT-4.1 in both the agent
  editor and the Simple Chat definition editor. Simple Chat draft is cancelled unsaved.
- Source Proof Responses Sol override Low/High + Low default persists across reload.
  5212 read-only Thinking table mirrors it exactly. Sol High agent keeps High; another
  Sol agent uses source default Low. Mini agents retain their independent Low/High.
- Custom gptoss20b64k:latest manual Low/High + Low default persists through Health
  discovery and appears on both clients. Empty efforts produce explicit validation.
  Boolean controls show Disabled/Enabled; that exploratory draft was never persisted
  for GPT-OSS, which cannot disable thinking. Automatic reset restores Low/Medium/High.
- All temporary source overrides and the dedicated source-default agent's model are
  restored through UI; both clients synchronize. User agent drafts are closed unsaved.

## Inspected screenshots

- source-thinking-editor.png: automatic provenance/summary with visible Cancel/Apply.
- source-manual-sol.png: exact Low/High/default selection. It revealed full-width rows;
  final-compact-dialog.png supersedes the layout with three-column checkbox rows.
- client-mirrored-thinking.png: source-owned read-only table, model-specific defaults;
  the fifth-tab wrap is corrected by final-source-tabs.png, all five tabs on one row.
- 5214-final-sol-medium.png: readable source-supported message, Medium selected, Save
  enabled and refresh visible in the normal agent dialog. User draft not saved.
- final-sol-runtime.png: Completed/Succeeded, requested High/effective High, preserved
  compatibility, token usage and zero tool calls. Dialog body owns scrolling; Close visible.
- Supporting actual captures: Ollama negative/manual/after-health, model choices,
  final unsupported state and Simple Chat model options.

Native select OS popups are not claimed as screenshot-visible option lists. Exact DOM
option equality and actual selections provide that evidence. Early failed locator calls
(startup Continue, asynchronous previous-provider rendering, disabled no-provider field,
navigation auto-opening an agent and Save remaining open) were corrected and repeated;
they are not accepted as passing assertions. mcp-final-results includes an immediate
empty restoredSolOptions read; final model matrices supply the actual awaited proof.

No mobile scope, no sibling library changes. Final frontend scope: one existing-provider
tab, a focused per-model dialog, explicit refresh and existing scoped desktop grid.
