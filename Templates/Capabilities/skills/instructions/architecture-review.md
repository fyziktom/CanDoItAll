Use this skill when reviewing the architecture of the current repository.

1. Do not start with a broad workspace inventory or summarize script for architecture questions.
2. Start by reading `CanDoItAll.AgentFramework.sln`, `src/MAF/Common/CanDoItAll.AgentFramework.Core/AgentFrameworkWorkspaceService.cs`, `src/MAF/Common/CanDoItAll.AgentFramework.Core/AgentFrameworkWorkspaceService.Chat.cs`, `src/MAF/Common/CanDoItAll.AgentFramework.Persistence/FileSandboxWorkspaceStore.cs`, and one additional source file from `src/MAF/Common/CanDoItAll.AgentFramework.Persistence/SandboxWorkspaceSeedFactory.cs` or `src/MAF/Common/CanDoItAll.AgentFramework.Maf/MafAgentRuntime.Capabilities.cs`.
3. If one of those paths moved, use `workspace_search` only to locate the closest match, then `workspace_read_file` on the exact file.
4. At least three of the required reads must end in `.cs` or `.razor`; `.sln` and `.csproj` files alone are not enough.
5. After the required source reads, read one or more relevant `.csproj` files only if you need boundary or dependency context.
6. Ignore additional context sourced from `data/`, `artifacts/`, `output/`, `.playwright-cli/`, `.vs/`, `tools/`, or sandbox page components unless the user explicitly asks about those surfaces.
7. Do not recommend framework upgrades, more comments, more tests, or dependency-injection changes unless the files you read show a concrete problem that justifies that recommendation.
8. Do not call out `net10.0` as a problem by itself, and do not claim testing is missing unless you read the tests project or a concrete test file and still found a specific architectural gap.
9. Do not claim missing abstractions when the constructor already injects an interface such as `ISandboxWorkspaceStore`, and do not claim missing logging/error handling if the file already shows `try/catch` and `AppendExecutionLogAsync`.
10. In this repository, strong findings usually come from responsibility concentration rather than from missing language features. Good candidates are: `AgentFrameworkWorkspaceService.Chat.cs` concentrating session creation, runtime invocation, approval continuation, execution logging, and metric persistence; `FileSandboxWorkspaceStore.cs` rewriting the whole `SandboxWorkspaceDocument` on save and normalizing the whole document on load; `SandboxWorkspaceSeedFactory.cs` combining seeded catalog definition with legacy refresh and migration rules; and `MafAgentRuntime.Capabilities.cs` concentrating skills, tools, plugins, MCP, RAG, memory, and compaction assembly in one runtime file.
11. Prefer findings from those four seams. If a possible issue does not map back to one of those files and a concrete observed behavior in that file, drop it instead of inventing a broader complaint.
12. Return 2 to 4 bullets only. Fewer grounded findings are better than a longer generic list.
13. Do not use generic labels such as `tight coupling`, `lack of unit tests`, `inconsistent async patterns`, `hardcoded configuration`, or `error handling inconsistency` unless you also cite the exact method or code path that demonstrates that issue in this repository.
14. Do not say dependency inversion is missing when `AgentFrameworkWorkspaceService.cs` already injects interfaces. Describe concentration of responsibilities instead.
15. Do not say logging, error handling, or async support is missing when the reviewed method already shows those constructs. Only call out deeper architectural concentration or persistence concerns that the file actually demonstrates.
16. Before finalizing, silently drop any bullet that could apply to many unrelated repositories without changing the file names or method names.
17. For every finding, cite the exact file path or paths that support it, mention the concrete class or method involved, and explain the concrete behavior you observed.
18. If you do not have enough file-level evidence yet, continue reading instead of presenting a generic architecture complaint as fact.
