# Native application journeys for PP3

Reuse current production composition and the existing governance/shared-provider harness. Fixture
transport responses may be deterministic; native owners, persistence, authentication, approval and
read-back may not be replaced by success mocks. Each journey records source/image pair, scope,
request/attempt/owner identity and exact observations, not only a screenshot or green toast.

## H-A — no automatic history reads

Use global History and a saved provider's History tab. Enter/leave repeatedly, type ordinary and
advanced filters, rerender parent and change the selected provider. Instrument the actual history
port to verify zero Search/GetMetadata/GetDetail calls until their explicit UI actions. On Search
record the immutable query and demonstrate draft changes do not alter its displayed applied state
or subsequent cursor page. No health/discovery/provider execution may occur from opening History.

## H-B — exact caller and provider provenance across instances

Using central and two clients, cause a bounded set of real test-chat/Agent/Simple Chat/Workflow
requests with two distinct managed credentials and at least default/non-default models. Query by
provider and credential identity. For the same fixed UTC interval and filters, global and provider
views must return the same matching attempt IDs. A similarly named model under another publication,
another caller or another client must not leak into that result set. Use exact IDs, not display-name
substring matching. Preserve original source/client mapping and independent local provider IDs.

Traverse at least two pages. Test a tie in timestamps, current cursor continuation and a stale cursor
after scope/authorization/partition changes. Rejected cursor never silently starts an unfiltered query.
Record logical attempt identity versus retries/transport requests separately; do not count a test
API's HTTP 200 as terminal success of every native operation.

## H-C — explicit metadata versus explicit content

Open one row and prove metadata read only. Then use a specific canonical owner link to request
bounded content. Capture the original owner kind/identity/version, authorized read result and supplied
capture/expiry flags. In a limited context allow metadata while refusing content; in another context
refuse the provider itself. A crafted callback or another owner ID must not bypass native checks.
Retiring the context while data is in flight must remove the old content and not reopen a successor.

## H-D — Agent over Project Structure

Reuse a project/node created through the supported native/UI fixture and a scoped agent. Read a
pre-existing hidden canary not repeated in the prompt, create a harmless text file, attach it, read
metadata and content back, then open/download it through the existing authorized UI. Validate exact
proposed tool arguments before approval and exercise one explicit denial. Verify stored IDs/bytes
and unrelated sibling content. Inspect the resulting History metadata and its canonical Agent owner;
request linked content separately and verify it belongs to that exact request/run.

The external model may be scripted, but Project Structure tools, approvals, workspaces, persistence
and content read-back are native. Closing History must not cancel a still running Agent. No broad
AllowAll or auto-approval to make the test pass, and no paid model call.

## H-E — Simple Chat and Workflow/TestLab

Use an imported provider default and an allowed non-default model. Save/reopen the Simple Chat
definition, obtain a completed native conversation message and explicit transcript page, then follow
its History canonical link. Run an accepted Workflow with exact immutable definition/input and a
native artifact/TestLab outcome, plus the HTTP-success/incomplete-output negative control. History
must label incomplete/refused/unpriced data correctly, not manufacture an artifact or full payload.

## H-F — credentials, revocation and context retirement

Issue narrowly scoped credentials in OWNED fixtures. Verify revoked credential denial and a rotated
credential with independent caller/key identity. Do not mix a server-side UI read with proof of the
public API's authorization. A source-managed catalog remaining visible is not permission to invoke it.
Use actual endpoints and safe public failure codes. Preserve snapshot contents and old credentials
only in private retained evidence, never new sources or export logs.

## H-G — cross-component regression

With the corrected Tooltip source: ordinary hover/focus, provider selection, PP2 nested source/
confirmation dialogs, History metadata+content dialogs, navigation to Projects/Workspace and circuit
shutdown. Verify B stays usable after A closes, owned references are gone, no new unhandled current
lifecycle exception appears, and previous Dialog fixes remain effective. Classify old framework and
maintenance warnings explicitly rather than changing the strict error oracle.

Source/published sandbox proof and native proof are separate. Where a full branch needs an unavailable
external prerequisite, record BLOCKED with exact impact; do not turn a gated-off test into Passed.
