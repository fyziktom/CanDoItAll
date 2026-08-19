# Agent tool failure recovery boundary

## Decision

Expected tool-input and access failures must cross the runtime boundary as typed,
sanitized `IAgentToolFailure` values. Unexpected implementation, provider, and I/O
failures remain opaque to the model and retain their diagnostic exception chain for
protected logging.

The owning domain validates its own data. A runtime adapter translates only known,
typed domain failures into the generic agent contract. The MAF invocation boundary
serializes that contract and records the same safe failure in the durable tool trace.

```text
tool input
  -> domain validation
  -> typed domain failure
  -> runtime-adapter translation
  -> IAgentToolFailure { code, safe message, retryability }
  -> model correction and retry
```

This is an adapter pattern. It is not a reason to expose `InvalidOperationException`
or other arbitrary exception messages globally.

## Incident evidence

Execution run `e3a22e82-d3db-48af-abb7-22c35083d3f3` had the spreadsheet skill,
`workspace_write_spreadsheet`, project-structure tools, file read/write access, and
automatic approval. The tool call targeted `A1:B12` but supplied three values in one
row. The spreadsheet service correctly rejected the two-column overflow. Because the
failure was an ordinary `InvalidOperationException`, the runtime reduced it to
`Error: Function failed.` The agent could not correct the range, did not retry, and
never reached workbook validation or project-asset registration.

The successful SVG control run proves that project asset creation itself was not the
failed boundary.

The rebuilt UI reproduction exposed a second generic spreadsheet-contract defect in
execution run `4045c6da-53ef-495a-8003-8e6495694dec`. The agent created two workbooks,
then added calculation and summary worksheets by writing back to each same workbook
with `overwrite=false`. The document service treated each in-place edit as a copy to an
already-existing output and rejected it. In-place edits now bypass the distinct-output
conflict check; copying to a different existing destination still requires explicit
overwrite authority.

## Responsibilities

| Boundary | Responsibility |
| --- | --- |
| Document service | Validate workbook, worksheet, address, and rectangular range invariants without agent-runtime knowledge. |
| Workspace spreadsheet adapter | Convert known document validation failures to safe, retryable agent failures. |
| Workspace path and file-access boundaries | Classify predictable path-resolution failures and apply one read/write/external-target policy for filesystem and document adapters. |
| Workspace tool set | Materialize each workspace tool once from configured, plugin, and individual declarations; preserve deterministic descriptions, monotonic approvals, and effective access policy. |
| Capability composer | Attach the unified workspace tool set once and account for catalog declarations without order-dependent shadowing. |
| MAF invocation boundary | Expose only typed safe failures; persist safe error evidence; mask unexpected exceptions. |
| Project asset source resolver | Accept exact target-project paths from every canonical managed root and reject foreign scopes. |
| Project asset content sanitizer | Inline only bounded safe text; never infer that an unknown small payload is text. |
| Project-structure context | Require correction/retry, workbook validation, asset registration, and persisted readback before claiming completion. |
| Image and document adapters | Translate only typed path/input failures; preserve unexpected provider, decoder, and I/O exceptions for protected diagnostics. |
| Workspace command boundary | Return only reviewed typed plan/input guidance; let unexpected host/lease/provider failures cross MAF opaquely, and never echo physical skill-script paths. |
| Agent HTTP boundary | Project persistence records to explicit public response contracts; continuation state and provider/runtime internals remain server-side. |
| Interactive project mutation policy | Use operation-scoped automatic leases; expose explicit lease-token tools only to governed process runtimes. |
| Project analytics tool | Project durable analytics to a minimal agent-facing operational view without host, identity, raw summary, or native error details. |

No project-specific or business-domain rule belongs in these boundaries.

## Invariants

- A range write never silently expands, truncates, or reshapes caller data.
- Updating the explicitly named input workbook in place is not an output collision.
  Writing to a distinct existing output remains a conflict unless overwrite is explicit.
- A correctable range-capacity failure reports the target address, capacity, supplied
  count, and offending values row without exposing native filesystem paths.
- `CanRetryWithCorrectedInput=true` means the tool exists and the caller should correct
  its arguments; it must not be presented as unsupported capability.
- File-access denial uses the same typed contract across workspace adapters.
- Predictable invalid, missing, wrong-kind, foreign-scope, and traversal path failures
  are typed by the path domain and translated by the owning runtime adapter; arbitrary
  `InvalidOperationException` and I/O failures are never promoted to safe model text.
- Workspace capability order cannot change the selected tool implementation,
  description, access decision, or approval requirement. Explicit declarations
  cannot weaken the base mutation approval or bypass the effective access plan.
- Spreadsheet and image inspection plus spreadsheet cell/range reads require file-read
  authority, not artifact-transformation authority. Document conversion and provider-
  based image analysis remain transformations.
- A project-scoped writer result remains a valid asset source under `artifacts`,
  `output`, `integration-map`, or `data`; a path in another project scope is denied.
- Project-managed source paths containing `.` or `..` segments are rejected before
  resolution, and the resolved path is checked against the target scope again.
- Small OOXML, ZIP, and other non-text package bytes are omitted from model-facing
  asset results. Only the bounded textual allowlist may inline Base64 content.
- An agent does not claim a generated project asset is complete until it has validated
  the workbook and read the stored asset metadata and content back.
- Unexpected image-generation, image-analysis, document-write, decoder, and native I/O
  failures remain opaque to the model. Typed path and input failures retain actionable,
  sanitized correction guidance.
- Public agent APIs never serialize runtime session keys, serialized continuation state,
  provider request/response identifiers, raw structured output, host paths, or lease
  tokens. Resumption resolves continuation state server-side from an opaque run or
  operation identifier.
- Interactive project-structure tools cannot acquire, inspect, renew, or release an
  explicit lease token. Each mutation obtains and releases its automatic lease even
  when the tool call fails.
- Model-facing project analytics contain only reviewed operational fields. The durable
  record may retain protected diagnostics but is never returned directly by the tool.
- Command and skill-script structured results never concatenate an arbitrary exception
  message or physical script path. Expected command inputs remain actionable; unexpected
  failures retain their causal exception chain.

## Rejected alternatives

- Globally exposing `InvalidOperationException`: leaks sensitive implementation and
  path details and misclassifies defects as user input.
- Silently trimming the extra empty cell or expanding the range: hides malformed data
  and changes the requested workbook without evidence.
- Adding Gardener, garden-layout, or `.xlsx` branches to Project Structure: couples a
  generic file/asset workflow to one example.
- Letting first-wins tool-name de-duplication resolve configured/plugin/catalog
  collisions: makes capability order a security and behavior input.
- Introducing a new project or one-method interface: the seams are existing document,
  adapter, access-policy, and invocation boundaries.

## Acceptance

- A deterministic malformed spreadsheet call produces a sanitized retryable result.
- A corrected call writes a valid workbook that summary and range tools can read.
- Sequential writes can add multiple worksheets to the same workbook without weakening
  overwrite protection for a distinct destination.
- The workbook is registered as a project asset and its stored bytes and metadata are
  read back.
- Every canonical project-managed root supports the same writer-to-asset handoff, while
  foreign project roots fail closed.
- Small XLSX, DOCX, and ZIP payloads are omitted from model-facing content results.
- Custom read/write access attaches spreadsheet validation tools without granting
  unrelated document transforms.
- Configured, plugin, and individual workspace declarations produce the same ordered
  tool set regardless of catalog order; approval and denial decisions are monotonic.
- Global and scoped run, chat, approval, receipt, usage, command, and event responses
  serialize only their explicit public projections.
- Interactive project mutation releases its automatic lease; governed process runtimes
  retain an explicit acquire/get/renew/release lifecycle.
- Project analytics retain protected durable evidence while the agent receives only the
  minimal safe projection.
- Command input/path failures are actionable without physical paths, while injected
  process-host and skill-script failures are unmapped and opaque at MAF.
- Focused unit/integration coverage and a real UI run on the rebuilt application pass.
  The broad stable gate is additional only for CI, release/merge closure, a frozen
  checkpoint, or a named cross-cutting invalidation trigger.

The final UI acceptance used execution run
`0314b84d-83dc-4fef-a6b9-016f823ca8c5` for creation and same-path worksheet updates,
then run `6ace150a-623b-4018-9de1-2b9b2dc795bf` for summary/range validation, project
asset registration, and metadata/content readback. Both workbooks rendered three
worksheets through the bounded Project Structure spreadsheet preview.

Interactive `Completed/Succeeded` is provider-turn and transport completion, not proof
that every requested deliverable exists. Raw tool traces cannot safely infer request
intent: a valid correction may change a path or identifier, while two calls with the
same tool and target may still represent different work. A future generic `Verified`
contract must use explicit request-scoped deliverable identities and positive validation
receipts/readbacks (or a typed finalizer that cites them); a heuristic hard gate does not
belong in this failure-translation repair.

An interrupted run can leave an unregistered workspace artifact. Project-scoped root
enumeration remains intentionally denied because it could discover stale or foreign
files, so a later turn must reuse the exact relative path from the successful tool result
or receipt. The current prompt requires the agent to report that path when registration
cannot finish, but model compliance is not a durable continuity contract. A future
generic recovery design should persist a same-chat, request-scoped artifact handle and
rehydrate that handle without granting directory discovery.

Typed expected-failure coverage is complete for the spreadsheet read/write, shared
filesystem path/access, reviewed project-asset input, and image-path boundaries changed
here. Project Structure exposes only its reviewed asset-input allowlist and sanitized
lease conflict through `IAgentToolFailure`; its default exception remains intentionally
opaque. The audited image-generation, image-inspection, image-analysis, and document-
output adapter paths likewise keep arbitrary exception text out of model-visible
results.

An explicit lease created by an older interactive run cannot be safely reclaimed from
the persisted snapshot because that record has no execution/session owner. It must be
released with its exact token or expire naturally within the configured 120-minute
maximum. Future run-owned explicit leasing requires a typed execution/session owner in
the acquisition receipt rather than an AgentId/host heuristic.

Command planning now classifies its reviewed path and argument failures through a typed,
sanitized boundary. Unexpected process-host, lease, and local-MCP failures propagate
with their exception chain and remain opaque at MAF; skill-script execution no longer
echoes an absolute script path or arbitrary exception message. Best-effort terminal
lease cleanup must continue across individual failures, so its structured result uses a
fixed safe message. The original protected cleanup exception is not currently written
to an observability sink; retaining that diagnostic without exposing it is follow-up
work.

`ProjectStructureAgentException` is also only selectively model-safe. The reviewed
asset-input and lease-conflict cases use explicit safe factories, while many legacy
project-structure 4xx validation sites still use the default opaque constructor. Those
fail closed at MAF and can still deprive an agent of correction guidance. They require
per-operation review and typed migration; making every historical message globally safe
would recreate the disclosure defect this boundary is designed to prevent.

Some legacy storage adapters also throw ordinary exceptions for predictable input
errors. Those exceptions remain opaque at MAF until their owning domains define reviewed
typed failures; an opaque result is not evidence that the storage tool is unavailable.
