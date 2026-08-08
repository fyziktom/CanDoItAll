# Verified architectural baseline

The follow-up must preserve these completed decisions.

## Runtime boundary

Application/Core coordinates execution. MAF maps provider/framework requests and responses. Product
modules contribute policies and tools through typed contracts. No product module reference may be
added to `CanDoItAll.AgentFramework.Maf`.

## Context and authority

Live UI observation is not authority. One immutable turn captures the observation and canonical
authority. Approval continuation reuses the original turn/run authority. A new UI state affects only a
new user turn.

## Workspace scope

Every execution creates one effective `WorkspaceExecutionScope` and one owned
`WorkspaceRuntimeServices` bundle. All scope-bound tools, script inspection, recovery reads, receipts,
and process leases must agree on that effective scope.

## Runtime state

Native MAF state is opaque inside a versioned envelope. Restore is an explicit
restore/migrate/replay/fail decision. Approval continuation fails closed when native state cannot be
restored compatibly.

## Lightweight LLM

`ILlmInvocationPort` is stateless, provider-neutral, and independent from agents, tools, workspace,
authority, approvals, processes, and MAF sessions.

## Ordinary conversation

The application transcript is canonical. Provider-native acceleration is optional and disposable.
One turn is atomic: either the admitted user entry and provider-change decision complete with an
assistant entry, or all turn-owned changes are rolled back.
