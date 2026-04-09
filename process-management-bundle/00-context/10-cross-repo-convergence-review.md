# Cross-repo convergence review

This review compared the current process-management bundle against both uploaded repositories.

## CanDoItAll already owns the business truth in the right places

### CRM-HR
`CrmHrBusinessModels.cs` already contains staffing requests, AI execution modes, AI validation states, and project assignment kinds including AI agents.  
`AiAgentProfileEditor.razor` already binds an AI agent to:

- a shared Workspace provider profile
- a human owner or steward
- a validation status
- governance notes
- capability notes

That is exactly the kind of durable business identity the Processes module should reference, not clone.

### Workspace
`WorkspaceModels.cs` already contains provider profiles and default model semantics.  
This means the future AI execution bridge already has a natural shared provider registry.

### Projects
`ProjectModels.cs` already provides the project identity and scope anchor.  
This supports typed links from processes into project context without forcing process orchestration to become a copy of the project hierarchy.

### Canvas and modeling surfaces
CanvasLib plus Factory and Workbench patterns already provide enough graph/canvas behavior to implement both authored process diagrams and later runtime overlays.

## AgentFramework overlay still behaves like a research seam

The uploaded AgentFramework repo continues to be valuable, but its own documents already point toward convergence:

- durable agent ownership should converge into CRM-HR
- provider profiles should converge into shared Workspace truth
- project execution should stay aligned with CanDoItAll project/task context
- rights, sessions, logs, and metrics should stay explicit and attributable

That is compatible with this bundle **only if** the future bridge remains process-bound and registry-convergent.

## New convergence rules closed by this pass

1. Business role and agent templates remain CRM-HR-owned.
2. Shared provider and capability ownership remain in CanDoItAll, not in the future runtime adapter.
3. Future runtime sessions, logs, and metrics must correlate to `ProcessRun`, `ProcessStepRun`, and assignment context.
4. The process canvas can show live execution, but that overlay remains a projection.
5. Project scope and process orchestration remain distinct models with typed links between them.

## Conclusion

The current CanDoItAll repo is ready for a first-class Processes module now.  
The current AgentFramework overlay can be integrated later, but only through a bridge that respects the above ownership split.
