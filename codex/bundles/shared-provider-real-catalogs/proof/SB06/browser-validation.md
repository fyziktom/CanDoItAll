# Desktop Playwright MCP validation

Target: 1920x1080, actual rebuilt Docker apps; no mobile claim. Main agent inspected
the screenshots, not only the accessibility tree. Tokens were held only in a local
Playwright call variable and never returned, logged or saved in evidence.

## Scope picker and token administration

- Source 5210: token list dialog absent before opening; component call-counter test
  additionally proves zero registry searches until mounted.
- Picker has all 12 declared scopes. Clear disables confirmation after the Blazor
  render completes. Selecting catalog.read and invoke fills exactly those two scopes.
  Reopen, Clear, Cancel preserves the textbox. Textbox and icon are adjacent.
- Issued a test token using the form. Searched by its display name in the management
  dialog; only metadata is present after page reload. No bearer is recoverable there.
- Final complete lifecycle was checked in ONE Playwright call, retaining the same
  token variable: source /api/shared-providers/v1/catalog returned 200 when active,
  200 after cancelling revoke, 401 after confirmed revoke, 401 after confirmed delete.
  Search then returned zero records. Test tokens were removed through the UI.
- Screenshots: scopes-dialog.png, tokens-dialog.png, token-revoked-final.png.
  Scope cards fit two columns; footer remains visible. Token table is readable, with
  search and paging controls on single rows. Dialog body owns scrolling for longer data.

## Preserved client and fresh client

- 5212 original legacy JWT expired at 2026-08-27T20:24:37Z; this was verified from the
  signed token's expiry without outputting its value. Initial Test/Discover rejected it.
- Issued a replacement through 5210 UI, selected ONLY catalog.read and invoke through
  the checkbox dialog, and updated the existing 5212 secret through Settings/Secrets.
  Lifetime is 480 minutes. No provider definitions/history were replaced.
- 5212 Test then returned Catalog connection verified. Discovery shows all 3 original
  publications: Ollama 72 models, OpenAI Chat 128, Image 5. Cancel preserves selection.
  client-catalog-final.png is inspected. Existing provider degraded badges are retained;
  this check proves connection/catalog access, not a new inference-health assessment.
- After recoverable reset, 5214 shows 0 / 0 providers, 0 sources, 0 imports and 0 tokens.
  Connections opens from the icon toolbar with no selected provider. Add source opens,
  then Cancel/Close saves nothing. fresh-5214-providers.png and fresh-5214-add-source.png
  were inspected: compact toolbar/filter, readable nested overlay and visible actions.
- Final browser left on http://localhost:5214/agents?tab=providers for manual setup.

## Failed harness attempts (not accepted as proof)

- A first immediate disabled-state read raced the Blazor render; explicit state wait
  confirmed the actual disabled control. A reload click raced prerender; waiting for
  the interactive workspace tablist made the action real.
- First HTTP probe used the wrong unversioned catalog URL (404). Another attempt used
  a global variable across isolated MCP calls (401 due to missing token). Neither is
  revocation evidence. The final one-call same-token lifecycle above owns acceptance.
- Secret expiry inspection first used an incorrect nested-input locator and then
  unavailable Node Buffer. The final DOM-local atob decode returned only expiry metadata.
- The component investigation matched the scope textbox instead of its checkbox.
  Restricting the selector to input[type=checkbox] fixed the test, not production behavior.

No paid model calls, new chat definitions or fresh-client setup were performed in SB05/SB06.
Historical real inference proof remains in SB02/SB03 and is not represented as rerun here.

## Final image2 repeat

admin-dialogs-20260827-2 includes the final short-ID search correction. Actual MCP
repeated the same-token lifecycle: 200 / 200 / 401 / 401, then deleted the test token.
Separate non-mutating search first returned 0 for an unknown name, then 1 for the exact
12-character ID displayed for the existing client token. It waited for completed search,
not only an old result count. See mcp-image2-result.json and token-id-search-settled.png.
The earlier token-id-search-final.png caught a transient loading state, so it does not
own the settled-row visual claim. Final fresh provider count remained 0 / 0.
All three instances run the same final digest; runtime-image2-final.txt records health,
zero fresh database tables/token files and isolated grants. This update did not reset data.
