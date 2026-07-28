# Test Plan

## Test Principles

- Capture fixtures under 1.13 before updating.
- Prefer deterministic fake providers/agents for message and state semantics.
- Add a small number of real provider/A2A validations after deterministic tests.
- Every removed workaround needs a failing-first test.
- Verify returned response and persisted history separately.
- Verify security failures fail closed and do not invoke a tool.
- Hash and retain sanitized fixtures.

## Baseline Matrix

Record under 1.13:

| Area | Fixture |
|---|---|
| Package graph | direct and transitive MAF/MEAI/OpenAI/A2A packages |
| Empty chat session | framework-managed, no messages |
| Framework history | multi-turn text |
| Provider history | conversation/response ID |
| Function approval | one pending mutation |
| MCP approval | one pending MCP call |
| Mixed calls | one approval-required plus one ordinary call |
| Multiple approvals | provider fake emits two requests even if normal options discourage it |
| Attachment | request-scoped data content, scrubbed persisted state |
| Governed step | isolated session |
| Background response | continuation token state |
| Handoff | intermediate messages plus explicit terminal output |
| Handoff order | tool call/result across participants |
| Reasoning | id-less reasoning followed by ID-bearing text |
| Workflow checkpoint | external request payload if active |
| A2A | agent card and one message |
| File tools | tool inventory and representative read/write policy |

## Approval Security Tests

### Native 1.15

- correct request ID and unchanged tool call executes once;
- correct ID with substituted tool name is rebound to original;
- correct ID with modified arguments is rebound to original;
- unknown ID is ignored/rejected and executes nothing;
- approval from another session executes nothing;
- approval from another run executes nothing;
- duplicate response executes once;
- replay after restart executes nothing;
- denial executes nothing;
- mixed approve/deny decisions apply by ID;
- omitted approval remains pending or follows explicit product policy;
- process-local cache cleared before continuation still succeeds from persistence;
- malformed persistent record fails closed;
- missing request ID fails closed;
- MCP call preserves server name and arguments;
- session scrubber retains binding state.

### Legacy 1.13

- direct response-only continuation is detected as incompatible;
- preferred reissue produces a new native 1.15 request;
- old decision cannot approve the new request accidentally;
- optional bridge accepts only trusted fingerprinted record;
- modified bridge record fails;
- expired bridge fails;
- concurrent bridge attempts execute once;
- bridge metrics and audit event emitted;
- disabling binding is not used.

### Mixed behavior modes

Mode P (parity): bypass disabled.

- ordinary and approval-required calls are surfaced as in 1.13 baseline;
- pending count and UI contract match baseline.

Mode N (new 1.15 behavior, optional after gate):

- only true approval-required calls surface;
- ordinary call is stored and resumed automatically;
- application mutation policy still blocks unapproved side effects;
- restart preserves auto-resume state;
- no duplicate ordinary call.

## Handoff and Merge Tests

For each direct and full-runtime path:

- explicit terminal output selected;
- intermediate response remains activity-only;
- no duplicate final text;
- tool call immediately precedes corresponding result;
- multiple calls/results retain intended order;
- id-less reasoning remains before answer text;
- author names preserved;
- response and message IDs stable enough for history/UI;
- usage is not double-counted;
- raw workflow output metadata remains available until projection completes;
- stored history equals authoritative response contract;
- max depth works with repeated and fragmented call IDs;
- return-to-previous works;
- cancellation disposes all participant builds;
- exception details remain redacted.

## Session Tests

- deserialize every 1.13 fixture under 1.15;
- serialize/deserialize native 1.15;
- strict JSON options with omitted null properties;
- provider-managed conversation preserved;
- framework-managed local history preserved;
- no double replay into provider-managed conversation;
- governed step receives new isolated session;
- approval continuation restores exact session;
- request-scoped attachment bytes absent after persistence;
- arbitrary state-bag values retained;
- serialization timeout classified;
- malformed JSON classified;
- incompatible session type classified;
- cancellation propagates;
- 1.15 → 1.13 rollback fixture result documented.

## Workflow Checkpoint Tests

Only if native MAF checkpoint/external request state is active:

- create checkpoint under 1.13;
- change assembly/package version;
- restore under 1.15;
- payload type resolved from live request port;
- human/external request resumes;
- unrelated/incompatible type rejected;
- no broad reflection type loading;
- checkpoint integrity remains enforced.

## File/Capability Tests

- before/after tool inventory exact or intentionally documented;
- no unexpected MAF file tools;
- no duplicate tool names;
- path traversal blocked;
- junction/symlink escape blocked;
- external alias authorization;
- read-only external target;
- process allowed-operation scope;
- script side-effect policy;
- write/delete approval;
- unsupported provider removes/blocks mutation tools as designed;
- suppress-approval path remains explicitly authorized;
- concurrent runs do not share workspace state;
- fallback DI services use correct workspace root/scope.

## A2A Tests

- host starts with 1.15 preview packages;
- agent card/discovery succeeds;
- non-streaming message succeeds;
- streaming message preserves order and completion;
- session continuity;
- approval exposure/continuation if endpoint supports it;
- cancellation;
- invalid input;
- exception redaction;
- authorization;
- no session leakage between clients.

## Package and Warning Tests

- one stable MAF version;
- one matching preview release train;
- no transitive 1.13 MAF assembly;
- no NuGet downgrade;
- no accidental Harness/AG-UI/declarative package;
- targeted build without blanket MAF warning suppression;
- every remaining experimental API suppression is documented locally.

## Runtime Isolation Tests

Run concurrent executions with distinct:

- workspace scopes;
- provider profiles;
- sessions;
- tool sets;
- MCP endpoints;
- approval records;
- transient context;
- attachments.

Assert zero cross-run state, disposal exactly once, and immutable preparation snapshots only.

## Real Validation

After deterministic closure:

1. one ordinary `gpt-5.4-mini` agent tool run;
2. one approval-required mutation with user approval;
3. one handoff workflow with explicit terminal output;
4. one A2A hosted invocation;
5. one restart between approval request and response;
6. one application restart followed by session continuation.

Record token/usage only in redacted form and never commit credentials.
