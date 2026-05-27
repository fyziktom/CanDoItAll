# SB03: Message Injection And Finalizer Structured Output

## Status

- Completed

## Objective

Use MAF 1.6 message injection or an explicit compatibility seam where it improves finalizer and guardrail reliability.

## Covered Inputs

- RQ03: adopt useful message injection/structured output behavior.

## Prerequisites

- SB02 adoption matrix must decide the message-injection strategy.

## Exact Source References

- repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs
- repo://src/CanDoItAll.AgentFramework.Core/Finalizers/AgentFinalizerPolicy.cs
- repo://tests/CanDoItAll.Tests.Unit/AgentFinalizerPolicyTests.cs

## Deliverables

- Production adapter path or documented compatibility seam for finalizer/guardrail instruction injection.
- Tests proving finalizer instructions survive tool loop and streaming/non-streaming execution where locally testable.

## Dependency Impact

- SB09 adapter boundary and SB10 process-finalizer validation depend on this behavior.

## Validation Depth

- Critical semantic proof must show structured output finalizer cannot be skipped or duplicated.
- Include adversarial negative proof for missing/duplicated finalizer instruction handling.

## Implementation Steps

- Inspect MAF 1.6 injection APIs available in local packages.
- Implement the smallest adapter seam that preserves finalizer instructions through the function loop.
- Add or update tests for finalizer survival and duplication prevention.
- Update `proof/SB03`.

## Do Not Do

- Do not concatenate hidden prompt fragments in multiple places.
- Do not silently fall back when provider support is missing; record the compatibility path explicitly.

## Acceptance Checklist

- Message injection/adaptation decision is implemented or explicitly deferred with guard.
- Finalizer tests cover positive and adversarial cases.
- Proof files cite production source and test transcripts.

## Proof Required

- Failing-first or adversarial transcript.
- Passing test transcript.
- Source assertions, anti-stub audit, and changed-file hashes under `bundle://proof/SB03/transcripts`.

## Browser Validation Logging

- N/A - no browser-visible behavior in this subbundle.

## Progression Gate

- SB09 may depend on SB03 only after finalizer injection/compatibility proof is artifact-backed.

## Suggested Agent Prompt

Evaluate local MAF 1.6 message injection support and harden finalizer instruction delivery with focused tests and adapter-level proof.

## Closure Proof

- bundle://proof/SB03/manifest.md
- bundle://proof/SB03/semantic-invariants.md

