# Test Inventory

## Unit Tests To Add

| Target | Test examples |
| --- | --- |
| Template schema loader | Valid pack loads; duplicate keys fail; invalid names fail; unknown implementation keys fail; raw secret fields fail. |
| Capability name policy | Kebab-case capability keys accepted; snake_case runtime tools accepted; invalid spaces/special characters rejected. |
| Tool invokers | Internal mock tool call succeeds; external process fake call maps JSON args/result; timeout returns explicit failure; stderr is captured with masked secrets. |
| MCP runtime | Fake local MCP starts, lists tools, respects allowedTools, stops cleanly, and reports startup failures. |
| Skill loader | File skill validates `SKILL.md`; inline skill loads resources; registered skill missing type fails predictably. |
| MAF adapters | Adapter maps descriptors to runtime objects without calling old hardcoded switches. |
| Structured diagnostics | Template validation, external tool failure, MCP startup/list-tools failure, cancellation, and cleanup errors include category, key/kind, correlation ID, masked detail, and repair hint. |
| Hardening gates | File-size guard, no direct MAF references from abstractions/implementations, no silent fallback, and focused performance scan findings are enforced or recorded. |

## Integration Tests To Add

| Target | Test examples |
| --- | --- |
| Seed materialization | `Templates/Capabilities` materializes the same default catalog keys as current seed code. |
| Capability filtering | Agents using existing `skills.json` assignments receive identical capability kinds and runtime names. |
| Runtime composition | MAF runtime composes workspace, .NET, provider-native, skill, and MCP capabilities through the new services. |
| Capability proof/setup | Generic verify delegates to setup services; MCP list-tools result persists actionable details. |
| Persistence migration | Existing seeded data is normalized without duplicate capability identities or managed seed churn. |
| Checkpoint composition | SB05 isolated services compose without MAF; SB07 seed dry-run proves no fallback; SB09 MAF adapters prove reduced coupling and no leaked MCP processes. |

## Component And E2E Tests To Add

| Target | Test examples |
| --- | --- |
| Setup wizard | User can add Tool, Skill, and MCP; validation messages are specific; raw JSON remains advanced path only. |
| MCP setup test | User can test start/list-tools and see allowed tool names before saving. |
| External tool setup | User can define a fake process/http tool, run test arguments, and see result/error evidence. |
| Process/workflow smoke | Existing software delivery/process templates still execute required tool policy paths. |
| Regression UI | Capability list filters still count Tool, Skill, and MCP correctly after template-backed seed. |

## Required Negative Tests

| Target | Failure examples |
| --- | --- |
| External process tool | executable missing, command rejected, timeout, non-zero exit, huge stderr/stdout, invalid JSON, schema mismatch, cancellation cleanup. |
| External HTTP tool | missing secret binding, non-success status, timeout, invalid JSON, schema mismatch, masked auth header. |
| Local stdio MCP | startup timeout, process exits before handshake, `tools/list` failure, empty discovered tools, allowedTools mismatch, cleanup failure. |
| Remote HTTP MCP | auth binding missing, non-success status, handshake failure, list-tools protocol error. |
| Template/seed | duplicate key, missing stable ID, invalid managed seed version, missing agent assignment, template path/field reporting. |
