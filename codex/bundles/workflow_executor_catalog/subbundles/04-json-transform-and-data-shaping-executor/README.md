# 04-json-transform-and-data-shaping-executor

## Status

- Status: `Completed`

## Closure Notes

- Implemented deterministic `json.transform` without arbitrary code execution.
- Added typed operation/settings/result models and catalog schema metadata.
- Covered path extraction, object/array shaping, invalid paths, and template scenario usage in tests.
- Proof manifest: `bundle://proof/SB04/manifest.md`
- Semantic invariants: `bundle://proof/SB04/semantic-invariants.md`

## Objective

Implement the planned `json.transform` executor for deterministic data shaping without arbitrary code execution.

## Covered Inputs

- RN02: Users need obvious deterministic helper executors.
- R5: Implement deterministic JSON transformation/data-shaping executor.
- R10: Templates should demonstrate JSON transform use.
- R11: Scenario harness must cover JSON transform.

## Prerequisites

- SB01 closure gate passed.
- SB03 closure gate passed if transform examples read or write workspace files.
- Descriptor catalog still lists `json.transform` as planned before implementation.

## Exact Source References

- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/BuiltInWorkflowExecutorDescriptors.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/WorkflowExecutorJson.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowExecutorContracts.cs`
- `repo://src/CanDoItAll.AgentFramework.Models/Workflows/WorkflowExecutorModels.cs`
- `repo://tests/CanDoItAll.Tests.Unit/WorkflowExecutorTests.cs`
- `repo://Templates/Workflows/manifest.yaml`

## Scope

- Add typed settings for safe transform operations.
- Implement select, set, remove, merge, array map/filter/sort/distinct/take, count aggregation, template object, and schema validation where feasible.
- Use a bounded JSON path or pointer syntax with explicit errors.
- Mark unsupported transform operations as validation failures, not runtime surprises.
- Update descriptor status from planned to implemented only for operations actually supported.

## Dependency Impact

- SB05 report rendering and SB09 templates depend on deterministic JSON output shapes.
- SB10 scenario harness depends on realistic transform proof.

## Validation Depth

- Unit tests for invalid paths, missing fields, array behavior, type handling, schema validation, and bounded output.
- Negative tests for arbitrary code attempts and unsupported operations.
- Passing executor catalog tests proving `json.transform` is runnable only when implementation exists.

## Implementation Steps

1. Define settings and operation models with strongly typed operation names.
2. Implement operations in a focused executor class using `System.Text.Json`.
3. Add descriptor schema metadata.
4. Add unit tests for positive transforms and adversarial invalid inputs.
5. Add a small template or defer template wiring to SB09 with documented dependency.

## Do Not Do

- Do not execute C#, JavaScript, shell, or user-provided scripts.
- Do not silently ignore invalid paths.
- Do not implement broad query language features that are not tested.
- Do not claim schema validation beyond the implemented checks.

## Acceptance Checklist

- Common JSON transforms no longer require LLM calls.
- Invalid transforms fail with actionable messages.
- Output is deterministic, bounded, and strongly shaped.
- Executor descriptor and catalog availability match the real implementation.

## Proof Required

- Targeted passing test transcript for `json.transform`.
- Negative transcript or test evidence for invalid paths and code-execution attempts.
- Changed-file hashes, source assertions, and anti-stub audit.
- Execution report row for SB04 closure.

## Browser Validation Logging

- N/A unless this phase changes browser-visible settings or catalog UI; otherwise component proof is sufficient.

## Progression Gate

- Continue to SB05 only after deterministic JSON transform outputs can feed Markdown/report rendering without LLM dependence.

## Suggested Agent Prompt

Use SB04 to implement a small, typed, deterministic JSON transform executor. Avoid arbitrary code and prove realistic array/object shaping plus negative invalid-input behavior.
