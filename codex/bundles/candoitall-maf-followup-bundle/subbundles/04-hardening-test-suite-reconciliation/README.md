# Subbundle 04 — Hardening test suite reconciliation

## Problem

The verification document claims that a focused hardening test filter passed 42/42, including test classes that are not present in the uploaded ZIP.

Document claims:

```text
AgentFinalizerPolicyTests
AgentToolInvocationPolicyTests
ProviderFeatureMatrixTests
AgentRuntimeHardeningStaticRegressionTests
```

Uploaded repository evidence: no files matching those class names were present in the ZIP. The only obviously related unit test file found was `AgentOutputContractTests.cs`.

## Required change

Either add the missing tests or correct the documentation. Prefer adding the tests, because the runtime now contains enough critical behavior to justify them.

## Minimum test files to add

### `tests/CanDoItAll.Tests.Unit/AgentFinalizerPolicyTests.cs`

Cover:

- known structured output contracts resolve to finalizer policies;
- unknown structured output returns no finalizer;
- required mode read from metadata;
- shadow mode read from metadata;
- disabled mode read from metadata;
- process-step default fallback behavior is documented;
- exact-one finalizer validator accepts one valid invocation;
- validator rejects missing, duplicate, malformed, and invalid invocation payloads.

### `tests/CanDoItAll.Tests.Unit/AgentToolInvocationPolicyTests.cs`

Cover:

- unknown tool denied;
- read tool allowed;
- mutation tool requires approval when not auto-approved;
- mutation tool allowed when auto-approved;
- mutation tool with wrapper/provider approval has effective approval path;
- repeated signature limit;
- sensitive argument redaction.

### `tests/CanDoItAll.Tests.Unit/ProviderFeatureMatrixTests.cs`

Cover provider matrix cases from Subbundle 03.

### `tests/CanDoItAll.Tests.Unit/AgentRuntimeHardeningStaticRegressionTests.cs`

Use repository-text/static tests sparingly to catch regressions such as:

- no `MetadataJson: "{}"` for governed process execution with `ProcessStepOutcomeStructuredOutputContract`;
- no `structuredOutput: null` in approval continuation paths;
- no broad `IsPolicyException => InvalidOperationException or NotSupportedException` pattern;
- verification docs do not reference missing test class names.

## Acceptance criteria

The focused hardening test filter must discover and run the expected tests. A filter that reports success because no tests were discovered is not acceptable.

## Status

Completed. Proof is recorded in `../../reviews/01-execution-report.md`.

## Requirements Owned

R06, R07, R12.

## Prerequisites

Subbundles 01, 02, and 03 must be completed or their code surfaces must be testable in the same pass.

## Dependency Impact

Critical foundation for all later verification and documentation claims.

## Validation Depth

Add or update focused unit tests, then prove the hardening filter discovers and runs the intended test classes.

## Progression Gate

Downstream work may continue only after the named hardening test classes exist in the repository and the focused unit-test filter reports nonzero discovered tests.
