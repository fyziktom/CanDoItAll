# Normalized Requirements

## RQ01 - Persisted Step Operation Contracts

Process step operation contracts must be first-class persisted/editable/importable/exportable fields. Text parsing may remain as fallback only.

## RQ02 - Operation-Aware Tool Policy

Tool policy must enforce allowed operations, not just `ProcessAllowsProductMutation`.

## RQ03 - Trusted Grounding Ledger

External target aliases must be grounded by typed trusted sources with intended-use metadata.

## RQ04 - Storage-Backed Artifact Validation

Artifact validation must read through storage abstractions, not only workspace filesystem paths.

## RQ05 - Stable Artifact Projection Identity

Artifact dedupe and audit must use typed lineage identity hashes, not bounded display keys.

## RQ06 - Explicit Workflow/Subprocess Output Mapping

Workflow and subprocess artifacts must map explicitly to process artifact expectations.

## RQ07 - Recovery Continuation for Workflow/Subprocess/Manager

Manager recovery and workflow recovery must validate recovered artifacts using correct lineage and may not silently block when recoverable evidence exists.

## RQ08 - Runtime Invariant Audit

After execution/finalization, runtime must audit actual tool receipts, artifact lineage, and changed paths against the step operation contract.

## RQ09 - Typed Block/Escalation Lifecycle

Blocked and failed process states must carry typed reason codes and recovery options, not just free-text reasons.

## RQ10 - Generic Red-Team Coverage

Add red-team scenarios for non-software and software processes proving generic behavior.
