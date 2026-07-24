# SB09 API-Seeded Scenarios, Documentation, And Closure

## Status

- `Completed`
- SB08/CP-08, the API-only scenario, repeated-run identity proof, populated browser inspection, final port `5032` host, and final closure checks pass.

## Objective

- Populate the running CRM-HR module with an idempotent, realistic hiring/workforce demonstration through the public CRM-HR API, update affected module/API/skill/bundle documentation, validate the scenarios in the browser, and complete the final architecture/performance/test/host closure.

## Covered Inputs

- Follow-up request items 4, 6, and 7.
- New `R019` and `R020`.

## Prerequisites

- SB08 and `CP-08` pass.
- SB07 may execute independently, but final browser proof and closure require both SB07 and SB09.

## Exact Source References

- `repo://src/App/CanDoItAll.Web/Api/ApiEndpointRouteBuilderExtensions.cs`
- `repo://src/Modules/CanDoItAll.Modules.CrmHr/README.md`
- `repo://codex/skills/candoitall-api-agents/SKILL.md`
- `repo://codex/bundles/crm-hr-feedback10-improvement/reviews/01-execution-report.md`
- `repo://codex/bundles/crm-hr-feedback10-improvement/reviews/csharp-architecture-gate.md`

## Scenario Contract

- Multiple party types and delivery units.
- Active employees with manager/home-unit relationships and skill matrices.
- A contractor with different rate/capacity semantics.
- Planned/incoming hire, interviewing candidate, offer-stage candidate, rejected or withdrawn case, and a completed hire with onboarding tasks.
- Scheduled and completed interviews with meaningful outcomes.
- Capacity blocks such as leave/training and varied availability.
- No fabricated invoice, purchase, or bought-financial facts.

## Deliverables

- Persistent database records created only through `/api/crm-hr`.
- An idempotent operator flow based on deterministic external codes/search-before-create, not startup code or direct SQL.
- Updated CRM-HR module README, Web API documentation surface, CRM-HR API skill reference, bundle requirements/traceability/architecture/report, and design-proposal index.
- Browser screenshots proving populated Directory, Workforce, Recruiting, and their dialogs at `1800x1100`.
- Final builds, affected tests, architecture review, performance scan, bundle validator, and verified port `5032` restart.

## Dependency Impact

- The scenario operator is an external HTTP client and adds no production project reference or startup hook.
- Documentation follows the shipped Web-to-CRM-HR service boundary and must not describe direct persistence.
- Closure changes no dependency direction established by SB07 or SB08.

## Validation Depth

- Proof tier: `Behavioral`.
- Architecture checkpoint: `CP-09` and final closure gate.

## Implementation Steps

1. Start the rebuilt host with the new API.
2. Use the CRM-HR API skill and HTTP endpoints to query/create the deterministic scenario; record returned ids and results without committing secrets.
3. Repeat the seed operation to prove idempotency/no duplicate business identities.
4. Validate the resulting data through bounded API queries and the actual Directory, Workforce, and Recruiting UI.
5. Update all affected documentation and durable bundle status.
6. Run the two-pass in-scope performance scan, architecture review gate, affected/full validation, completed bundle validator, and final `5032` restart/readiness check.

## Do Not Do

- Do not add product startup seeding, direct SQL/EF scripts, fixture-only UI branches, fake financial numbers, secrets, or destructive cleanup endpoints.
- Do not claim idempotency from row counts alone; prove stable business identities on a repeated run.

## Acceptance Checklist

- [x] The running module visibly contains varied realistic hiring/workforce scenarios.
- [x] Every persistent scenario record was created or updated through normal HTTP commands.
- [x] Repeating the operator flow does not create duplicate external-code identities.
- [x] API, module, skill, architecture, and bundle documentation agree with the final observed runtime behavior.
- [x] Directory/Workforce catalogues and dialogs remain usable with populated data.
- [x] The Release solution build passes and architecture source boundaries have been reviewed.
- [x] Runtime performance/test/browser/host gates pass and are durably recorded.
- [x] The final Release host returns HTTP 200 on port `5032`.

## Proof Required

- Semantic positive: visible multi-stage hiring and workforce records match API responses and persisted UI behavior.
- Adversarial negative: repeated seeding preserves identity; invalid references fail without partial records; unavailable financial domains remain unavailable.
- Shallow-pass trap: direct database inserts, hard-coded startup fixtures, one homogeneous employee list, or screenshots using test-only data.
- Anti-stub audit: no seed hook, fake branch, TODO, or undocumented endpoint.

## Progression Gate

- `CP-09` and the final bundle gate passed. API-only seed and identity reconciliation, bounded readback, populated UI, clean final console, affected regressions, architecture/performance review, and the restarted Release host agree.

## Completion Record

- Trusted prerequisite: SB08/CP-08 passed with a real-host positive HTTP scenario, meaningful invalid-reference/query negatives, validated/synchronized skill files, and affected/full builds.
- API-only semantic proof: `bundle://proof/SB09/seed-first-run.md` records the deterministic external-code contract and the linked scenario created/reconciled solely through the public CRM-HR API.
- Repeat adversarial proof: `bundle://proof/SB09/seed-repeat-run.md` records zero creates, writes, replacements, or conversions and reuse of `29` parties, `20` relationship sets, `20` profiles, `12` skills, `40` party skills, `5` capacity blocks, `8` applications, `9` interviews, `4` lifecycle tasks, `3` support assignments, and `1` conversion.
- Readback proof: `bundle://proof/SB09/api-readback.md` records `78` parties (`29` demo), `32` workforce records, `8` applications across seven stages, `12` skills, `9` interviews, `4` lifecycle tasks, and `3` supported candidates.
- Browser proof: `bundle://proof/SB09/browser-review.md` and `bundle://proof/SB07/browser-normal-and-dialog-review.md` record inspected populated Directory, Workforce, Recruiting, record-dialog, paging, scroll-owner, conversion, and clean-console states at `1800x1100`. A populated Recruiting selection race was found, fixed, and covered by a dedicated `1/1` regression.
- Host proof: `bundle://proof/SB09/host-5032.md` records HTTP `200` for root/access, populated totals `78/32/8`, empty stderr, and no server error pattern from the final Release listener on port `5032`.
- Final validation: `bundle://proof/final-validation.md` records the `0`-error Release build, `37/37` feature UI, `2/2` focused API, `35/35` broader CRM-HR integration, skill synchronization, architecture/performance review, and diff hygiene. The existing `NU1903` advisory and unrelated all-unit baseline debt remain explicit residual risks.
- Closure decision: `Completed`; `CP-09` and the final bundle gate passed.

## Reopen Triggers

- Duplicate identities, direct persistence bypass, missing scenario stage, stale docs, UI clipping/scroll failure, architecture/performance blocker, test/build failure, or unhealthy port `5032`.
