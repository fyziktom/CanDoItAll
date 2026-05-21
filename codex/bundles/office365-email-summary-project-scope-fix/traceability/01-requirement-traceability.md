# Requirement Traceability

| Input or requirement | Bundle location | Owning subbundle | Planned proof | Notes |
| --- | --- | --- | --- | --- |
| R1 project scope reaches MAF contributors | `architecture/01-target-solution.md` | `subbundles/01-propagate-workflow-project-scope-to-agent-context` | `dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~AgentContextContributionTests|FullyQualifiedName~WorkflowExecutorTests"` | Covers invoker and runtime policy. |
| R2 missing project scope still fails | `analysis/02-assumptions-and-risks.md` | `subbundles/01-propagate-workflow-project-scope-to-agent-context` | `Cognitive_memory_contributor_fails_process_automation_when_project_scope_is_missing` | No silent fallback. |
| R3 empty memory is skipped explicitly | `architecture/01-target-solution.md` | `subbundles/01-propagate-workflow-project-scope-to-agent-context` | `Cognitive_memory_contributor_skips_empty_context_pack_for_process_automation` | Real new project condition. |
| R4 runContext preserved through asset creation | `analysis/01-current-state.md` | `subbundles/02-verify-office365-email-summary-creates-project-asset` | `ProjectStructureAgentApi_llm_workflow_uses_project_scope_and_creates_markdown_asset_under_workflow_node` | Prevents lease bypass. |
| R5 live summary content is correct | `inputs/00-original-request.md` | `subbundles/02-verify-office365-email-summary-creates-project-asset` | `bundle://proof/SB02/transcripts/live-office365-asset-proof.txt` | Mentions Tetris, static hosting, keyboard controls. |
| R6 asset under workflow node | `requirements/01-normalized-requirements.md` | `subbundles/02-verify-office365-email-summary-creates-project-asset` | `bundle://proof/SB02/transcripts/live-office365-asset-proof.txt` | `assetIsUnderWorkflowNode=true`. |
