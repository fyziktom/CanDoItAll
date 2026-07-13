# Requirement Traceability

| Requirement | Primary subbundles | Source proof | Validation proof |
| --- | --- | --- | --- |
| REQ-001 | SB01, SB04, SB07 | `bundle://codex/01-launch-variable-placeholder-resolution.md` | Resolver unit tests and incident integration. |
| REQ-002 | SB01, SB08, SB09 | `repo://src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureProcessLaunchVariableContributor.cs` | Template/launch validation fails unresolved tool refs. |
| REQ-003 | SB02 | `bundle://codex/02-completion-gate-aggregator.md` | Aggregate gate unit and adapter tests. |
| REQ-004 | SB02, SB04, SB06 | `bundle://evidence/incident-facts.json` | Packet assertions preserve diagnostic metadata. |
| REQ-005 | SB03 | `bundle://codex/03-safe-auto-rework-recovery.md` | Runtime engine safe retry tests. |
| REQ-006 | SB03, SB12 | `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.ResultHelpers.cs` | Budget exhaustion and unsafe diagnostic tests. |
| REQ-007 | SB04 | `bundle://codex/04-diagnostic-specific-rework-packets.md` | Rework packet unit tests and incident transcript. |
| REQ-008 | SB06 | `bundle://codex/05-subprocess-child-diagnostics-and-ledger-bridge.md` | Parent packet includes child diagnostic. |
| REQ-009 | SB05, SB06 | `repo://src/Processes/CanDoItAll.Processes.Runtime/ParentSubprocessArtifactBridge.cs` | File-existence negative test. |
| REQ-010 | SB05 | `bundle://codex/06-managed-artifact-acceptance-order.md` | Projection/wording and slot promotion tests. |
| REQ-011 | SB07, SB10 | `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeToolPreflightService.cs` | Exact tool-plan preflight tests. |
| REQ-012 | SB08, SB09 | `bundle://codex/08-template-agent-contract-hardening.md` | Template schema validation over all templates. |
| REQ-013 | SB07 | `bundle://codex/07-runtime-owned-dotnet-solution-setup-plan.md` | Guard rejects missing helper receipt. |
| REQ-014 | SB11 | `bundle://codex/07-runtime-owned-dotnet-solution-setup-plan.md` | Runtime-owned executor integration. |
| REQ-015 | SB10 | `repo://Templates/Agents/teams/dotnet-delivery/members/dotnet-application-developer/instructions.md` | Assignment/capability mismatch tests. |
| REQ-016 | SB09, SB12 | `bundle://inventories/02-process-template-inventory.md` | Full template audit manifest. |
| REQ-017 | All critical subbundles | `bundle://README.md` | Semantic invariants and anti-stub proof. |
| REQ-018 | All architecture-heavy subbundles | `bundle://architecture/01-csharp-boundary-map.md` | C# architecture gate. |
| REQ-019 | SB12 | `bundle://codex/09-test-and-validation-checklist.md` | Targeted and manual validation transcripts. |
| REQ-020 | All implementation subbundles | User AGENTS.md | Code review and architecture closure. |
