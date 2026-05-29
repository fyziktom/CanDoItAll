# SB06 Proof Manifest

## Scope

Agent workflow UI seeding and compatibility migration.

## Changed File Hashes

- `fdfdb7e97371fcf3de8f22fcf607ce3fa114fd5d5b00f934fa72133e69bd39b7` `repo://tests/CanDoItAll.Tests.Components/WorkflowsPageTests.cs`

## Evidence

- Semantic invariant contract: `bundle://proof/SB06/semantic-invariants.md`
- Failing-first transcript: N/A - process hardening of seed ownership behavior with no UI file changes.
- Passing transcript: `bundle://proof/SB06/transcripts/proof-summary.txt`
- Anti-stub audit transcript: `bundle://proof/SB06/transcripts/proof-summary.txt`

## Cited Tests

- Test name: `CanDoItAll.Tests.Components.WorkflowsPageTests.Workflow_example_seed_creates_production_examples_when_enabled`
- Test name: `CanDoItAll.Tests.Components.WorkflowsPageTests.Workflow_example_seed_preserves_non_managed_definitions_with_template_names`

## Invariants

- Invariant ID: `SB06-INV-001`
