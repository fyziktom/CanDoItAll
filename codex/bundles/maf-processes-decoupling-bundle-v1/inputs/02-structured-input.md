# Structured Input

## Raw Intent

Decouple `CanDoItAll.AgentFramework.Maf` from `CanDoItAll.Modules.Processes` before starting broader process-core extraction or domain driver work.

## Desired End State

- MAF runtime composes tools through registered provider abstractions.
- Processes module owns process-specific tool construction.
- MAF project no longer references Processes project.
- All existing process tool names and behavior remain stable.
- Existing tests are repaired or expanded, not weakened.
- Architecture guardrails prevent the dependency from returning.

## Explicit Non-Goals

- No full process-core split in this bundle.
- No process dispatcher decomposition in this bundle beyond proof and inventories.
- No DotNet/SWDev helper driver implementation in this bundle.
- No business-analysis driver implementation in this bundle.
- No removal of process tools from agent runtime.

## High-Risk Literal Requirements

- "Must not get lost": add durable XLSX checklist and subbundle progression gates.
- "Must not simplify or omit": add tool parity inventory and tests that enumerate every process tool.
- "Touches many tests": include test-impact inventory and explicit test repair subbundle.
- "Small steps": split into subbundles with entry/closure gates and stop conditions.
