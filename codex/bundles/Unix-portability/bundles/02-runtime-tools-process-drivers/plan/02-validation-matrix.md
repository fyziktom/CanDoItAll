# Validation matrix

| Subbundle | Entry | Focused proof examples | Minimum closure |
|---|---|---|---|
| B00 | Core Gate C4 | python ./scripts/scan_portability.py --repo-root <repo> --output <repo>/artifacts/unix-portability/B00/runtime-scan.json; dotnet build ./CanDoItAll.slnx -c Release --no-restore /m:1 | Gate R0 is GO against the exact Core C4 commit.; One owner exists for every runtime responsibility and no process-semantic rule is assigned to MAF/Infrastructure. |
| B01 | B00, Gate R0 | dotnet test ./tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj -c Release --filter 'FullyQualifiedName~LocalWorkspaceProcessHost\|FullyQualifiedName~WorkspaceExecutable\|FullyQualifiedName~EnvironmentPolicy\|FullyQualifiedName~ExternalProcess'; dotnet test ./tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj -c Release --filter 'Category=ProcessPortability' | Gate R1a is GO on Windows/Linux/macOS actual-host tests.; One low-level process primitive and lifecycle owner are authoritative. |
| B02 | B01 | dotnet test ./tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj -c Release --filter 'FullyQualifiedName~ProjectStructureRuntime\|FullyQualifiedName~DirectDotNetCommand\|FullyQualifiedName~Python'; dotnet test ./tests/Playwright/CanDoItAll.Tests.Playwright/CanDoItAll.Tests.Playwright.csproj -c Release --filter 'FullyQualifiedName~RuntimeNode\|FullyQualifiedName~ProjectStructure' | Runtime-node plans are typed and shell-neutral.; Direct headless execution works on Windows/Linux/macOS. |
| B03 | B02 | dotnet test ./tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj -c Release --filter 'FullyQualifiedName~WorkspaceRuntimeProcessTools\|FullyQualifiedName~WatchSupervisor\|FullyQualifiedName~Tailwind\|FullyQualifiedName~Manager'; dotnet test ./tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj -c Release --filter 'Category=ManagerPortability' | Gate R2 is GO.; No process is killed using name-only or ambiguous evidence. |
| B04 | B03, Gate R2 | dotnet test ./tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj -c Release --filter 'FullyQualifiedName~Mcp\|FullyQualifiedName~PlaywrightMcp\|FullyQualifiedName~ExternalProcessTool'; dotnet test ./tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj -c Release --filter 'Category=McpPortability\|Category=ExternalToolPortability' | Gate R3a is GO.; Local MCP/external tools use authoritative execution and secret boundaries. |
| B05 | B04 | dotnet test ./tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj -c Release --filter 'FullyQualifiedName~DockerHostTool\|FullyQualifiedName~DesktopFile\|FullyQualifiedName~FileTools\|FullyQualifiedName~ProjectStructureLocalFile'; dotnet test ./tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj -c Release --filter 'Category=PluginPortability\|Category=DesktopPortability' | Gate R3b is GO.; Docker and plugin tools use authoritative host execution and capability probes. |
| B06 | B05 | dotnet test ./tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj -c Release --filter 'FullyQualifiedName~ProcessDriver\|FullyQualifiedName~ProcessStrategy\|FullyQualifiedName~ToolReceipt\|FullyQualifiedName~Authority\|FullyQualifiedName~WorkspaceScope'; dotnet test ./tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj -c Release --filter 'Category=ProcessCapabilityPortability' | Gate R3 is GO.; Processes remains the semantic owner and MAF remains a generic execution adapter. |
| B07 | B06, Gate R3 | dotnet restore ./CanDoItAll.slnx; dotnet build ./CanDoItAll.slnx -c Release --no-restore /m:1 | Final Gate R4 is GO with actual-host Windows/Ubuntu/macOS evidence.; All runtime P0 requirements are Solved and no critical finding remains open. |

## Universal gates

Every mandatory subbundle also runs the repository stable Release gate appropriate to its point in the program and records:

- exact command and exit code;
- OS/profile/architecture/tool versions;
- log/evidence path;
- pre-existing versus introduced failures;
- redaction result;
- changed requirements/findings/source references.

By the final bundle gate, Windows, Ubuntu, and macOS actual-host evidence is mandatory.
