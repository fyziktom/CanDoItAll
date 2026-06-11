# SB04: Multi-team software-delivery automation

## Status
- Status: Completed

## Objective
Prove the `software-delivery` representative multi-team process through automation dispatch and release-governance flow.

## Covered Inputs
- Raw request: especially representative templates such as multi-team development.
- REQ-004: prove multi-team/software-delivery automation including role assignments, peer review, QA, release approval, and project-structure output.

## Prerequisites
- SB03 closure gate proves production-path automation dispatch without suppressed dispatch.
- Template catalog and `software-delivery` definition are available in the checkout.

## Exact Source References
- repo://Templates/Processes/processes/software-delivery/definition.json
- repo://src/CanDoItAll.Modules.Processes/Templates/ProcessTemplateCatalogInventory.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateGovernanceTests.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateExecutionE2ETests.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateAutomationTestSupport.cs

## Deliverables
- Add/strengthen automation assertions for role coverage: product owner, delivery manager, architect, lead engineer, QA, security reviewer, release manager.
- Verify peer review, QA validation, security review, release approval, rollout, and post-release learning steps.
- Add project-structure output/readback assertion for the software-delivery run.
- Decide whether a stable alias key `multi-team-development` should exist. If not, document and test that `software-delivery` is the canonical multi-team representative in UI/catalog readback.

## Dependency Impact
- SB05 may proceed only after this subbundle makes the multi-team representative unambiguous.
- SB08 release decision depends on this subbundle proving governance and release steps, not only happy-path completion.

## Validation Depth
- Run focused automation and catalog/governance tests.
- Verify multi-role coverage, governance steps, project-structure output, canonical alias/readback behavior, and Process Core leakage scans.
- Include semantic adequacy proof, manifest, negative/positive transcripts, source assertions, and anti-stub audit under `proof/SB04/`.

## Implementation Steps
- Audit the `software-delivery` template roles and steps against the required multi-team governance path.
- Strengthen E2E assertions for role assignments, peer review, QA, security review, release approval, rollout, learning, and project-structure readback.
- Add catalog/governance proof for canonical multi-team wording or alias behavior.
- Run source scans proving Process Core stays domain-generic.

## Do Not Do
- Do not duplicate the template definition unless alias support requires a lightweight pointer.
- Do not introduce a fallback template selector.
- Do not make Process Core aware of multi-team or software-delivery concepts.

## Acceptance Checklist
- Multi-team automation E2E passes with completed first-pass path.
- Required multi-role assignments are present.
- Release approval artifact and rollout evidence are present.
- Repair path remains skipped only when the QA branch chooses quality accepted.

## Proof Required
- Focused integration transcript.
- Catalog/governance test transcript.
- Source scan for Process Core domain leakage.

## Proof Captured
- Manifest: `bundle://proof/SB04/manifest.md`
- Semantic invariants: `bundle://proof/SB04/semantic-invariants.md`
- Focused integration transcript: `bundle://proof/SB04/transcripts/focused-integration.txt`
- Source assertions: `bundle://proof/SB04/transcripts/source-assertions.txt`
- Process Core leakage scan: `bundle://proof/SB04/transcripts/process-core-leakage-scan.txt`
- Code-first guard: `bundle://proof/SB04/transcripts/code-first-guard.txt`
- Anti-stub audit: `bundle://proof/SB04/transcripts/anti-stub-audit.txt`
- Failing-first baseline: `bundle://proof/SB04/transcripts/failing-first-source-assertion.txt`

## Browser Validation Logging
- N/A unless UI/catalog wording changes; if it does, cite the SB02 route or add a targeted Playwright screenshot.

## Progression Gate
- SB05 may proceed only after the multi-team representative path is unambiguous.
- Reopen SB04 if later UI or release proof cannot distinguish `software-delivery` from the requested multi-team representative.

## Suggested Agent Prompt
- Implement SB04 by proving `software-delivery` as the canonical multi-team representative through production dispatch, governance steps, project-structure readback, and Process Core leakage scans. Store proof under `proof/SB04/`.
