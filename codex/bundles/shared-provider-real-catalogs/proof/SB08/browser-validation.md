# Actual Playwright MCP validation

Target: Chromium, 1920x1080 desktop, source 5210 and client 5212 on final image6.
All setup changes used rendered UI controls. SQL was used read-only for usage evidence.
The original Playwright calls and returned values are in mcp-results.json and
source-client-parity.json. Initial delayed Blazor renders were handled with subsequent
reads and explicit locator waits; immediate stale lists were not accepted as proof.

## Configuration and behavior

Seven dedicated Thinking Proof agents were saved and reopened. Mini Low/High use the
same default model; Luna Low and Sol High use distinct real models. Source Default leaves
the override absent. Ollama Low/High use the existing real shared Ollama model. Exact
selected model/effort/allowed options are in thinkingSavedAgentSettings in mcp-results.json.

The separate source Thinking Proof OpenAI Responses provider was configured, published
and imported via UI with existing credentials. Only source default was changed Medium
to High between two runs of the same untouched client agent; no client refresh/sync
intervened. Explicit Mini Low still won over High. Source default was restored to Medium.
Actual responses and runtime details are correlated with the nine source records in
manifest.md. All explicit tested efforts show requested=effective and Compatibility
Preserved. No tool was invoked; the real SDK's built-in tool declarations were present.

Unsupported GPT-4.1 disables the effort selector and explains why. No agent was saved
for that negative inspection. Final unsaved source/client parity dialogs were closed.
The original UI Shared OpenAI Chat lists match exactly, including provider default and
12 explicit main model choices, naturally ordered. GPT-5.6 Luna is represented by the
provider-default row; gpt-5.6 without a suffix was not in this upstream's discovered
inventory. The 14-ID main allowlist is an intersection, not invented catalog membership.
Sol offers identical source/client Provider default, None, Low, Medium, High, Extra high,
Max. Extra high/Max were checked as available choices and in regression tests, not claimed
as additional paid live requests.

## Visual inspection

- client-sol-high.png: inspected normal agent-settings overlay; real Sol label, High
  enabled, source capability explanation and temperature omission visible. Save/Close
  are in the first viewport; no new cards or wasted form rows.
- client-effort-options.png: inspected focused native select. Chromium's page screenshot
  does not include the operating-system popup; this image is not claimed as a visible
  open-options capture. Actual option text and selections are independently asserted.
- client-unsupported.png: inspected disabled GPT-4.1 state; readable message, no clipping,
  Save/Close visible. The unsaved new-agent editor was closed without creating data.
- sol-high-runtime.png and ollama-high-runtime.png: inspected open runtime overlays.
  Completed/Succeeded, requested High/effective High, preserved compatibility and usage
  are visible together. The dialog body owns vertical scrolling and Close stays visible.
- client-final-chat.png: inspected normal chat surface with actual 323 reply and completed
  run. Composer and Runtime details are visible. Existing narrow Switch Agent wrapping
  is outside this model/effort repair; no unrelated chat layout redesign is claimed.

Other saved screenshots (Mini Low/High and original shared Sol) supplement these inspected
views. No mobile or reusable BaseLib change is in scope. Runtime diagnostics still show
internal route IDs for correlation; user-facing model dropdowns show real model names.

## Real limitations, not hidden successes

Preliminary Chat Completions reasoning/tool restrictions and the initial Responses SDK/
terminal failures were investigated, not relabeled as passing. The production temperature,
envelope and terminal fixes are covered by SB07 red/green tests. Final positive evidence
is only the image6 nine-request matrix. Model self-report and output-token differences
are not used to establish effort application. Historical failed/active counters remain.

All three instances are healthy on one image and retain their existing mounts. 5214 was
not cleared in this follow-up. Scoped source credentials expire; see manifest.md.
