# C# Architecture Gate Result

## SB03 local browser identity gate

Status: Pass. The existing Web infrastructure identity owner retains circuit principal
resolution; LocalOperatorUiOptions owns only exact deployment ingress configuration.
No new interface, project, package, partial class, domain dependency or service locator.
Construction remains the existing scoped DI registration, with validated options.

Snapshots snap-20260827172158-9a0e08df and snap-20260827173911-9a0e08df cover only
Web.Infrastructure: types 13 to 14, members 70 to 76, zero diagnostics and scoped cycles.
Seventeen findings remain; the existing identity owner has an informational member-count
finding. Splitting this cohesive owner would not improve the boundary. The filtered
dependency query returned no matching edges; direct project-reference inspection and
unchanged-project assertions are the dependency evidence, not a full-graph claim.

38 component tests cover positive/negative principal, options and registration behavior;
9 HTTP tests protect API/dev/file boundaries; 3 real browser cases prove actual production
consumers and post-reload runtime access. All pass. The four new regression cases failed
before the repair. Evidence: bundle://proof/SB03/manifest.md and codeanalytics-summary.json.

Anonymous trust is constrained to both captured transport addresses; authenticated scopes
and HttpContext.User are not augmented. Docker trust is explicit, validated and enabled
only after verifying local port bindings. No silent authentication fallback was introduced.

## SB01/SB02 checkpoint review

Status: Pass for the final deployed repair and its real dependent execution.

## Findings

| Severity | Finding | Evidence | Decision |
| --- | --- | --- | --- |
| None blocking | Existing typed owners suffice; no new project/interface/DI registration | source-audit.txt; unchanged project references | Retain boundaries |
| Informational | ProviderManagement scoped loader partially interprets factory registrations | codeanalytics.json; eight Info diagnostics | No whole-solution claim |
| Informational | Runtime scope reports 20 file-size warnings and 60 type-size informational findings | codeanalytics-runtime.json | Not a clean-codebase claim; no broad cleanup |
| Informational | Compatibility owner has ten source members | runtime snapshot, direct source review | Cohesive existing wire normalization owner; no extraction justified |
| Resolved | Image input names, response contract and reasoning admission were incomplete | failing-first and passing boundary tests; build6-runtime-ui.trx | Real dependent behavior now passes |
| Resolved | Injected approval context split assistant/tool-result sequence | approval-context-red.trx; final 100 tests; real completed image approval | Preserve context and approvals; normalize only stamped context |

## Dependency Direction And Ownership

ProviderManagement owns discovery/persistence; Models owns value normalization; Blazor
orchestrates explicit events and renders. Image-name mapping stays in the existing image
tool input boundary. The existing driver rechecks allowed model routes.

SharedProviders.Http keeps its closed request/response schemas. Documented image metadata
and reasoning values are validated without exposing arbitrary upstream fields/errors,
weakening byte/base64 limits, changing credentials or bypassing admission.

The existing OpenAI compatibility client resolves the real model from unique source
metadata for policy only; the SDK wire model remains its constrained route. Missing or
duplicate metadata fails before dispatch. NormalizeToolHistory repositions only
AIContextProvider-stamped messages before pending assistant calls. It preserves actual
messages, normal user/history order, parallel call IDs, missing-result errors, caller
immutability, approval gating and context/compaction.

No new interface, factory, service registration, partial file or generic helper.
No provider HTTP discovery was moved into rendering components. This is a bounded repair,
not project extraction or a hidden fallback.

## Scoped CodeAnalytics

- ProviderManagement: snap-20260827142907-cf2335c0, 245 dependency edges, zero scoped cycles,
  no blocking errors; eight informational DI interpretation diagnostics.
- Final Maf runtime: snap-20260827162452-e586cbf7, 196 types, 1,583 members, 190 dependency
  edges, zero scoped cycles, zero diagnostics. Twenty large-file warnings and sixty
  type-size Info findings remain; the compatibility owner's ten-member Info finding was
  reviewed directly. Zero DI registrations in this scope is not composition-root coverage.
- Artifacts: proof/SB01/codeanalytics.json and codeanalytics-runtime.json.
- These snapshots do not imply whole-solution analysis or zero existing architectural debt.

## Testability And Real Consumer Proof

- Catalog/pricing: 134 unit, nine component, 46 integration cases.
- Image input: six meaningful initial failures, then 20 passing cases.
- Image envelope: five failures, then 18 positive/adversarial cases.
- Shared reasoning: three shared failures with local passing controls.
- Relay admission: eleven failures with controls; 44 schema cases and 23 integration cases pass.
- Approval/context: four failing context cases with four passing no-context controls,
  streaming/buffered and with/without compaction. Final compatibility/context/approval
  scope passes all 100 cases, including 18 real-SDK wire cases.
- Negative checks preserve unknown/ambiguous-name rejection, closed payload schemas,
  missing/duplicate metadata failure, no fabricated tool results and unchanged caller input.
- Current build6 real UI proves both providers, nondefault models, approved image completion,
  vision and eight complete source-side invocations. Unit fixtures do not substitute for it.

## Closure Decision

Pass. Both subbundles' dependent behavior is now verified on the rebuilt pair.
Source audit/hashes and final canonical check are recorded by reviews/02-final-verifier.md.
Any later catalog, route, request schema or approval-context change invalidates the related
focused scope and real UI proof. Primary-agent review; no independent reviewer is claimed.
