# SB01 Semantic Invariants

## Invariant SB01-MCP-SOLUTION-BOUNDARY

- Invariant ID: `SB01-MCP-SOLUTION-BOUNDARY`
- Source raw note: `N001` requested moving MCP servers to their own repo and creating a solution there just for MCPs.
- Expected behavior: The MCP repo contains the active MCP source, tests, helper tools, and `CanDoItAll.Mcp.slnx`; the main `CanDoItAll.slnx` no longer includes moved MCP projects.
- Disallowed shallow implementation: Creating an empty solution or docs-only repo while still building MCP projects from the main solution.
- Failing-first test: N/A - process/non-production repository migration; no production behavior changed. Negative proof is the source assertion transcript that rejects any moved MCP project path remaining in the main solution.
- Passing test: `bundle://proof/SB01/transcripts/build-release.txt` and `bundle://proof/SB01/transcripts/focused-tests.txt`.
- Changed source files: `repo://CanDoItAll.slnx`, `repo://tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj`, `repo://tests/CanDoItAll.Tests.Unit/README.md`; MCP repo local context only: `CanDoItAll.Mcp.slnx`, `Directory.Build.props`, `NuGet.config`, `global.json`, moved MCP `src`, `tests`, and `tools`.
- Production assertions: N/A - source layout and build tooling migration only; no application runtime behavior was changed.
- Red-team negative case: `bundle://proof/SB01/transcripts/source-assertions.txt` fails if migrated MCP paths remain in the main solution.
- Downstream dependency check: `SB02` resetup proof consumes the MCP solution and wrapper path produced by this invariant.

## Invariant SB01-COMPONENT-PACKAGE-DEPENDENCY

- Invariant ID: `SB01-COMPONENT-PACKAGE-DEPENDENCY`
- Source raw note: `N001` required component dependencies to be connected as NuGet packages.
- Expected behavior: `CanDoItAll.Mcp.Components` keeps `CanDoItAll.Components.*` dependencies as package references through MCP repo restore configuration.
- Disallowed shallow implementation: Replacing component packages with main-repo project references or hardcoded component metadata.
- Failing-first test: N/A - process/non-production repository migration; no production behavior changed. Negative proof is the source assertion transcript that checks package references explicitly.
- Passing test: `bundle://proof/SB01/transcripts/source-assertions.txt` and `bundle://proof/SB01/transcripts/build-release.txt`.
- Changed source files: MCP repo local context only: `src/CanDoItAll.Mcp.Components/CanDoItAll.Mcp.Components.csproj`, `NuGet.config`.
- Production assertions: N/A - build dependency wiring only.
- Red-team negative case: `bundle://proof/SB01/transcripts/source-assertions.txt` fails if any expected component package reference is absent.
- Downstream dependency check: `SB02` resetup publishes the Components MCP from the MCP repo after package restore.
