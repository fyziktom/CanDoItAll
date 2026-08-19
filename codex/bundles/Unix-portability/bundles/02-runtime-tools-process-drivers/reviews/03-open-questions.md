# Runtime open questions to resolve during B00/B01

1. Can `LocalWorkspaceProcessHost` be reused directly by ExternalProcessToolInvoker and Docker without creating undesirable MAF Core dependencies, or should a smaller neutral process primitive be extracted?
2. Does `.Kill(entireProcessTree: true)` satisfy child/grandchild cleanup on the target .NET/runtime/OS combinations?
3. What exact process discovery API is reliable and distributable on macOS?
4. Which environment variables are safe/common versus tool-specific for dotnet, node/npm, Docker, Python, MCP, and Manager?
5. How should explicit executable allowlists identify symlinked/version-manager executables without accepting substitution?
6. Which Linux/macOS terminal presentation adapters are supportable, and should the first R4 claim omit them in favor of direct/headless execution?
7. Does FileTools package 0.1.18 actually implement safe macOS/Linux open/reveal behavior?
8. Which existing capability model should represent host runtime dependencies without granting authority?
9. Does the Core C4 implementation already alter path/executable/capability contracts enough to trigger a runtime subbundle split?
