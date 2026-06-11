# SB04: Multi-team software-delivery automation

## Status
Prepared.

## Objective
Prove the `software-delivery` representative multi-team process through automation dispatch and release-governance flow.

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

## Browser Validation Logging
N/A unless UI route changed.

## Progression Gate
SB05 may proceed only after multi-team representative path is unambiguous.
