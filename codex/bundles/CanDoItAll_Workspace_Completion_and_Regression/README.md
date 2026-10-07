# Workspace completion and application regression campaign

Start with [prompt.md](prompt.md). This is an executable handoff for Codex GPT-6 Astra Max on the current CanDoItAll checkout, not a request for a plan only.

The last reviewed product commit is `fbfba65de9d3118729b73ce9dcf97f28a219c84c`. Storage selection and its same-target prerequisite are present. No new blocking defect was identified in the freshly reviewed paths. That conclusion is source review, not a product execution certificate. Workspace still has Data Sources, Recovery/continuations and configuration-renderer work.

Complete those remaining UI boundaries as separate, tested stages, then run a frozen-checkpoint application campaign. Do not create a giant Workspace manager or start another module's UI extraction. Small reproduced regressions may be repaired; complex problems receive detailed causal mapping and future repair inputs instead of an improvised redesign.

## Reading map

| Document | Purpose |
|---|---|
| [Review](REVIEW.md), [Workspace map](WORKSPACE_COMPLETION_MAP.md) | Actual reviewed state, remaining scope, and limits |
| [Architecture](ARCHITECTURE.md) | Protected dependency direction and responsibilities |
| [Recovery](PHASE_RECOVERY.md) | Exact placement and prepared-owner continuation UI |
| [Data Sources](PHASE_DATA_SOURCES.md) | Profiles, schema, transfer and restart activation |
| [Configuration](PHASE_CONFIGURATION.md) | Neutral fallback and trusted host classification |
| [Campaign](REGRESSION_CAMPAIGN.md), [live journeys](LIVE_AGENT_JOURNEYS.md) | Cross-module operator paths with independent effect oracles |
| [Validation](VALIDATION_MATRIX.md), [evidence](EVIDENCE.md) | Discovery, final gates, replay and source-checkpoint rules |
| [Triage](TRIAGE.md), [safe environment](SAFE_ENVIRONMENT.md) | Small fixes versus complex findings and isolation |
| [Development loop](DEV_LOOP.md), [sources](SOURCES.md) | Graph/watch proof and exact review provenance |

The [shared v3 foundation](shared/README.md) is included unchanged. Its historical module map is not the current inventory. Current repository instructions and canonical `docs/architecture/ui-component-seams.md` remain authoritative.

`campaign-plan.json` contains the required case groups. Copy `templates/campaign-results.json` to an ignored evidence directory and append actual attempts; it intentionally begins with every case NOT_RUN. `tools/validate_campaign.py` checks result bookkeeping and evidence hashes, not the truth of runtime claims. `--require-complete` must reject that untouched template.

No .NET build, product test, browser run, live model request or performance measurement was executed while preparing this handoff. Artifact checks are reported separately in [PACKAGE_VALIDATION.md](PACKAGE_VALIDATION.md).
