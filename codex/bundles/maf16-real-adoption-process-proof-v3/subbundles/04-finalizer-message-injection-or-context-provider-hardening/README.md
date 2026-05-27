# SB04: 04-finalizer-message-injection-or-context-provider-hardening

## Goal

Make finalizer/structured-output instructions robust in the MAF 1.6 tool loop.

## Required work

- Adopt IChatMessageInjector if available and practical.
- If unavailable, harden MessageAIContextProvider/finalizer fallback and document why.
- Test that finalizer instructions survive multi-tool loops, approval pending/resume, malformed response repair, and streaming/non-streaming execution.
- Ensure no duplicate finalizer instruction causes confused output.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: MAF package-level / MAF adapter-level / process runtime-level / template/UI-level.
- Note whether this subbundle changes behavior or only improves proof/documentation.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB04` are updated and downstream subbundles can rely on it.

## Status

- Completed

## Objective

Keep finalizer and context-provider behavior explicit through the tool loop.

## Covered Inputs

- RQ04 finalizer and context-provider behavior.

## Prerequisites

- SB02 and SB03 define available MAF capabilities.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionPrompt.cs`

## Deliverables

- Finalizer behavior stays explicit; no broad adapter change was needed.

## Dependency Impact

- SB11 and SB13 rely on finalizer diagnostics remaining authoritative.

## Validation Depth

- Source inspection and downstream validator tests.

## Implementation Steps

- Inspect finalizer prompt and validation boundaries.
- Keep validation failures explicit.

## Do Not Do

- Do not hide finalizer failures behind silent fallback behavior.

## Acceptance Checklist

- Downstream artifact validation tests pass.

## Proof Required

- SB11 and SB13 proof manifests.

## Browser Validation Logging

- No browser route is affected.

## Progression Gate

- Finalizer validation remains the source of truth before read-model projection.

## Suggested Agent Prompt

Preserve explicit finalizer validation semantics while closing artifact correctness gaps.
