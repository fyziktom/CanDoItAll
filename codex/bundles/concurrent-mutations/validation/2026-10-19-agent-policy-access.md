# Agent policy native and HTTP access

Status: focused checkpoint passed; the end-to-end bundle remains open. The design
was recorded in [P07 Agent policy access](../plan/P07-agent-policy-access.md) before
implementation. The signed predecessor is `bd89b5254` (native planning and source refresh).

The Agent catalog now opens a separate data-policy dialog. Nullable overrides cover
run/step snapshots, publication currentness, changed-basis response, replan count,
work duration and basis age. Blank fields inherit; zero replans is explicit. Existing
admissions keep their captured policy, and each actual runtime still checks owner
capabilities. This does not activate Process, Workflow or arbitrary tool policies.

The existing Agent policy owner remains the sole writer. The original workspace,
profile generation, Agent and whole-catalog revision accompany each frozen submission.
An acknowledged policy save also advances the ordinary Agent version. Other open
editors keep their original versions. Rejection preserves the draft; reading current
values and explicitly adopting them or rebasing the draft are separate actions.
Later typing survives a delayed save, closing/reopening and catalog refresh. Profile
retirement blocks further actions without routing old outcomes into a new editor.

GET/PUT `/api/agents/{agentId}/decision-data-policy` use existing read/write Agent
permissions. Both Int64 revisions are canonical decimal strings. Requests require
the exact route identity, nonempty operation ID, unique known JSON fields and an
8-KiB body limit. The existing strict override codec is applied only to the wire
property, preserving unrelated domain serialization. OpenAPI describes that encoding.
Responses distinguish accepted, rejected and unconfirmed outcomes; private exception
messages and configuration are not disclosed. The shared duplicate-member validator
also serves planning actions, so their HTTP regression cases were included.

## Executed proof

All builds use standard Release and the existing isolated PostgreSQL fixture. The
complete repaired candidate built UI, module, Web, sandbox and four test projects.
Build-backed discovery and execution passed 12 state cases, 40 native component and
catalog cases, nine HTTP cases and two browser journeys: **63/63**.

The native browser uses the actual registered catalog/file/database owners, races
another actor, checks the stored policy, preserves the unsaved draft across closing
and reopening, and requires explicit comparison/rebase before another save. The
backend-free sandbox covers inheritance, invalid input, comparison, unknown outcome,
keyboard closure and focus return. Both use 1920x1080 with visible actions and no
horizontal document overflow. HTTP proof includes stale ordinary-editor refusal,
reserved-policy preservation by a later ordinary edit, permission separation,
profile/scope mismatch, malformed/streamed oversized input, exact Int64 transfer,
strict OpenAPI schemas and sanitized failure outcomes.

Visual review then found a literal `AgentName` subtitle in the native wrapper. A
one-line Razor binding correction and name assertions passed all four policy-dialog
component cases and both browser cases: **6/6**. The exact delta from the 63-case
candidate contains that wrapper, two test files and the reviewed rendering-register
hash. The remaining 57 cases were not rerun after this display-only correction.

Final source inventory: 9,379 inputs,
`8ECE9EC46A79FB7FDFB3B7C08C8D6889A6D52DE25ACB3336D8F2F15754D19BB0`.
Final runtime inventory: 6,180 files,
`B49A6643D3B30F85C489158AFBBF873FF574A0E00E2AD7BFC11A5A8D39066477`.
Both inventories were verified unchanged after execution before releasing the freeze.
Final portability enforcement passes **15,611 reviewed findings unchanged**. Its only
baseline delta moved the intentional ordinal-ignore-case duplicate-member check to
the shared validator; the old entry was removed and the new entry reviewed. The final
rendering register passes **1,209 inputs**; documentation and diff checks pass.

Final native images are under
`output/playwright/crm-hr-live-agent-tools/20261019T133408877Z-108e4f16a4394be6942024d36a3f89d7`.
The conflict image was inspected: the selected Agent name is correct, the four-minute
draft remains visible, original/current values show three/two minutes, and explicit
reconciliation actions remain accessible. Sandbox unknown-state images were also
inspected in the preceding candidate.

| Candidate | Result | Retained proof SHA-256 |
| --- | --- | --- |
| `c4-agent-policy-access` | 21 passed, two failed in 23 finalized TRX rows; component lane interrupted | `00CC809AD390AC41BEAFC550126E11FE94E1869CDB52568B49B36137929B8D59` |
| `c4-agent-policy-access-repair` | 63/63 passed; visual subtitle finding retained | `2D34199892FFE7DE8581CDD9ED25AE77F2201A70F09387C73B2518DE86D1BA50` |
| `c4-agent-policy-access-visual` | 6/6 passed; exact delta bridge and visual review | `22114373F7F11D44CB0A3BB1030DA0897E0DA5B9314568F6E697951D5712EAA8` |

JSON records are in `../proof/2026-10-19-planning-authority/`. The initial failures
were an incorrect assertion that surrounding duration whitespace must be rejected,
and omission of the existing database-startup helper in the native browser journey.
The parent prematurely interrupted the first component lane at 58 seconds; flushed
output showed 13 existing catalog cases passing, not an established deadlock. All
40 cases passed on rerun. No interrupted component result is counted as a completed
lane. The first bridge-report assertion also omitted the rendering-register hash
change; the retained corrected report explicitly includes all four actual deltas.

## Architecture gate and remaining limits

Status: Pass with follow-up. Renderer/state live in AgentFramework.UI; the native
module owns workspace effects and opening lifetime; Web owns HTTP encoding. No project
reference, DI service, production partial or AgentDetailsDialog responsibility was
added. CodeAnalytics snapshots `snap-20261019124718-70e88475` and
`snap-20261019130909-70e88475` retain the same two existing cycles and 75 registrations.
Four new member-count observations describe cohesive draft/state/DTO/API types.
Existing package-advisory and dependency/DI diagnostics limit static analysis; direct
build, pure state, real host and browser proof supply the separate evidence.

The whole-catalog CAS remains deliberately coarse and may conflict on another Agent's
edit. No lock spans editing, network or model work. This checkpoint is not a capacity
benchmark. The owner has no durable policy-operation receipt: unknown submissions
remain blocked, and a current read cannot prove acceptance. Draft/unknown retention
lasts for the catalog host lifetime; navigation/restart durability remains open.
Process/Workflow policy propagation, actual claim/child fences, external-Agent target
versions, remaining writers and broad Stable/performance gates remain required.

Independent observation covers each complete test command. The final successor has
zero remaining observed processes and zero new database-name delta; seven unattributed
TEMP deltas are preserved. These checks do not prove complete allocation coverage.
The first attempt omitted the existing catalog-component TEMP family; its successor
manifest adds it explicitly. The interrupted fixture was attributed by its exact
database-derived profile ID and retained activation record, but automatic approval
review rejected cleanup before execution with **blocked by policy**:

- Database: `cditall_primary_e76a5b7c828f4ec7b51c7d88b604524e`.
- TEMP: `C:/Users/lucys/AppData/Local/Temp/candoitall-component-tests/e701d324735d408aa351ef96a6011b02`.
- Profile: `0a7200a7-d75f-4d95-e4fb-611c843977a8`; dataset namespace:
  `b2da6e8f-72e4-4bed-aa47-7bd1aabaa150`.

Neither deletion ran, and no alternate deletion was attempted. This and earlier
cleanup blockers remain explicit. SPA remains on `codex/concurrent-mutations`, with
no merge into main, push or maintainer contact.
