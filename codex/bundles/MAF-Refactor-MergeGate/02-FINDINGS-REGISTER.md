# Findings register

    | ID | Severity | Finding | Required |
    |---|---:|---|---:|
    | MRG-001 | P0 | Malformed or incomplete current authority projection degrades to no governance | Yes |
| MRG-002 | P1 architecture | Source authority provider registry is hard-coded and owns other modules' semantics | Yes |
| MRG-003 | P0 | Effective process policy context is discarded after evaluation | Yes |
| MRG-004 | P0 | Project-scoped kept-alive process leases are cleaned through an organization-scoped cleaner | Yes |
| MRG-005 | P0 | File conversation CAS is serialized only inside one scoped store instance | Yes |
| MRG-006 | P1 | Failed provider adoption cannot restore pre-turn provider or acceleration state | Yes |
| MRG-007 | P1 | Concurrent rename can orphan an active ordinary-conversation turn | Yes |
| MRG-008 | P1 | Ordinary-conversation turn capacity is not reserved before provider invocation | Yes |
| MRG-009 | P1 | Lightweight LLM empty-response retry drops usage from earlier attempts | Yes |
| MRG-010 | P1 activation | Optional ordinary conversation service is production-registered without profile-switch fencing | Yes |
| MRG-011 | Release gate | Current HEAD has no visible CI result or executed follow-up proof set | Yes |


        ## MRG-001 — Malformed or incomplete current authority projection degrades to no governance

        **Severity:** P0  
        **Owner:** Core execution admission  
        **Merge blocker:** Yes

        The authority parser returns null both for genuinely absent legacy metadata and for malformed current metadata. Runtime restoration can therefore classify a corrupted context-admitted turn as having no governance snapshot instead of rejecting it before runtime construction.

        **Primary paths**

        - `src/MAF/Common/CanDoItAll.AgentFramework.Core/Context/AgentTurnContextMetadata.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`

        ## MRG-002 — Source authority provider registry is hard-coded and owns other modules' semantics

        **Severity:** P1 architecture  
        **Owner:** Module composition  
        **Merge blocker:** Yes

        The Core provider SPI exists, but the resolver constructs a hard-coded default list. Project, Project Structure, and Processes authority semantics all live in Modules.AgentFramework rather than the modules that publish those source kinds. This is not a real modular registry and leaves process/product semantics in the integration module.

        **Primary paths**

        - `src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentExecutionAuthorityComposition.cs`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentExecutionSourceAuthorityProviders.cs`

        ## MRG-003 — Effective process policy context is discarded after evaluation

        **Severity:** P0  
        **Owner:** Provider-neutral tool governance pipeline / MAF mapping  
        **Merge blocker:** Yes

        ComposeAndEvaluateAsync evaluates an enriched local context but returns only the decision. MafRuntimeAgentFactory then passes the original neutral context to the block guard, telemetry, logging, and effective-approval checks. Governed denials may become fatal instead of recoverable, and process identity/restrictions disappear from diagnostics. The process-contributor presence check also relies on ReferenceEquals and can be bypassed by an unrelated cloning contributor.

        **Primary paths**

        - `src/MAF/Common/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicyPipeline.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeAgentFactory.cs`
- `src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessToolInvocationPolicyContextContributor.cs`

        ## MRG-004 — Project-scoped kept-alive process leases are cleaned through an organization-scoped cleaner

        **Severity:** P0  
        **Owner:** Workspace execution lifetime  
        **Merge blocker:** Yes

        Floating chat execution is persisted by an organization-scoped workspace service, while MAF creates a project-scoped per-run command service for a Project Structure turn. That service stores durable process leases under the project-scoped audit root. Terminal cleanup uses the organization workspace's fixed-scope command service and therefore enumerates a different lease directory.

        **Primary paths**

        - `src/Modules/CanDoItAll.Modules.AgentFramework/Workspace/CurrentProfileAgentFrameworkWorkspaceService.cs`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Workspace/AgentFrameworkWorkspaceFactory.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ProcessLeases.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Process/WorkspaceExecutionRunProcessLeases.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Commands/WorkspaceCommandExecutionService.cs`

        ## MRG-005 — File conversation CAS is serialized only inside one scoped store instance

        **Severity:** P0  
        **Owner:** Ordinary LLM conversation persistence  
        **Merge blocker:** Yes

        The file store uses an instance-local semaphore dictionary and is registered scoped. Two scopes can read the same revision, both pass the compare-and-swap check, and overwrite each other. Existing tests race one store instance and do not reproduce the production lifetime topology.

        **Primary paths**

        - `src/MAF/Common/CanDoItAll.AgentFramework.Llm.Conversations/FileLlmConversationStore.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Llm.Conversations/LlmConversationServiceCollectionExtensions.cs`

        ## MRG-006 — Failed provider adoption cannot restore pre-turn provider or acceleration state

        **Severity:** P1  
        **Owner:** Ordinary LLM conversation state machine  
        **Merge blocker:** Yes

        Adopt is persisted at admission, but rollback is rebuilt from the admitted document and preserves the adopted provider while acceleration remains cleared. A process crash has the same problem because ActiveTurn does not persist the pre-turn compensation state.

        **Primary paths**

        - `src/MAF/Common/CanDoItAll.AgentFramework.Llm.Abstractions/LlmConversationContracts.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Llm.Conversations/LlmConversationService.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Llm.Conversations/FileLlmConversationStore.cs`

        ## MRG-007 — Concurrent rename can orphan an active ordinary-conversation turn

        **Severity:** P1  
        **Owner:** Ordinary LLM conversation state machine  
        **Merge blocker:** Yes

        Rename is permitted while a provider turn is in flight and advances the revision. Completion then fails CAS; ConcurrencyConflict is excluded from compensation, leaving the pending user entry and ActiveTurn marker until manual abandonment.

        **Primary paths**

        - `src/MAF/Common/CanDoItAll.AgentFramework.Llm.Conversations/LlmConversationService.cs`

        ## MRG-008 — Ordinary-conversation turn capacity is not reserved before provider invocation

        **Severity:** P1  
        **Owner:** Ordinary LLM conversation state machine  
        **Merge blocker:** Yes

        A conversation with one remaining transcript slot can admit the user entry and call the provider, then fail while appending the assistant entry. Capacity for the complete turn must be checked before the billable provider call.

        **Primary paths**

        - `src/MAF/Common/CanDoItAll.AgentFramework.Llm.Conversations/LlmConversationService.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Llm.Abstractions/LlmConversationContracts.cs`

        ## MRG-009 — Lightweight LLM empty-response retry drops usage from earlier attempts

        **Severity:** P1  
        **Owner:** Lightweight LLM invocation and workflow usage  
        **Merge blocker:** Yes

        The adapter retries one empty provider response but returns only final-attempt usage. Failure paths also expose no accumulated usage, so workflow failure analytics can record zero despite billable provider attempts.

        **Primary paths**

        - `src/MAF/Common/CanDoItAll.AgentFramework.Llm.Abstractions/LlmInvocationContracts.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Llm.ProviderRuntime/ProviderBackedLlmInvocationAdapter.cs`
- `src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.Runtime/WorkflowLlmComponentInvoker.cs`

        ## MRG-010 — Optional ordinary conversation service is production-registered without profile-switch fencing

        **Severity:** P1 activation  
        **Owner:** Application composition  
        **Merge blocker:** Yes

        The scoped service resolves its file root once. A long-lived Blazor scope can survive a database profile switch and retain the previous profile root. As no product surface consumes this optional foundation yet, the merge-safe action is to keep it dormant until a profile-aware integration exists.

        **Primary paths**

        - `src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Llm.Conversations/LlmConversationServiceCollectionExtensions.cs`

        ## MRG-011 — Current HEAD has no visible CI result or executed follow-up proof set

        **Severity:** Release gate  
        **Owner:** Validation  
        **Merge blocker:** Yes

        The branch contains many tests and proof templates, but the reviewed HEAD exposes no GitHub check or workflow result and the follow-up bundle has no completed proof manifests. Fresh independent build, tests, guards, and application smoke evidence are mandatory.

        **Primary paths**

        - `codex/bundles/MAF-Refactor/`
- `codex/bundles/MAF-Refactor-Followup/`


    ## Closure standard

    Each finding requires a pre-fix failing test or executable dependency proof, an owner-boundary fix,
    focused and neighboring tests, architecture guards where applicable, and recorded full-suite impact.
