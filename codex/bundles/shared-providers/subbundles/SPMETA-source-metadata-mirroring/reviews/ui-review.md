# SPMETA browser composition and screenshot review

Viewport: 1920x1080 desktop Chromium. Reviewed actual screenshots from final image 3, not
the user's before images or preliminary image 2. No BaseLib/CSS/mobile redesign was made.

## Findings while screenshots were visible

- Agent runtime dropdown: proof/browser/metadata-ui-closure-2/metadata-agent-models-open.png.
  The actual native popup is open inside the real dialog's right-hand grid column. Default
  and secondary model labels are readable, not hashes. No harmful column overflow, modal
  footer overlap or clipped options. Source-managed custom override is absent. Header and
  save/close footer remain visible; the dialog body owns any additional scrolling.
- Imported Ollama prices: proof/browser/metadata-ui-closure-repeat/metadata-ollama-client.png.
  First viewport shows provider identity, one real model, source-ownership text, private
  checkbox checked/disabled, and exact visible rates. No OpenAI default rows or local add/reset
  actions. Counts are compact badges; the right summary is supporting information.
- Resynchronized OpenAI: proof/browser/metadata-ui-closure-repeat/metadata-resynced-client.png.
  One remaining model, input 9.87, private checked/disabled, Shared/private mode. Source
  screenshot from metadata-ui-closure-2/metadata-resynced-central.png shows the saved checked
  flag. Central price-only rows are not treated as published callable models.
- The existing pricing table owns horizontal overflow for nine rate columns; the provider
  editor owns vertical settings scrolling. No new page-level overflow was introduced.
  DOM assertions compare all nine columns, not just the visible first viewport.
- Chat/image/vision: final metadata-ui-closure-2 screenshots 06, 07/07a and 08 show completed
  chats, image tool approval/resumption, and attachment analysis. The generated-image
  screenshot shows the artifact path, not a rendered bitmap; the runtime proof separately
  verifies that the new PNG exists.
- Source-empty prices are covered by the component rendered-state test (both private flags),
  not a fabricated browser fixture. The current source editor normally supplies default-model
  pricing, so no claim of a live empty-price screenshot is made.
- Existing narrow “New provider” and “Switch Agent” button wrapping and historical failure
  badges are outside this metadata change; no attempt was made to hide retained test history.

## Rejected preliminary proof

metadata-ui-final-2 and metadata-ui-repeat were not accepted as final private-state proof:
both sides could agree on the wrong false value. Visual review caught the source save bug.
Final UI helpers reopen the source, assert the requested private value after persistence,
then compare the imported state. Both final runs pass this stronger assertion.

The first final-image run hit an existing browser setup race: absence was checked before the
Secret vault loaded, and a newly filled name was mistaken for a completed save. The helper
now waits for the loaded vault and cleared editor after save. Fixed-time token comparison
remains unchanged and never prints token values.

Composition gate: PASS for the changed provider/model surfaces and realistic constrained
dialog column. Components MCP failed with Transport closed twice; existing shared controls
and layout were retained rather than inventing a new visual system.
