# Scope Inventory

## CanDoItAll Main Repo

| Path | Role In This Bundle |
|---|---|
| `C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-boundary-hardening` | Completed source/MAF boundary hardening proof to preserve. |
| `C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture` | Architecture bundle that must be synced after projection hardening. |
| `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Sources\MemorySourceSnapshotContracts.cs` | Source contracts already hardened; consume as architecture context only. |
| `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Context\AgentContextContributionContracts.cs` | MAF contributor boundary already hardened; consume as architecture context only. |

## RAG Repo

| Path | Current Role | Expected Work |
|---|---|---|
| `C:\repositories\CanDoItAll.AgentFramework.Rag\src\CanDoItAll.AgentFramework.Rag.Driver\Abstractions\IRagDriver.cs` | Driver operations. | Extend with generic filter/index/delete lifecycle operations or compatible request contracts. |
| `C:\repositories\CanDoItAll.AgentFramework.Rag\src\CanDoItAll.AgentFramework.Rag.Driver\Models\RagSearchRequest.cs` | Search request without filter. | Add typed filter support. |
| `C:\repositories\CanDoItAll.AgentFramework.Rag\src\CanDoItAll.AgentFramework.Rag.Driver\Models\RagKnowledgeEntry.cs` | Text/vector payload with untyped metadata. | Keep generic metadata, ensure filters address metadata fields safely. |
| `C:\repositories\CanDoItAll.AgentFramework.Rag\src\CanDoItAll.AgentFramework.Rag.Driver\Models\RagDeleteRequest.cs` | Delete by explicit ids only. | Add delete-by-filter/source-equivalent lifecycle cleanup. |
| `C:\repositories\CanDoItAll.AgentFramework.Rag\src\CanDoItAll.AgentFramework.Rag.Qdrant\QdrantRagDriver.cs` | Provider implementation. | Translate generic filters/index/delete operations to Qdrant or fail explicitly when unsupported. |

## SemanticCompletion Repo

| Path | Current Role | Expected Work |
|---|---|---|
| `C:\repositories\CanDoItAll.AgentFramework.SemanticCompletion\src\CanDoItAll.AgentFramework.SemanticCompletion.Driver\Embeddings\IAgentTextEmbeddingGenerator.cs` | Embedding generator contract. | Preserve contract if possible; ensure returned result carries profile metadata. |
| `C:\repositories\CanDoItAll.AgentFramework.SemanticCompletion\src\CanDoItAll.AgentFramework.SemanticCompletion.Driver\Embeddings\AgentTextEmbedding.cs` | Embedding result with source text and vector. | Add stable provider/model/profile/dimension/normalization metadata. |
| `C:\repositories\CanDoItAll.AgentFramework.SemanticCompletion\src\CanDoItAll.AgentFramework.SemanticCompletion.Driver\Embeddings\OnnxAgentTextEmbeddingOptions.cs` | ONNX model options. | Derive stable profile metadata without relying only on absolute local paths. |
| `C:\repositories\CanDoItAll.AgentFramework.SemanticCompletion\src\CanDoItAll.AgentFramework.SemanticCompletion.Driver\Semantics\ISemanticTextRanker.cs` | Semantic ranking utility. | Source context only; avoid coupling ranking to Cognitive Memory. |
