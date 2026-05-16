# Source Artifacts

## Live Repositories

| Artifact | Path | Role |
|---|---|---|
| Main CanDoItAll repository | `C:\repositories\CanDoItAll` | Host application, Workbench, Processes, workflows, MAF runtime, storage, search, module registration. |
| RAG repository | `C:\repositories\CanDoItAll.AgentFramework.Rag` | Provider-neutral RAG abstraction and Qdrant driver to wrap as a projection store. |
| SemanticCompletion repository | `C:\repositories\CanDoItAll.AgentFramework.SemanticCompletion` | Local embeddings, semantic ranking, and classification primitives to wrap as semantic utilities. |
| Neuro architecture patch bundle | `C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-neuro-architecture-patch` | Architect-supplied neuro-cognitive control, belief, replay, procedural skill, and answer-gating patch. |

## Source Inspection Commands

- `rg --files C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2`
- `rg --files -g *.sln -g *.slnx -g *.csproj C:\repositories\CanDoItAll`
- CodeAnalytics snapshot `snap-20260515230800-1b0ae250` for the main CanDoItAll scoped architecture inspection.
- CodeAnalytics snapshots for RAG driver, Qdrant driver, and SemanticCompletion driver.
- Direct file reads for MAF context provider, Workbench project structure, RAG driver contracts, SemanticCompletion contracts, workflow contracts, and storage/search integration.

## Bundle Inputs

- User request from `inputs/00-original-request.md`.
- Existing architecture sketch files under this bundle.
- Existing C# contract sketches under `contracts/csharp`.
- Current source observations captured in `analysis/01-current-state.md`.
- Neuro patch source captured in `inputs/04-neuro-architecture-patch-reference.md`.
- Score geometry review request captured in `inputs/06-score-geometry-review-request.md`.

## Artifact Limitations

- No implementation was performed.
- No product build or test run was performed.
- The source tree has unrelated pending git changes outside this bundle; this repair ignores them.
