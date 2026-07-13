# Final Closure Validation

Date: 2026-07-08

## Prepared Verifier Attempt

Command:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File "codex\bundles\candoitall-maf-1.13-update-execution-bundle\inputs\original-prep\scripts\Verify-MafUpdate.ps1" -Configuration Release -SkipBroadTests -SkipPlaywright
```

Result:

- `dotnet --info`: completed.
- `dotnet restore CanDoItAll.slnx`: passed with known `Microsoft.OpenApi` 2.0.0 NU1903 warnings.
- `dotnet build CanDoItAll.slnx --configuration Release --no-restore`: passed with known `Microsoft.OpenApi` 2.0.0 NU1903 warnings.
- Prepared focused unit filter: passed `321/321`.
- Prepared focused integration filter: stalled in local vstest infrastructure after launching a very broad filter (`FullyQualifiedName~AgentFramework|FullyQualifiedName~Process|FullyQualifiedName~ProjectStructureAgent`) and was stopped.

The stalled process ids were limited to the verifier run:

- PowerShell verifier process: `61224`
- `dotnet test` process: `23256`
- `vstest.console` process: `52028`

## Targeted Integration Rerun Attempt

Command:

```powershell
dotnet test tests\Integration\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --configuration Release --no-build --filter "FullyQualifiedName~ProjectStructureAgentIntegrationTests|FullyQualifiedName~AgentFrameworkExecutionRunTrackingIntegrationTests|FullyQualifiedName~MafAgentRuntimeHandoffTests"
```

Result:

- The run started normally, but the local test host stalled after discovery output and was stopped.
- The live 5032 managed app was stopped before the final poll to remove possible runtime/database contention.
- This rerun is not counted as a pass.

Accepted integration proof remains the completed SB05 transcript:

`bundle://proof/SB05/transcripts/focused-integration-tests-after-readiness-and-provider-fixes.md`

That transcript passed the same targeted risk surface after the code fixes:

- `ProjectStructureAgentIntegrationTests`
- `AgentFrameworkExecutionRunTrackingIntegrationTests`
- `MafAgentRuntimeHandoffTests`
- Result: `58/58`

No production code changes were made after that passing transcript; subsequent changes were bundle proof/docs only.

## Static Guardrails

Stale stable MAF package scan:

```powershell
rg 'Microsoft\.Agents\.AI" Version="1\.8\.0|Microsoft\.Agents\.AI\.OpenAI" Version="1\.8\.0|Microsoft\.Agents\.AI\.Workflows" Version="1\.8\.0' src tests tools -g "*.csproj"
```

Result: no matches, exit code `1` as expected.

Production process runtime-provider scan:

```powershell
rg 'registers .*ProcessAgentRuntimeToolProvider|new ProcessAgentRuntimeToolProvider|class ProcessAgentRuntimeToolProvider|Add.*ProcessAgentRuntimeToolProvider|ProcessManagerTools' src tests -g "*.cs"
```

Result: no matches, exit code `1` as expected.

Production process API expansion scan:

```powershell
rg '/api/processes/definitions|/api/processes/templates|/api/processes/runs/\{runId\}/detail' src\App src\Modules tests -g "*.cs" -g "*.razor"
```

Result: no matches, exit code `1` as expected.

Raw docs-inclusive process scan note:

- A broader docs-inclusive scan matched `docs\processes-maf-providers-implementation-map.md` because that document contains the guardrail `rg` command as text.
- This is a documented false positive, not a production registration or API route.

Diff hygiene:

```powershell
git diff --check
```

Result:

- No whitespace errors.
- Git emitted line-ending warnings for touched files because the worktree will normalize LF to CRLF when Git next touches them.

## Live App Validation

See:

- `bundle://proof/SB05/transcripts/live-5032-floating-chat-pdf-to-xlsx.md`
- `bundle://proof/SB05/transcripts/final-workbook-inspection.md`

Result: `Pass`

The rebuilt 5032 app served the project-structure route, the floating chat completed through MAF streaming, the agent read the PDF asset and generated an XLSX project-structure asset, and the final workbook content matched the PDF-derived target and margin calculations.
