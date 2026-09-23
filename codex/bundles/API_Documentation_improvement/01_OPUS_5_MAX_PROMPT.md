# Opus 5 Max — finish API documentation as a usable integration contract

## Mission and authority

Work in the user's current CanDoItAll and CanDoItAll.SharedInfo checkouts. Your principal deliverable is a **complete, accurate, self-contained English HTTP API description** in C# XML documentation and in the generated OpenAPI document shown by Swagger UI. A separate-UI developer must understand an operation and every related DTO without reverse-engineering the repository.

The user explicitly authorizes the required work in both repositories in this run. Complete the work autonomously in meaningful checkpoints; do not stop after adding a few summaries or after refreshing only the task endpoint. Read this pack as a reviewed starting point and verify local source before applying it. The review is not proof of your final implementation.

This is not another UI-decoupling or broad tool-runtime repair assignment. Preserve those completed changes. Keep the existing transport behavior, naming, permissions and owner boundaries. Fix documentation and the narrow infrastructure needed to export it. Correct demonstrably wrong OpenAPI metadata without silently changing runtime behavior. A newly found unrelated behavior defect is a separately described blocker or follow-up, not permission for a rewrite.

## Entry and repository safety

Record branch, full HEAD, worktree state and live sibling source revisions for both repositories. The reviewer saw product development at `d0f3c41a458fd8543f447a4525e6123e6820de59`, SharedInfo main at `409d7c291e9378b2a17fdfce91ed64e9ef851f0e`, and an older product main. Respect the actual user worktree. Do not reset, rebase, merge, push, delete working bundles, change a deployment or overwrite local edits. Do not reinstall skills into the user's profile without separate authorization.

Use current repository instructions and maintained test guidance. Old bundle execution instructions are not standing authority. No classic bundle directory/workflow is required. Do not recreate temporary bundles in development/main. Use a small local progress ledger and later consolidate only durable rules.

Signed local commits are authorized. Preserve the current Git identity, signing key and `commit.gpgsign` policy. Reuse one persistent PowerShell session and the same Git/GPG environment for subsequent commits. When a real signing process is waiting for user pinentry, keep that process and session alive and poll it; do not repeatedly cancel it, start competing signers or modify the index underneath it. Never restart/reload gpg-agent as cleanup, disable signing, cache a passphrase in code or secretly extend cache expiry. A persistent shell is not a guarantee that gpg-agent never expires. Verify every created commit's signature and report hashes by repository. No push or merge.

All new comments, descriptions, docs, fixtures, examples, commit messages and your final report are English. Existing intentional user content or multilingual input tests must not be indiscriminately translated.

## Source and terminology policy

Read `02_SOURCE_REVIEW.md`, the glossary JSON/Markdown and `03_DOCUMENTATION_STANDARD.md` before annotating models. Use its qualified preferred terms consistently. Treat definitions as source-grounded at the reviewed version and preferred prose as an adoption proposal. Recheck changed contracts; add evidence-backed terms before using a new meaning. Do not invent semantics from property names. In particular:

- Party, CRM account, account profile, workforce profile, person identity and login account are different.
- Project ID, project lifetime, write admission, lease token and authorization grant are different.
- A node key/task ID is not universally a GUID.
- Current values are edit preconditions, not desired values; revisions belong to specific owners.
- Task resources, direct task assignees, storage objects and Project Structure assets are not interchangeable.
- Definition, revision, conversation, turn, message entry, agent execution run, workflow run and process run are different identities.
- HTTP operations are not runtime tools; API authority is not an agent capability grant. Do not document HTTP as a bypass when a tool is unavailable or denied.
- Required and nullable are independent; absent, null, empty and zero are not synonyms.
- None, NotCommitted, Committed and Unknown must follow their actual owner contract; HTTP status alone does not prove effect state.

Use source files, validators, mappers, query limits, authorization policies, serializers, existing boundary tests and returned owner data as evidence. SharedInfo's older snapshot is a historical comparison baseline, not the truth for today's build. Resolve contradictory prose through current source and tests and record the correction.

## Scope — full supported HTTP surface and its DTO closure

Discover the actual runtime EndpointDataSource/ApiDescription surface and generated OpenAPI documents. Do not restrict discovery to `src/App/CanDoItAll.Web/Api`, public C# declarations or types with a `Dto` suffix. Project Structure handlers live outside that folder; internal records and nested/transitive schemas are exposed too.

Inventory every method/route, operation identifier, tag, handler, source project, request reader/serializer, auth policy, request/response/header types, error envelopes and all reachable properties. Include manual schemas, generic wrappers, nested arrays/maps, polymorphic alternatives, enum tokens, nullable members and any declared event payloads. Cross-check endpoints that are intentionally excluded from API discovery, so omissions are classified rather than forgotten.

Cover Agents/providers/history/approvals, Agent Recruiting, Projects, Project Structure/tasks/assets/leases, CRM/HR, Prompt Gallery, Workflows and external responses, Processes/run records, Simple Chat definitions/conversations/operations, Memory Providers, Plugins, Shared Providers, storage recovery and existing authorized/managed/storage file routes. Classify `/_dev` and other diagnostics clearly. Document them in the correct development audience if currently exposed; never broaden their access or imply production availability. Blazor page routes and static files are not API operations.

Use `inventory/historical-api-family-seed.json` only as a discovery cross-check. Its 321 operations and 518 schemas describe a September 15 snapshot, not your target count. The final denominator comes from your current build.

## Canonical documentation architecture

C# XML comments are the canonical member/operation semantics. SharedInfo owns the adopted cross-repository writing standard and the shared API/domain vocabulary. The product owns actual contracts, endpoint behavior and generated OpenAPI. Keep one canonical glossary, with links or deliberate generated adoption rather than two independently edited copies.

Use summaries for DTO types and serializable members/constructor parameters, and summaries, remarks, parameter/return/response docs for actual handlers and meaningful API conversion boundaries. Include internal serializable types. A method that only registers routes is not documentation for all of its handlers.

The product uses **Microsoft.AspNetCore.OpenApi**, not SwaggerGen. `Swashbuckle.AspNetCore.SwaggerUI` is only the viewer. First prove the native XML path on the pinned SDK/package versions; do not solve this by adding a second generator, a new viewer, random Swagger settings or a major dependency upgrade.

Enable XML output for the API contract/handler project closure as appropriate, verify evaluated MSBuild settings and generated XML availability, and check project-reference/package/sibling documentation inputs. Preserve the source-generator-compatible AddOpenApi registration and all current schema/operation transformers. Avoid broad global removal of CS1591 suppression that turns unrelated public code into this task's scope; use a focused coverage gate for the actual API surface.

For inline Minimal API lambdas, prefer documented named handler methods with unchanged binding, DI, route names, metadata and behavior. Compiler XML docs above a lambda/Map call do not describe the operation. For custom/manual schemas and cases the generator cannot resolve, use the smallest owner-aware transformer consuming the canonical description source. Do not maintain a second independent bag of hundreds of prose strings. Test records' generated properties, cross-assembly types and inheritdoc rather than assuming a build success proves export.

For responses, XML response tags annotate declared responses; declare the correct schema/status/media type through existing typed results or metadata where inference is insufficient. Do not claim unverified statuses, wrap raw results in new envelopes, replace existing errors with ProblemDetails, or change null/default/enum behavior to make documentation easier.

## First vertical slice before bulk changes

Prove the complete path with:

1. Task-update handler and the new cross-assembly input with nested schedule/estimate/execution/basis types.
2. One CRM list endpoint using `[AsParameters]` and its query defaults plus page response.
3. A DTO with positional properties and a nested nullable property from another assembly.
4. One existing custom schema/operation transformer and one enum/string-value wrapper.

Build from clean outputs, inspect emitted XML, export both OpenAPI routes and assert actual descriptions in the raw JSON. Open Swagger UI and expand model properties. Test reference occurrences, not only a schema's title. Once proved, apply the pattern across the inventoried surface with bounded reviews/tests.

## Task-update documentation repair — mandatory

Follow `05_TASK_UPDATE_CONTRACT.md`. The repair already exists in the inspected source; do not reintroduce the old component value types on the wire.

- `PUT /api/project-structure/projects/{projectId}/tasks/{taskId}` uses `ProjectStructureTaskUpdateAgentInput`; `TaskId` and nested affected-task IDs are strings, not `{ "value": ... }` wrappers.
- The route/body task IDs must match exactly.
- HTTP must submit the owner-returned `expectedProjectAdmission`; omission or a different project produces the existing lifetime-refresh rejection. The agent tool binds its own admitted authority, so HTTP and agent permissions are not the same despite a shared input type.
- Current title/progress/estimate/execution/cost basis/direct-assignment revision must come from the owner read. Document exactly how an HTTP client obtains them, including the task's metadata where appropriate. Do not invent a GET task-detail route if it is not mapped.
- Current progress accepts -1 or 0..100; proposed progress accepts 0..100. `currentCostBasis` is a required JSON member that may be null. Other requiredness must be verified against the relevant HTTP and agent serializers separately.
- `expectedEffortHours` is stored in hours even when the preferred unit is ManDays. Do not instruct a client to send a raw man-day value in this property.
- Describe schedule gesture, affected-task intervals, direct-assignee change semantics, execution-state transitions, cost basis and revision preconditions from their actual owners.
- Publish at least one runnable raw-JSON read/modify/write example generated from seeded returned state. Include unchanged/null cases, a schedule change and key rejection examples. Verify payloads through the HTTP boundary, not just a C# constructor.
- Update SharedInfo's snapshot, provenance, migration note, Project Structure skill and every related stale reference/example. Keep route and operation names stable.

## Writing acceptance criteria

For each operation a reader can answer: what it does; when to use it; prerequisites and source of referenced IDs; authority; required parameters; successful response semantics; asynchronous/partial effects; actual errors; and safe next steps. State whether a command creates, updates, replaces, acknowledges, starts or merely observes.

For each serialized field describe its domain meaning, identity source/scope, value unit or representation, omission/null/default meaning, constraints and interactions when relevant. State sensitivity and whether it is request-only, response-only, owner-issued or client-selected. Do not manufacture an ISO/culture/date/uniqueness guarantee from a suggestive name. Required fields must not be called optional because C# permits null.

Document each enum's actual wire representation and member meanings. Do not globally change serialization to strings, change casing, use enum ordinals as new stable names or infer a shared status lifecycle from different enum types. Preserve foreign protocol vocabulary, notably the shared relay subset.

Descriptions must be self-contained. Links to the glossary can add context but cannot replace explanations. Ban filler such as 'Gets or sets X', 'The data', 'The model' and tautological 'The identifier' without identifying the entity and how to obtain it. Do not meet a gate by duplicating the type summary into every property.

Examples must use synthetic data and real supported routes, exact casing and media types. Do not paste secrets, real local paths, tokens or captured personal data. Do not hard-code a fake server-issued admission and present it as runnable.

## SharedInfo adoption

Read its AGENTS.md and use `06_SHAREDINFO_SYNC.md`. Add/adopt the durable documentation standard and glossary in maintained locations, link them from the owner-contract orientation, and synchronize the actual API skills and support package. Do not copy product code or this working pack into SharedInfo.

Generate the snapshot from the final identified product build. Prefer a clean signed source commit plus exact sibling revisions and a clean publish. Capture `/openapi/v1.json` and `/swagger/v1/swagger.json` on the same isolated host and compare them. A port number is not source identity. Do not stop the user's protected host or turn on its diagnostics to capture a spec. Respect or deliberately revise the validator's existing canonical-server rule; never fake provenance by relabeling a different host.

Update manifest hash/version/counts/source/cleanliness/dependency pins, the support README, API skill route and contract examples, operation sets and partner migration guidance as actually affected. Review all maintained API skills for the same terminology and stale task shapes. Run Test-CanDoItAllWebOpenApi.ps1 and Test-SharedInfo.ps1 plus any current skill-parity validators. Do not hand-edit generated schema descriptions as the solution; fix their source and regenerate.

## Validation and cost control

Start with narrow pipeline and task regressions; use `--list-tests`, expected discovery and current binaries. Do not run the whole several-hour suite at entry or per annotation batch. Build affected projects and classify invalidation of tests as you go.

Your final `API-DOCUMENTATION-CLOSURE` checkpoint must include:

- Complete endpoint/type/property coverage inventory with a reviewed disposition for every supported item and every exclusion; no 'public types only' loophole.
- XML-to-generated-document assertions for the representative hard cases and all inventoried owned members.
- Documentation-only versus structural OpenAPI diff. Preserve route/method/operation IDs, names, types, requiredness, enum tokens, constraints, media types, security and success/error shapes. Any legitimate metadata correction has an explicit runtime-backed rationale and test. Do not blanket approve structural drift.
- Raw-JSON task update tests: successful unchanged/read-modify-write path; valid reschedule; mismatched task IDs; missing task; omitted versus explicit-null currentCostBasis; missing/mismatched admission; stale preconditions; relevant invalid values. Do not weaken owner safety to make examples pass.
- Swagger Playwright proof: expanded operation, parameter, request schema, nested properties and response/error schema show useful descriptions; correct referenced and custom-schema descriptions; one safe representative Try-it-out/read-modify-write on synthetic data. Save focused screenshots and inspect them. Swagger visual success does not replace raw JSON assertions.
- At least one independent non-domain-object client exercise of the documented workflow; a small JavaScript/fetch client or generated-client compilation is useful. Do not require the separate-UI colleague to use internal C# domain types.
- Clean-build/publish reproduction of document generation and both routes, plus normal documentation/static gates. If project graph, Directory.Build.*, cross-cutting registration or shared test infrastructure changed, name the resulting broader Stable trigger and run it once on the stable final checkpoint under repository rules.
- Both SharedInfo validators and affected API skills/examples, linked to exactly the exported product source.

The Python helpers in this pack are optional reviewer tools, not a complete OpenAPI validator or proof of semantic correctness. Extend or replace them with maintained product/SharedInfo tests. Their coverage warnings must not be suppressed with generic prose.

## Exit report

Return an English report containing: start/final HEADs and signed commits for both repositories; adopted glossary/standard locations; generator/build decisions; counts of current documented operations/types/properties and exclusions; before/after description coverage with real denominators; task migration proof; exact test discovery/results/source revisions; Swagger screenshots; structural changes with reasons; final snapshot hash/provenance; and any genuine remaining blockers.

Keep separate: source comments completed, XML emitted, OpenAPI descriptions present, descriptions semantically reviewed, Swagger rendered, raw-JSON examples working, SharedInfo synchronized. Do not claim completion from one of these alone. Do not claim all tests passed when a known independent gate remains failing. Finish this documentation mission rather than proposing the next UI refactor.
