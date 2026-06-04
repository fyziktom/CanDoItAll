# Tool Provider Metadata And Policy

## Problem

`IAgentRuntimeToolProvider` currently returns raw `AITool` lists. This is sufficient for the first decoupling but weak for future driver packs and manager-verification safety.

## Proposed Additions

Add provider-neutral metadata such as:

```csharp
public sealed record AgentRuntimeToolProviderDescriptor(
    string Key,
    string DisplayName,
    IReadOnlySet<string> DomainTags,
    IReadOnlySet<AgentRuntimeToolProviderPurpose> SupportedPurposes,
    AgentRuntimeToolProviderRiskLevel RiskLevel);
```

And optional tool metadata such as:

```csharp
public sealed record AgentRuntimeToolDescriptor(
    string ToolName,
    string ProviderKey,
    AgentRuntimeToolOperationKind OperationKind,
    bool RequiresApprovalByDefault,
    IReadOnlySet<AgentRuntimeToolProviderPurpose> SupportedPurposes,
    string EvidenceKind);
```

Do not force all external providers to implement the full metadata immediately. Use adapters/defaults during migration but require first-party providers to become explicit.
