# Runtime Flows

## Source Ingestion And Consolidation

```mermaid
sequenceDiagram
    autonumber
    participant Caller as UI or API caller
    participant Ingestion as CognitiveMemorySourceIngestionService
    participant Source as Source snapshot provider
    participant Db as AppDbContext
    participant Consolidation as CognitiveMemoryConsolidationEngine
    participant Authority as MutationAuthority
    participant Applicator as CandidateApplicator
    participant Review as ReviewUiService

    Caller->>Ingestion: IngestAsync(source kind, scope, cursor, take)
    Ingestion->>Source: ReadSnapshotAsync(...)
    Source-->>Ingestion: Snapshot manifest and items
    Ingestion->>Db: Upsert manifest, source items, evidence, layout, graph, hints, tombstones
    Caller->>Consolidation: RunAsync(project, mode, profile, policy, idempotency)
    Consolidation->>Db: Claim run lease and load source items
    loop each eligible source item
        Consolidation->>Authority: Submit governed mutation command
        Authority-->>Consolidation: Accepted, review required, or rejected
        Consolidation->>Db: Persist candidate and optional review item
        alt machine-generated candidate accepted
            Consolidation->>Applicator: Apply candidate as MachineGenerated / Experimental
            Applicator->>Db: Create memory record, claim, source link, evidence links
        else review required
            Consolidation->>Db: Leave pending review item
        end
    end
    Caller->>Review: DecideReviewItemAsync(approve/reject/etc.)
    alt approved consolidation candidate
        Review->>Applicator: Apply candidate as Approved / Active
        Applicator->>Db: Materialize canonical memory
    else rejected
        Review->>Db: Mark candidate rejected
    end
```

## Recall And Context Pack Rendering

```mermaid
sequenceDiagram
    autonumber
    participant Caller as API, UI, probe, or MAF contributor
    participant Recall as CognitiveMemoryRecallOrchestrator
    participant Db as AppDbContext
    participant Projection as Optional projection adapter
    participant Workspace as Workspace and attention services
    participant Signals as Signal ledger
    participant Scoring as Score geometry driver

    Caller->>Recall: RecallAsync(project, query, intent, mode, budget)
    Recall->>Db: Load lexical candidates from memory and source text
    opt projection inputs and provider available
        Recall->>Projection: Embed/search provider-scoped vector projection
        Projection-->>Recall: Projection hits
    end
    Recall->>Workspace: Add active workspace candidates
    Recall->>Signals: Add signal activation candidates
    Recall->>Db: Expand graph/source neighbors
    Recall->>Scoring: Evaluate candidate score vectors and shapes
    Recall->>Db: Load claims, evidence anchors, source refs
    Recall->>Db: Persist trace stages, candidates, context pack, context sections, source refs
    Recall-->>Caller: Context pack, candidates, stages, warnings
```

## Projection Rebuild

```mermaid
sequenceDiagram
    autonumber
    participant Caller as UI/API/operator
    participant Rebuild as ProjectionRebuildService
    participant Db as AppDbContext
    participant Lifecycle as ProjectionLifecycleService
    participant Provider as RAG/Qdrant projection provider

    Caller->>Rebuild: RebuildAsync(project, take, collection)
    Rebuild->>Db: Select stale, rebuild-required, or failed projection records
    loop each selected projection
        Rebuild->>Db: Load memory record, source links, evidence anchors, claims, context frames
        alt durable inputs are complete
            Rebuild->>Lifecycle: ProjectAsync(lifecycle request)
            Lifecycle->>Provider: Upsert/delete projection point as needed
            Provider-->>Lifecycle: Provider trace and projection state
            Lifecycle-->>Rebuild: Projection record result
            Rebuild->>Db: Preserve row identity and update projection status/hash/provider trace
        else durable inputs missing
            Rebuild->>Db: Leave projection unchanged and record explicit skip warning
        end
    end
    Rebuild->>Db: Complete projection run record
    Rebuild-->>Caller: Item results, skipped/failed/projected counts, warnings
```

## Explicit Automation Run

```mermaid
flowchart TD
    Caller["API/operator calls /automation/run"] --> Settings["Load CognitiveMemoryAutomationSettings"]
    Settings --> Trigger{"Trigger allowed by schedule mode?"}
    Trigger -- "No" --> Skipped["Return Executed=false with warning"]
    Trigger -- "Yes" --> ProjectIngest{"AutoIngestProjectStructure?"}
    ProjectIngest -- "Yes + project id" --> Workbench["Ingest Workbench project structure"]
    ProjectIngest -- "Yes + no project id" --> Warning["Add explicit warning"]
    ProjectIngest -- "No" --> ProcessCheck{"AutoIngestProcessRuntime?"}
    Workbench --> ProcessCheck
    Warning --> ProcessCheck
    ProcessCheck -- "Yes" --> Process["Ingest process runtime evidence"]
    ProcessCheck -- "No" --> ConsolidateCheck{"AutoConsolidateAfterIngestion and successful ingestion?"}
    Process --> ConsolidateCheck
    ConsolidateCheck -- "Yes" --> Consolidation["Run incremental or nightly consolidation"]
    ConsolidateCheck -- "No" --> Result["Return run summary"]
    Consolidation --> Result
```

## Lifecycle Flow

```mermaid
flowchart LR
    Source["Read-only source snapshot"] --> Manifest["Source manifest"]
    Manifest --> SourceItem["Source item"]
    SourceItem --> Evidence["Evidence anchor"]
    SourceItem --> Candidate["Consolidation candidate"]
    Candidate --> Mutation["Mutation command and audit"]
    Mutation --> Decision{"Review required?"}
    Decision -- "No" --> Memory["Canonical memory record"]
    Decision -- "Yes" --> Review["Review item"]
    Review -- "Approve" --> Memory
    Review -- "Reject / defer / changes" --> CandidateState["Candidate stays rejected, deferred, or needs changes"]
    Memory --> Claim["Claim"]
    Evidence --> ClaimLink["Claim/evidence link"]
    Claim --> ClaimLink
    Memory --> SourceLink["Source link"]
    SourceItem --> SourceLink
    Memory --> Recall["Recall candidate"]
    Claim --> Recall
    Evidence --> Recall
    Recall --> ContextPack["Context pack and trace"]
    ContextPack --> Maf["Agent-facing context package"]
    ContextPack --> Probe["Probe answer and feedback"]
    Probe --> Signals["Signals, calibration, proposals, review work"]
```

## MAF Context Contribution

```mermaid
sequenceDiagram
    autonumber
    participant Maf as AgentFramework MAF
    participant Contributor as CognitiveMemoryAgentContextContributor
    participant Settings as AutomationSettingsService
    participant Recall as RecallOrchestrator

    Maf->>Contributor: ContributeAsync(agent, provider, workspace scope, messages)
    Contributor->>Settings: GetAsync()
    Settings-->>Contributor: Model access policy settings
    alt provider not allowed, project scope missing, or memory unavailable in process-critical mode
        Contributor-->>Maf: Failed contribution with trace metadata
    else provider not allowed, project scope missing, or memory unavailable in interactive mode
        Contributor-->>Maf: Skipped with trace metadata
    else provider allowed and query available
        Contributor->>Recall: RecallAsync(FocusedTaskContext)
        alt recall succeeds and context is not empty
            Recall-->>Contributor: Context pack and trace id
            Contributor-->>Maf: System context message with trace metadata
        else memory unavailable or empty
            Contributor-->>Maf: Skipped with reason
        end
    end
```

## Current Flow Limits

- Recall always records stages and warnings when vector projection is skipped or unavailable.
- Projection invalidation and explicit rebuild are now first-class service/API flows, but provider-backed operational proof is still required for beta.
- Scheduled automation settings are persisted and can be executed explicitly through the API runner. There is still no dedicated hosted cognitive-memory worker.

