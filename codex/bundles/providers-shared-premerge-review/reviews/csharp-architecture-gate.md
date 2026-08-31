# C# Architecture Gate Result

Status: Pass with follow-up for implemented SB01–SB07; final merge closure remains blocked by SB08/SB09 proof and authority.

## Findings

| Severity | Finding | Evidence | Required action |
| --- | --- | --- | --- |
| Resolved | Response status/stream terminal errors could look successful; diagnostic reads could erase upstream status | SharedProviderPremergeRelayTests and pinned SDK regressions; 179-case final integration run | Preserve these negative cases |
| Resolved | HTTP loopback policy differed between discovery and imported runtime | RuntimeProjection integration and URI policy unit tests | Retain shared URI policy |
| Resolved | Quoted credentials and sanitized timeout causes were mishandled | Decrypted persistence tests, real OpenAI driver HttpClient deadline, boundary unit negatives | Keep redaction before encryption and typed timeout flag |
| Resolved | Orphan input tombstones accumulated; removal alone could recapture expired input | Actual recorder retry/revision test and populated retention/transfer tests | Preserve frozen input expiry and bounded cleanup |
| Follow-up | Existing request-policy and composition files remain large | Scoped CodeAnalytics and original inventory | Do not expand unrelated responsibilities; no size-only extraction in this repair |
| Proof | Final canonical export, active packages, independent verifier and original three-app gate remain open | SB08/SB09 and historical handoff | No merge recommendation until resolved |

## Dependency direction

Current snapshot: snap-20260831000620-9c068da1, nine affected projects / 436 documents; no scoped dependency cycles. See sb09-codeanalytics.json. Three pre-existing factory DI resolution warnings remain at composition registration lines 99/101/103. The tool reports zero scoped EF entities and is not used as persistence completeness proof; actual migrations and PostgreSQL tests provide that evidence.

The project/solution/build-configuration diff is empty. HTTP protocol ownership stays in Integration. Web owns OpenAPI mapping and post-header HTTP abort. ProviderManagement owns current persisted catalog eligibility. Neutral ProviderHistory contracts carry the deadline; Persistence produces/enforces it. MAF translates safe failures into history outcomes. No contract project gained an infrastructure reference.

## Responsibility and construction

SharedProviderOpenApiSchemas owns only generated API schema semantics; no runtime validation was moved into Web. ProviderHistoryFailureOutcome owns classification only. Existing adapters/decorators, registration and final target validation remain in use. No new service locator, registration-time BuildServiceProvider, generic manager, SDK dependency, project or factory hierarchy was introduced.

The catalog no longer materializes full settings/models for cache hits. A miss stores under the stamp of the rows actually loaded, avoiding a stamp/read race. The public span usage API remains available; production owned-memory calls avoid its compatibility copy.

## Partial-class policy

No new production partial, nested service or XML documentation comment. Existing cohesive Blazor/generated partial files were not expanded. New nested factories/harnesses exist only in tests. Source anti-stub scan found no TODO, NotImplementedException, fixture-specific branch, premerge branch, IServiceProvider, BuildServiceProvider or partial class in changed production files.

## Testability proof

Pure policy/outcome/redaction tests remain direct. External behavior is separately proved through the pinned OpenAI SDK and actual imported runtime graph. Database tests use real isolated PostgreSQL, production recorder/capture/outbox/backfill and migration services. UI seed data is explicitly a visual fixture and is not claimed as producer proof. Nine owning project builds passed directly; final focused Integration 179/179, Unit 145/145 and additional hot-path Unit 110/110 passed. The frozen Stable invocation also passed all9,424 rows, with all55 deferred-theory expansion rows reconciled against existing source data.

## Closure decision

SB01–SB07 can close at their declared tiers. The independent preparation reviews remain preparation-only; this execution audit is by the implementing agent plus CodeAnalytics, not a second independent human/agent review. SB09 must preserve that distinction. No source/dependency finding requires a new architecture layer or a further product repair.
