# Target Migration Architecture

```mermaid
flowchart TB
    VersionProps[Shared MAF stable and preview version properties]
    Projects[MAF adapter, workflow adapter, hosting projects]
    VersionProps --> Projects

    Facade[MafAgentRuntime singleton facade]
    Blueprint[Immutable preparation blueprint]
    Facade --> Build[Per-execution RuntimeBuildResult]
    Blueprint --> Build

    Build --> ChatAgent[ChatClientAgent with default 1.15 middleware]
    ChatAgent --> Binding[ApprovalResponseBinding enabled]
    ChatAgent --> Bypass[Approval-not-required bypass feature gate]
    ChatAgent --> FICC[Function invocation]
    FICC --> AppPolicy[CanDoItAll invocation policy and audit]
    AppPolicy --> Tools[Per-run custom tools]

    Build --> SessionCompatibility[Versioned application compatibility metadata]
    SessionCompatibility --> Classifier[State classifier]
    Classifier --> Native115[Native 1.15 continuation]
    Classifier --> Legacy113[Legacy 1.13 reissue or temporary bridge]
    Classifier --> ProviderManaged[Provider-managed continuation]
    Classifier --> WorkflowCheckpoint[Native workflow checkpoint path]

    Native115 --> Session[Restored MAF AgentSession]
    Legacy113 --> Reissue[Reissue approval under 1.15]
    Legacy113 -. controlled, expiring .-> Bridge[Trusted request plus response bridge]

    Build --> Workflow[Workflow-hosted handoff agent]
    Workflow --> ActivityProjection[Intermediate activity projection]
    Workflow --> TerminalProjection[Authoritative terminal output projection]
    ActivityProjection --> UI[Activity stream]
    TerminalProjection --> Result[AgentRuntimeResponse]
    Result --> Finalizer[Application finalizer/typed-output validation]

    Tools --> Workspace[Existing workspace/file/command/artifact services]
    Workspace --> Scope[Workspace scope, aliases, read-only and mutation policy]

    Build --> Disposal[Run-owned disposal]
```

## Target Decisions

### Package alignment

Use two shared properties, not five repeated literals:

```text
MicrosoftAgentsAIStableVersion = 1.15.0
MicrosoftAgentsAIPreviewVersion = 1.15.0-preview.260722.1
```

### Approval security

- binding remains enabled;
- old mixed-call behavior is preserved during parity through an explicit disable flag;
- pending approvals become request-addressed;
- legacy state is classified and cannot execute silently;
- MAF binding and CanDoItAll policy form defense-in-depth.

### Workflow output

- intermediate events remain available for user activity;
- one terminal projection is authoritative for machine output;
- response projection is not reconstructed by timestamp sorting;
- max handoff depth is enforced without forcing the wrong merge strategy.

### File tools

- no architectural replacement;
- no accidental Harness file provider;
- full regression and duplicate-tool inventory.

### State and rollback

- application compatibility metadata versions opaque MAF state;
- no in-place mutation of framework JSON;
- canary writes are measurable;
- rollback has a tested state-store strategy.
