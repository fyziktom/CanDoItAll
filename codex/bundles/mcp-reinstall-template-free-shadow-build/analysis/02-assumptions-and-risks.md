# Assumptions And Risks

## Assumptions

- Repository templates remain needed for other app/build workflows, so the shared target should be controllable rather than deleted.
- MCP install artifacts need compiled MCP outputs and runtime support files, not repository template packs.
- DotNetWatch should still run from a copied artifact location to avoid locking normal repo build outputs.

## Critical Path Risks

- If template copying is disabled globally by default, non-MCP workflows that expect `Templates` under build output could regress.
- If the shadow copy misses runtime files from the built output directory, DotNetWatch may prepare successfully but fail to launch.
- If publish steps still allow repository templates, non-DotNetWatch MCP installs may keep copying unneeded files into artifacts.

## Validation Risks

- A targeted `dotnet build` is not enough proof; the failing path is the full reinstall script.
- `-PrepareOnly` proves DotNetWatch shadow preparation but not Components/CodeAnalytics/SshOps publish or skill sync.
- Existing background processes may be stopped by the reinstall script as designed; validation should treat that as expected host behavior.

## Reopen Triggers

- `tools\Reinstall-CanDoItAllMcps.ps1` fails at any MCP build or publish step.
- Any `.artifacts\mcp-server-shadow` or `.artifacts\mcp-installs` MCP output contains a copied top-level `Templates` directory after reinstall.
- The install manifest is missing expected MCP entries or the skills sync section after reinstall.
