# Native planning review and submission

The existing AgentChatSession owns accepted workspace reads and their generations. It
will own the planning review and its open/unconfirmed state. Clear this authority-bearing
projection before every read; a same-target failed read must not leave a stale approval
available. Validate the original run and chat identity before accepting a review. The
existing AgentChatPanel only routes origin-bound intents and reconciles the original
operation; no new execution partial, service registration or project reference is needed.

AgentFramework.UI renders immutable review data through existing Alert, Stack, Cluster,
TextBlock, Button and controlled Dialog contracts. The transcript/composer stay primary.
A compact status opens a wide dialog whose shared body owns scrolling and footer retains
the explicit actions. Plain text is escaped and preserves line breaks. Up to 100 tasks
remain bounded by the 300,000-byte review contract. Show original project/run identity,
generation, saved policy, basis timestamp, charged/remaining work and unresolved state.
Do not add polling, live budget estimates or a second authorization model in the renderer.

The exact reviewed expectation and publication binding travel in a typed action intent.
Session validation rejects retired views, changed reviews, hidden actions and a closed
dialog. The owner still rechecks authority, freshness, generation and budget at dispatch.
Use the existing activity orchestrator to admit a saved planning action without capturing
the currently selected context or calling the ordinary approval continuation. The host
tracks the original agent/chat/handle/profile/view generation. Closing or navigating away
does not cancel an already admitted write; late results cannot alter another opening.
Failure disables the current review until an explicit fresh read. A confirmed action
followed by a failed refresh reports that distinction. The live composer is untouched.

An explicit Plan project tasks composer action carries ProjectTaskPlan through the
existing retained submission. It is a per-send selection, not a sticky mode. Reject
attachments before clearing text; backend admission requires its trusted original Project
Structure source and canonical Workspace/Agent settings. Retained failure copies preserve
the selected purpose, while restoring text never sends it or restores authority. Generic
tool approval cards are suppressed for planning decisions; they cannot substitute for
reviewing the actual prepared tasks. Ordinary HTTP chat start remains ordinary: adding a
planning flag to an endpoint without trusted source authority would not create a valid
planning entry. Saved planning actions are available through the reviewed HTTP contract.

CodeAnalytics baseline `snap-20261019112531-bfeafecb` contains UI (43 documents), Components
(18), and the AgentFramework module (249). The direction remains Module -> Components ->
UI; UI sees Models, not Core or module owners. The two existing cycles are one internal
module cycle and the image-generation provider/builder type cycle. There are 43 large-file,
183 complexity and two dependency advisories. All 189 workspace errors are existing NuGet
advisory diagnostics (WS0002), not compiler errors; two DI registrations are unresolved
by static analysis and factory registrations are partially interpreted. Do not call this
snapshot green. Direct affected Release builds and real-host proof remain required.

The existing panel is already large (184 members). Keep new policy/validation with its
session and execution admission with its orchestrator, add only the actual host routing,
and avoid another partial file. A separate service for three presentation fields or a
new project would add no boundary. Test the session's stale/failed-read and exact-action
rules without constructing the panel, test the orchestrator without the runtime, then
exercise the real host's duplicate/foreign/delayed actions and preserved draft in bUnit.
Include the existing submission tests because the send request changes. Add a backend-free
sandbox scenario and large-desktop Playwright evidence for review scrolling, footer/focus,
two actors and navigation during an admitted action. Proof layers retain their limits.

The browser proof reuses LiveUiHost and the existing isolated PostgreSQL server. Only the
external model endpoint is a bounded local HTTP responder: native context capture,
orchestration, policy admission, planning journal, material basis and task publication
remain real. Seed a scoped Agent and Workspace policy, start from Project Structure, then
make a second actor's real task write after review capture. Approval must pause without
publishing. An explicit refresh obtains a new generation; delayed provider completion must
preserve later typing, and approval must publish that generation exactly once. The existing
native retained-submission browser journey also runs for explicit planning purpose, covering
navigation and unknown admission without claiming its controlled orchestrator proves the
runtime. The independent sandbox exercises 100 long tasks, scrolling, visible footer,
keyboard focus, unknown outcome and paused states. No new production dependency is needed.

The actual native browser path exposed a capture-boundary defect before provider work:
the host selected planning only after ordinary context composition had copied the UI's
attachments. Keep the planning coordinator's attachment rejection. Carry the typed
PlanningOwner into the existing turn-capture command and invocation factory instead.
Capture still checks the route, database generation, canonical permissions and original
project lifetime. Planning skips ordinary fragment composition, attachment freshness
and external-target grant projection because none of that material enters its request;
its participant separately captures and validates the authoritative material basis.
Persist the original source/authority/transition metadata, the original user prompt and
an empty model-context digest. Detached planning retains its intent and fails admission;
it must never silently become ordinary chat. The registry only copies already published,
bounded in-memory references, so expanding its contract or adding another service would
not remove database work. No dependency edge, service registration or partial is added.
Direct capture tests cover ordinary freshness, skipped planning payloads and unchanged
authority denial; the activity test verifies purpose before dispatch. The native browser
journey remains the required real composition proof. The failed four-case candidate is
retained separately (three passed, native planning failed before the provider).

The passing native journey's screenshot exposed one remaining reconciliation omission:
saved planning actions did not publish the existing original-source completion
notification, so the Project Structure canvas could remain stale after a successful
publication. Reuse the existing completion factory and notification hub in the activity
orchestrator after the confirmed result. The saved run metadata selects the source;
current navigation must not be captured. Paused/cancelled/failed actions do not claim
successful publication. The existing subscriber owns refresh and rejects retired
surface lifetimes. Add direct outcome/source notification tests and require the native
canvas to display the new task titles without manual refresh. Wait for explicit review
reload to settle before taking final browser evidence.
