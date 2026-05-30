# SB02 Proof Manifest

## Scope

`SB02` updated resetup tooling so MCP binaries build from the sibling MCP repo while settings, installs, and skills remain rooted in the main CanDoItAll workspace. It also cleaned historical MCP artifacts.

## Changed File Hashes

- `repo://tools/Reinstall-CanDoItAllMcps.ps1` SHA-256 `85d5028b79ef9d399dbf5d13df483c8a2b5b4efa7b613c7ae57de7907ca578de`
- `repo://CanDoItAll.Mcp.DotNetWatch.settings.json` SHA-256 `08d5223e3ed18d797cd154eff6357eb873e7b7e0b4fe7caffbc2be1f85310d5d`
- MCP repo local context only: `tools/CanDoItAll.Mcp.DotNetWatch/Start-CanDoItAllDotNetWatchMcp.ps1` SHA-256 `b7d48e139cf6b534fdd732a86206253cff12039f5061a9abd6c8889c16ecb233`
- MCP repo local context only: `tools/CanDoItAll.Mcp.DotNetWatch.Tray/TrayOptions.cs` SHA-256 `869cd3268553a7dcecdda198513f30c0215342421a3a918a697c72778988b692`
- MCP repo local context only: `tests/CanDoItAll.Mcp.DotNetWatch.IntegrationTests/ValidationHarness.cs` SHA-256 `020d86638037f81ba2a561aac7f1f1de79b94c85f113e84909710d20a94fbae3`
- MCP repo local context only: `tests/CanDoItAll.Mcp.DotNetWatch.IntegrationTests/BootstrapValidationTests.cs` SHA-256 `a22f4763a24ff0481b5e0b6167e2541712709c60611f8f0be877cf7529442a0d`

## Semantic Invariant Contract

- `bundle://proof/SB02/semantic-invariants.md`

## Command Transcripts

- Passing transcript: `bundle://proof/SB02/transcripts/resetup.txt`
- Passing transcript: `bundle://proof/SB02/transcripts/wrapper-config-integration-tests.txt`
- Source assertion transcript: `bundle://proof/SB02/transcripts/source-and-config-assertions.txt`
- Cleanup assertion transcript: `bundle://proof/SB02/transcripts/artifact-cleanup.txt`
- Anti-stub audit transcript: `bundle://proof/SB02/transcripts/anti-stub-audit.txt`
- Failing-first: N/A - process/non-production setup and artifact migration; no production behavior changed.

## Test Names

- Test name: `RepositoryMcpConfig_UsesWrapperLauncher`
- Test name: `WrapperPrepareOnly_ProducesShadowManifestAndDllPath`

## Invariant Coverage

- `SB02-RESETUP-ROOT-SEPARATION`: proved by `bundle://proof/SB02/transcripts/resetup.txt`, `bundle://proof/SB02/transcripts/source-and-config-assertions.txt`, and `bundle://proof/SB02/transcripts/wrapper-config-integration-tests.txt`.
- `SB02-ARTIFACT-CLEANUP`: proved by `bundle://proof/SB02/transcripts/resetup.txt` and `bundle://proof/SB02/transcripts/artifact-cleanup.txt`.

## Anti-Stub Audit

`bundle://proof/SB02/transcripts/anti-stub-audit.txt` reports no `TODO`, `NotImplemented`, stub, or fixture-specific markers in resetup tooling.
