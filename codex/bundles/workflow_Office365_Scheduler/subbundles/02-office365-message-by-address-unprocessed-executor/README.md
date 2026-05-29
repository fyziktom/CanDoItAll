# 02-office365-message-by-address-unprocessed-executor

## Status

- Status: `Completed`

## Objective

Add an Office365 workflow executor that downloads at most one newest unprocessed email matching a configured email address and harden mark-processed add-only category mutation.

## Covered Inputs

- R1: download at most one unprocessed email matching a configured address.
- R2: exclude messages already carrying the processed category.
- R3: return no-op success on no matching email by default.
- R4: add processed category without requiring a source category.

## Prerequisites

- SB01 baseline and failing-first evidence are captured.
- Existing Office365 category executor behavior is understood and not regressed.

## Exact Source References

- `repo://src/plugins/CanDoItAll.Plugin.Office365/Office365WorkflowExecutor.cs`
- `repo://src/plugins/CanDoItAll.Plugin.Office365/Office365GraphClient.cs`
- `repo://src/plugins/CanDoItAll.Plugin.Office365/Office365BundledPlugin.cs`
- `repo://src/plugins/CanDoItAll.Plugin.Office365/Office365PluginConstants.cs`
- `repo://src/plugins/CanDoItAll.Plugin.Office365/Office365PluginServiceCollectionExtensions.cs`
- `repo://src/plugins/CanDoItAll.Plugin.Office365/Office365PluginModels.cs`
- `repo://tests/CanDoItAll.Tests.Unit/WorkflowExecutorTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/WorkflowPreviewSimulationTests.cs`

## Scope

- Add a strongly typed settings model and match/no-message enums for address-based polling.
- Add Graph client support for server-side filtering with a bounded client-side fallback when Graph rejects complex filters.
- Add executor output containing `count`, `noMessages`, `route`, `messages`, `office365Processing`, `projectId`, `nodeId`, and `runContext`.
- Extend mark-processed so empty source category means add-only processed category while preserving unrelated categories.
- Register descriptor, constants, preview simulation, and DI.

## Dependency Impact

- SB03 templates depend on the executor output contract and category mutation ordering.
- SB06 idempotency depends on `office365Processing.selectedMessageId` and stable idempotency key context.
- SB07 observability depends on the executor route and no-message result shape.

## Validation Depth

- Critical semantic proof with failing-first, adversarial negative, and positive fake Graph tests.
- Fake Graph URL/filter assertion for processed-category exclusion.
- Tests for matching message, already processed message ignored, no-message success, add-only category mutation, descriptor registration, and preview simulation.
- Source assertions proving bounded fallback and no live Graph calls in tests.

## Implementation Steps

1. Add settings, enums, constants, and descriptor metadata.
2. Implement Graph client address polling with bounded fallback.
3. Implement executor input resolution and no-message success output.
4. Harden mark-processed add-only behavior.
5. Add preview simulation and tests.
6. Record proof manifest and semantic invariants.

## Do Not Do

- Do not make live Graph calls in tests.
- Do not fetch unbounded mailbox pages.
- Do not treat no-message as an exception by default.
- Do not require a source category for processed marking.

## Acceptance Checklist

- New executor id is registered in the Office365 plugin descriptor.
- Address matching validates input and supports configured JSON-path fallback.
- Already processed messages are excluded.
- No-message output is success/no-action by default.
- Mark processed can add category without source category.

## Proof Required

- `bundle://proof/SB02/manifest.md`
- `bundle://proof/SB02/semantic-invariants.md`
- Failing-first transcript for missing address executor/no-message semantics.
- Passing unit tests for fake Graph and add-only category mutation.
- Source assertion and anti-stub audit transcripts.

## Browser Validation Logging

- N/A; this is a backend/plugin executor subbundle. If descriptor visibility changes the Workflows UI, defer route proof to SB03/SB08.

## Progression Gate

- Continue to SB03 only after tests prove the executor can produce one-message and no-message results, preserve project/node context, and expose the idempotency/category data later templates require.

## Closure Notes

- Office365 address polling executor, settings, descriptor, simulation, DI registration, Graph client path, and add-only mark-processed behavior are implemented.
- Closure proof: `bundle://proof/SB02/manifest.md`; `bundle://proof/SB02/semantic-invariants.md`.
- Passing transcript: `bundle://proof/SB02/transcripts/integration-office365-address-after-implementation.txt` proves one-message matching, processed-category exclusion, bounded fallback, invalid-address rejection, no-message success, add-only category mutation, descriptor registration, and preview simulation.
- Build transcript: `bundle://proof/SB02/transcripts/build-after-sb02.txt` passed with existing EF Core assembly-version warnings only.

## Suggested Agent Prompt

Implement the Office365 address/unprocessed executor and add-only mark-processed hardening with fake Graph tests, then close SB02 with artifact-backed semantic proof before touching templates.
