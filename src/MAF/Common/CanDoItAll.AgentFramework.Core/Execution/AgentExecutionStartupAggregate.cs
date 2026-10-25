using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Runtime.Abstractions;

namespace CanDoItAll.AgentFramework.Core;

internal sealed record AgentExecutionStartupAggregate(
    AgentExecutionPreparationBlueprint Blueprint,
    SandboxWorkspaceCatalogSnapshot CatalogSnapshot,
    AgentDefinition Agent,
    ProviderProfile Provider,
    ChatSessionRecord? Session,
    ExecutionRunRecord Run,
    ChatMessageRecord? UserMessage,
    IReadOnlyList<AgentRuntimeInputAttachment> InputAttachments);
