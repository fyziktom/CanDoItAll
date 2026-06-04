# SB04 Semantic Invariants

## Invariants

- `SB04-INV-001`: Claimed production E2E proof must be automation-dispatch driven. The proof script must not call manual process transitions or pass `suppressAutomationDispatch=true`.
- `SB04-INV-002`: The harness may create request packets and start runs, but it must not scaffold or write generated app source for the claimed production proof path.
- `SB04-INV-003`: Every scenario must contain a current process run id, process-run detail, non-empty execution runs, tool receipts, provider usage observations, generated source root, generated source layout, build transcript, browser summary, and cleanup receipt.
- `SB04-INV-004`: Generated app roots must be under the current process run output and must end with `GeneratedBlazorApp`; the runnable project must be `GeneratedBlazorApp.csproj` directly under that root.
- `SB04-INV-005`: Browser proof must include desktop and mobile rows with rendered interactive UI and no blocking console/page/network failures.
- `SB04-INV-006`: Production templates, agents, skills, and seed assets must not branch on the five scenario keys.

## Evidence

- `bundle://proof/SB04/manifest.json`
- `bundle://proof/SB04/manifest.md`
- `bundle://proof/SB04/scenarios/*/agent-execution-runs.json`
- `bundle://proof/SB04/scenarios/*/tool-receipts.json`
- `bundle://proof/SB04/scenarios/*/usage-summary.json`
- `bundle://proof/SB04/scenarios/*/generated-source-root.json`
- `bundle://proof/SB04/scenarios/*/generated-source-root-layout.json`
- `bundle://proof/SB04/scenarios/*/browser/browser-validation-summary.json`
- `bundle://proof/SB05/transcripts/passing-new-sb04-proof.txt`
- `bundle://proof/SB07/transcripts/template-contract-and-scenario-scan.txt`

## Residual Risk

The run set uses real provider/process infrastructure and therefore remains environment-sensitive. The proof is fixed to concrete run ids and artifacts; future regressions should be caught by rerunning the harness and the SB05 proof-quality gate.
