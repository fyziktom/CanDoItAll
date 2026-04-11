# Bundle-application audit

## Summary
- Previous in-repo apply-manifest entries: **501**
- Present targets in current repository: **24**
- Missing targets in current repository: **477**
- Missing target root distribution: **output=477**

## Key conclusion
The repository still contains the previous bundle folder and its completion narrative, but the actual file-driven template-pack tree was not materialized. The missing set is dominated by `output/process-template-pack/**`, which means the source-of-truth template files the user expected to see on disk are absent even though the documentation said the bundle had been executed.

## Representative missing targets
- `output/process-template-pack/README.md`
- `output/process-template-pack/framework-sources.json`
- `output/process-template-pack/framework-sources.md`
- `output/process-template-pack/manifest.json`
- `output/process-template-pack/processes/ai-assisted-change-delivery/artifacts/evaluation-benchmark-report.json`
- `output/process-template-pack/processes/ai-assisted-change-delivery/artifacts/evaluation-benchmark-report.md`
- `output/process-template-pack/processes/ai-assisted-change-delivery/artifacts/execution-trace-pack.json`
- `output/process-template-pack/processes/ai-assisted-change-delivery/artifacts/execution-trace-pack.md`
- `output/process-template-pack/processes/ai-assisted-change-delivery/checklists/agent-delegation-checklist.json`
- `output/process-template-pack/processes/ai-assisted-change-delivery/checklists/agent-delegation-checklist.md`
- `output/process-template-pack/processes/ai-assisted-change-delivery/definition.json`
- `output/process-template-pack/processes/ai-assisted-change-delivery/definition.md`
- `output/process-template-pack/processes/ai-assisted-change-delivery/mermaid/flowchart.mmd`
- `output/process-template-pack/processes/ai-assisted-change-delivery/mermaid/sequence.mmd`
- `output/process-template-pack/processes/ai-assisted-change-delivery/projection/current-module.compatibility-report.json`

## Missing-target concentration by process or resource area
- `shared/artifacts` → 66 missing files
- `shared/roles` → 50 missing files
- `customer-onboarding` → 36 missing files
- `incident-response` → 36 missing files
- `branching-code-review` → 34 missing files
- `shared/prompts` → 32 missing files
- `software-delivery` → 30 missing files
- `shared/validations` → 30 missing files
- `ai-assisted-change-delivery` → 29 missing files
- `shared/checklists` → 28 missing files
- `hotfix-rollout` → 26 missing files
- `oss-intake-supply-chain-governance` → 25 missing files

## Why this matters
- `Directory.Build.targets` already expects `output/process-template-pack/**/*.*` to exist and copy into build outputs.
- Loader, exporter, projection, and test paths all assume a real on-disk pack exists.
- Humans inspecting the repository will not find the template folders they were explicitly told to expect.

## Required action
Materialize the full pack before claiming completion. Any further architecture or QA work must treat this as a blocking baseline defect.
