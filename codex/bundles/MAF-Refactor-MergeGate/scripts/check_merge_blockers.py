from __future__ import annotations

import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[4]
failures: list[str] = []

def read(rel: str) -> str:
    path = ROOT / rel
    if not path.exists():
        failures.append(f"Missing file: {rel}")
        return ""
    return path.read_text(encoding="utf-8", errors="replace")

metadata = read(
    "src/MAF/Common/CanDoItAll.AgentFramework.Core/Context/AgentTurnContextMetadata.cs"
)
if not any(token in metadata for token in (
    "Malformed",
    "ProjectionReadStatus",
    "GovernanceProjectionRead",
    "AuthorityProjectionRead",
)):
    failures.append(
        "Authority projection reader has no explicit malformed/absent result."
    )

resolver = read(
    "src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentExecutionAuthorityComposition.cs"
)
if "CreateDefaultProviders" in resolver:
    failures.append("Authority resolver still hard-codes source providers.")
if "IEnumerable<IAgentExecutionSourceAuthorityProvider>" not in resolver:
    failures.append("Authority resolver is not visibly DI-enumerable.")

policy_pipeline = read(
    "src/MAF/Common/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicyPipeline.cs"
)
if "ReferenceEquals(composedContext, context)" in policy_pipeline:
    failures.append("Process contributor validation still relies on ReferenceEquals.")
if not ("EffectiveContext" in policy_pipeline and "Decision" in policy_pipeline):
    failures.append("Policy pipeline does not visibly return effective context plus decision.")

maf_factory = read(
    "src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeAgentFactory.cs"
)
if "TryCreateRecoverableDeniedResult" in maf_factory and "EffectiveContext" not in maf_factory:
    failures.append("MAF block guard does not visibly use the effective policy context.")

module = read(
    "src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs"
)
if "services.AddLlmConversations(" in module:
    failures.append("Ordinary LLM conversations remain production-registered.")

store = read(
    "src/MAF/Common/CanDoItAll.AgentFramework.Llm.Conversations/FileLlmConversationStore.cs"
)
if "private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _conversationGates" in store:
    failures.append("File conversation CAS still uses only an instance-local gate.")

if failures:
    print("Final merge-blocker guard FAILED:")
    for failure in failures:
        print(f"- {failure}")
    sys.exit(1)

print("Final merge-blocker guard passed.")
