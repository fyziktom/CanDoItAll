# CanDoItAll process template execution bundle — current architecture aligned

This bundle is the revised and expanded successor to the original process-template execution bundle.

## What changed
- The template pack now aligns to the current process-module architecture and uses first-class **dependencies**, **artifact inputs**, **decision roles**, **branch coordinates**, and explicit **branch outcomes**.
- The pack now contains **9 process templates** and **5 baseline scenarios** aligned to the current repository.
- The pack includes a dedicated **branching-code-review** template and updated projections for the current module import envelope.
- The bundle adds strict **architecture review gates** and a mandatory **corrective subbundle rule**: if review detects architectural drift, the run must stop, a corrective subbundle must be added, and only then may downstream work continue.
- The bundle explicitly isolates the remaining hardcoded canvas chrome debt in `ProcessCanvasSurfaceFactory` and provides a corrective subbundle for de-hardcoding it.
- Bundle execution closed that corrective path by loading definition chrome from the `chrome-actions.json` sidecar through `ProcessCanvasChromeCatalogService`.

## Main contents
- `repo-overlay/output/process-template-pack/` — file-driven template pack and sidecars
- `artifacts/process-template-catalog.xlsx` — workbook catalog
- `subbundles/` — execution-grade staged plan with strict gates
- `tools/validate_process_template_pack.py` — pack validator
- `analysis/` — architecture and QA review notes
- `codex/MASTER_TASKS.json` — execution order for Codex or another automation runner

## Current template inventory
- `software-delivery` — Multi-team software delivery and release governance
- `branching-code-review` — Branching code review and merge governance
- `hotfix-rollout` — Emergency hotfix rollout with shard-risk governance
- `customer-onboarding` — Customer onboarding orchestration
- `incident-response` — Incident response and escalation
- `architecture-decision-governance` — Architecture decision governance and ADR stewardship
- `release-readiness-and-deployment` — Release readiness and deployment control
- `oss-intake-supply-chain-governance` — Open-source intake and supply-chain governance
- `ai-assisted-change-delivery` — AI-assisted change delivery with guarded delegation

## Current baseline scenarios
- `software-delivery` — Multi-team software delivery and release governance / billing export capability
- `branching-code-review` — Branching code review and merge governance / account-settings UI
- `hotfix-rollout` — Emergency hotfix rollout with shard-risk governance / checkout latency
- `customer-onboarding` — Customer onboarding orchestration / Contoso enterprise rollout
- `incident-response` — Incident response and escalation / tenant login disruption

## Execution status
Bundle execution completed in `C:\repositories\CanDoItAll` on `2026-04-11`.

Validation that was actually executed:
- `python cdi_process_templates_bundle/tools/validate_process_template_pack.py output/process-template-pack`
- `dotnet build CanDoItAll.slnx -v:minimal`
- `dotnet test tests/CanDoItAll.Mcp.Processes.Tests/CanDoItAll.Mcp.Processes.Tests.csproj -v:minimal`
- `dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --no-build --filter "FullyQualifiedName~ProcessCanvasSurfaceFactoryTests|FullyQualifiedName~ProcessCanvasChromeCatalogServiceTests" -v:minimal`
- `dotnet test tests/CanDoItAll.Tests.Playwright/CanDoItAll.Tests.Playwright.csproj --filter "FullyQualifiedName~Process_management_canvas_bundle_flows_are_validated_in_browser" -v:minimal`

Browser proof artifacts were captured under `output/playwright/process-management-bundle/`.
