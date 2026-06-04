# SB01 Proof Manifest

## Scope

`SB01` moved active MCP source, tests, and MCP helper tools into the sibling MCP repository and removed those project entries from the main solution.

## Changed File Hashes

- `repo://CanDoItAll.slnx` SHA-256 `76e6d7b0bbd91a9ab53d75cdfa085d0a101e0f961fb1a21d0a729d2647dbafb4`
- MCP repo local context only: `CanDoItAll.Mcp.slnx` SHA-256 `0726409af61fb4c54ec4a7e56aaf42e90292bbe6c688bd1bfed3c8b36b129112`
- MCP repo local context only: `NuGet.config` SHA-256 `2c0070ca87c1b0d2a48167fe8fbbdc06863887efda825699edae9b3b911022f9`
- MCP repo local context only: `Directory.Build.props` SHA-256 `742c2bc87239b253852aa066b73d992366fd11363be889bcbe92ef97c836261c`
- MCP repo local context only: `global.json` SHA-256 `be73185c23fd60b6be834925fb612bdf094ac69c633ac55b5be9ae8ec79ee015`
- MCP repo local context only: `src/CanDoItAll.Mcp.Components/CanDoItAll.Mcp.Components.csproj` SHA-256 `4dbe65d4f12e2968d69d42d595dbe53679f8f0cef481800080863088a617449c`
- MCP repo local context only: `tests/CanDoItAll.Mcp.SshOps.Tests/CanDoItAll.Mcp.SshOps.Tests.csproj` SHA-256 `f2bb71e345979e823ac4c565eb8f3bedd92923fdc56c7bc949bb12a2318cf90f`
- MCP repo local context only: `tests/CanDoItAll.Mcp.SshOps.Tests/SshOpsIdleShutdownOptionsTests.cs` SHA-256 `deb72dfd6c5861b43cd7478bac9d40c7beead68693d97328deb4549d2a295166`
- MCP repo local context only: `tests/CanDoItAll.Mcp.SshOps.Tests/SshOpsSecretResolverTests.cs` SHA-256 `9d0468f5e37b3084279a993804b1d77d3ba3a77d481c097a5843501c0f4c6531`
- `repo://tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj` SHA-256 `d3eb399444b14b1d807f9f0802394b7ff495bdba90c2aa5a3e765f667df6a9cf`
- `repo://tests/CanDoItAll.Tests.Unit/README.md` SHA-256 `3f22f9bb6c957bb472ea1235a0acd0be74df54c44af2e1d0d6035e12062a341b`

## Semantic Invariant Contract

- `bundle://proof/SB01/semantic-invariants.md`

## Command Transcripts

- Passing transcript: `bundle://proof/SB01/transcripts/build-release.txt`
- Passing transcript: `bundle://proof/SB01/transcripts/focused-tests.txt`
- Source assertion transcript: `bundle://proof/SB01/transcripts/source-assertions.txt`
- Anti-stub audit transcript: `bundle://proof/SB01/transcripts/anti-stub-audit.txt`
- Failing-first: N/A - process/non-production repository migration; no production behavior changed.

## Test Names

- Test name: `CanDoItAll.Mcp.Components.Tests`
- Test name: `CanDoItAll.Mcp.DotNetWatch.Tests`
- Test name: `CanDoItAll.Mcp.Mermaid.Tests`
- Test name: `CanDoItAll.Mcp.SshOps.Tests`

## Invariant Coverage

- `SB01-MCP-SOLUTION-BOUNDARY`: proved by `bundle://proof/SB01/transcripts/build-release.txt`, `bundle://proof/SB01/transcripts/focused-tests.txt`, and `bundle://proof/SB01/transcripts/source-assertions.txt`.
- `SB01-COMPONENT-PACKAGE-DEPENDENCY`: proved by `bundle://proof/SB01/transcripts/source-assertions.txt` and `bundle://proof/SB01/transcripts/build-release.txt`.

## Anti-Stub Audit

`bundle://proof/SB01/transcripts/anti-stub-audit.txt` reports no `TODO`, `NotImplemented`, stub, or fixture-specific markers in migrated MCP production source and tools.
