# Two validation lanes: deterministic integration and genuine-model rehearsal

The latest user request explicitly requires practical UI Agent work before the demonstration.
That is more than a scripted provider returning prewritten tool calls. Keep two evidence lanes.

## Deterministic integration

Use the existing isolated source/two-client fixtures for controlled transport, exact identities,
permissions, race and failure tests. Native owners, tools, approvals, files, Process/Workflow
admission and storage are real. Only the external model response is scripted. Label this lane
`deterministic`; it cannot satisfy a genuine-model rehearsal group.

## Genuine-model rehearsal

Use an already configured, approved actual model through the production UI. Its tool decisions
and analysis must not be scripted. Use non-customer synthetic data and specifically assigned
capabilities. The runtime model need not be the coding model. Prefer an already functioning
local non-scripted model where it supports the required task; do not silently substitute a
weak or incompatible model just to avoid reporting a provider prerequisite.

Check permission, credentials, model availability, tool/image/vision capability and budget at
entry, while requesting PGP unlock, not after completing all implementation. Do not expose
credentials or copy them into fixtures, prompts or reports. Native secure configuration stays
in its original private owner. Missing credentials are not permission to create accounts,
buy credits, change billing, or broaden provider access.

**Paid calls require an existing explicit authorization covering this rehearsal, or a one-time
confirmation at entry.** The latest request authorizes the rehearsal task, but this package
does not invent a financial authorization. Proposed conservative ceiling for that confirmation:
**USD 10 total, at most 64 language/vision provider attempts and four image-generation attempts**,
including failed calls and retries. These are ceilings, not target usage or price estimates.
Use the lower of these ceilings and any existing approved limits. The historical exhausted
40/40 test budget is a different ledger and must not be reset. If no paid approval is available,
use suitable configured local models or mark the exact cloud lane BLOCKED_AUTHORIZATION.

Reserve the estimated maximum cost before each outbound call using current configured pricing
and token/image limits. Record attempts, terminal statuses, actual known usage and conservative
cost separately. Missing reliable cost information cannot be treated as zero. If a remote
budget cannot be enforced, stop that lane rather than exceed the approved amount. Do not keep
retrying until the test turns green; inspect the first failure and allow at most one purposeful
retest of a corrected case inside the remaining budget.

Start with a compact end-to-end rehearsal, not dozens of exploratory prompts. Cap per-run tool
steps, output tokens, wall time and fixture paths using current supported controls. Do not
raise global production timeouts or auto-approve all tools. At approval inspect actual argument
values: project/node, relative path, overwrite mode, target content and the exact operation.
Unexpected proposed effects must be rejected, even when their tool name is generally allowed.

## Evidence and blockers

Record actual provider/profile and model IDs plus friendly labels, real outbound attempts,
run/session and tool/approval identities, source pair and resulting native artifact IDs.
Never record secrets, raw authentication headers or sensitive provider URLs. A model claim,
HTTP 200, final Completed badge or zero-call gated test is not sufficient.

If a prerequisite blocks a genuine run, continue safe deterministic testing, complete the
source changes and deliver a runnable candidate with an exact blocked status. Do not claim
`demo_ready=true`, report a fixture as live, or spend the old exhausted allowance. Provide the
short actual command/UI procedure needed to finish the pending lane in Wednesday preparation.
