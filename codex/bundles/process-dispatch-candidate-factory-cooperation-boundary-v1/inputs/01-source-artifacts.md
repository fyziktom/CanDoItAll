# Source Artifacts

Reviewed branch: `fyziktom/CanDoItAll@maf-processes-refactor`

Important source/proof references from the current branch:

- `codex/bundles/process-dispatch-candidate-hydration-boundary-v1/reviews/01-execution-report.md`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCandidateHeaderSelector.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCandidateHydrationLoader.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchArtifactInputAssembler.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchBranchDependencyContext.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchAssignmentRouteHelper.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchTechnicalAgentBindingCoordinator.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRecoveryQueryHelper.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Cooperation.cs`
- `codex/bundles/process-dispatch-candidate-hydration-boundary-v1/proof/SB18/transcripts/sb18-final-red-team-scan.txt`

Current confirmed status:
- Previous candidate-hydration bundle completed SB01-SB18.
- Browser validation is N/A; no UI files changed.
- No Process Core, production driver API, MAF back-dependency, or prohibited small/medium/mobile proof path was introduced.
- Candidate header selection and hydration readback are local helpers.
- LoadDispatchCandidateAsync still owns multi-route candidate construction, cooperation metadata, and some side-effect integration decisions.
