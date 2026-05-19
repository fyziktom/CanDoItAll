namespace CanDoItAll.Web.Api;

internal static partial class CognitiveMemoryApi
{
    private const string CognitiveMemoryApiContractVersion = "v1";
    private const string LegacyApiBasePath = "/api/cognitive-memory";
    private const string V1ApiBasePath = "/api/cognitive-memory/v1";

    private enum CognitiveMemoryApiSurface
    {
        Legacy = 0,
        V1 = 1
    }

    private static void MapContractEndpoints(
        RouteGroupBuilder memory,
        CognitiveMemoryApiSurface surface)
    {
        memory.MapGet("/contract", () => Results.Ok(BuildApiContract(surface)))
            .WithName(EndpointName("GetCognitiveMemoryApiContract", surface));
    }

    private static string EndpointName(
        string legacyName,
        CognitiveMemoryApiSurface surface)
    {
        return surface == CognitiveMemoryApiSurface.Legacy
            ? legacyName
            : $"{legacyName}V1";
    }

    private static CognitiveMemoryApiContractResponse BuildApiContract(CognitiveMemoryApiSurface surface)
    {
        var basePath = ApiBasePath(surface);
        return new CognitiveMemoryApiContractResponse(
            CognitiveMemoryApiContractVersion,
            basePath,
            LegacyApiBasePath,
            surface == CognitiveMemoryApiSurface.Legacy
                ? "Legacy-compatible v1 contract. New integrations should prefer /api/cognitive-memory/v1."
                : "Stable v1 contract. Legacy /api/cognitive-memory routes remain available for compatibility.",
            ApiRouteTemplates
                .Select(route => route.ToRoute(basePath, EndpointName(route.LegacyEndpointName, surface)))
                .ToArray(),
            BuildExamples(basePath));
    }

    private static string ApiBasePath(CognitiveMemoryApiSurface surface)
        => surface == CognitiveMemoryApiSurface.V1
            ? V1ApiBasePath
            : LegacyApiBasePath;

    private static IReadOnlyList<CognitiveMemoryApiExampleContract> BuildExamples(string basePath)
        =>
        [
            new(
                "Check active database profile and contract",
                "GET",
                $"{basePath}/status",
                string.Empty,
                "Returns the active database profile, contract version, contract path, and route list."),
            new(
                "Run scoped recall",
                "POST",
                $"{basePath}/recall",
                """
                {
                  "projectId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
                  "query": "Which deployment context applies to this task?",
                  "intent": "SourceLookup",
                  "mode": "FocusedTaskContext",
                  "projectionCollectionName": "candoitall-knowledge",
                  "projectionProfileId": "qdrant-default-v1",
                  "embeddingProfileId": "local-hashing-v1:dimension=384",
                  "budget": {
                    "coarseCandidateLimit": 24,
                    "vectorResultLimit": 12,
                    "focusLimit": 8,
                    "contextCharacterBudget": 12000
                  }
                }
                """,
                "Returns a typed recall result with trace, selected records, source references, vector stage state, and budget decisions."),
            new(
                "Ingest external web link",
                "POST",
                $"{basePath}/external-sources/web-links",
                """
                {
                  "projectId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
                  "url": "https://example.com/runbook",
                  "actorId": "operator:docs"
                }
                """,
                "Creates an external source ingestion operation and returns source/evidence identifiers when ingestion succeeds."),
            new(
                "Rebuild stale projections",
                "POST",
                $"{basePath}/projections/rebuild",
                """
                {
                  "projectId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
                  "take": 50,
                  "actorId": "operator:projection-rebuild",
                  "collectionName": "candoitall-knowledge",
                  "projectMissingRecords": true,
                  "projectionProfileId": "qdrant-default-v1",
                  "embeddingProfileId": "local-hashing-v1:dimension=384",
                  "targetProviderName": "qdrant",
                  "projectionStoreKind": "Qdrant",
                  "vectorDimensions": 384
                }
                """,
                "Rebuilds stale projection records and can project missing durable memory records when provider options are supplied."),
            new(
                "Run explicit automation pass",
                "POST",
                $"{basePath}/automation/run",
                """
                {
                  "projectId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
                  "triggerKind": "Manual",
                  "actorId": "operator:automation"
                }
                """,
                "Runs configured ingestion/consolidation automation explicitly; no hidden scheduler is implied."),
            new(
                "Dry-run retention cleanup",
                "POST",
                $"{basePath}/retention/cleanup",
                """
                {
                  "projectId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
                  "deleteBeforeUtc": "2026-04-19T00:00:00Z",
                  "dryRun": true,
                  "scopes": [ "RecallTraces", "ConsolidationCandidates", "ProbeSessions", "DistributedJobs" ],
                  "actorId": "operator:retention"
                }
                """,
                "Returns matched cleanup counts without deleting records when dryRun is true."),
            new(
                "Approve review item",
                "POST",
                $"{basePath}/review-items/bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb/decisions",
                """
                {
                  "decisionKind": "Approve",
                  "actorId": "operator:review",
                  "expectedConcurrencyToken": "cccccccc-cccc-cccc-cccc-cccccccccccc"
                }
                """,
                "Applies a concurrency-checked review decision and materializes approved consolidation candidates when applicable.")
        ];

    private static readonly CognitiveMemoryApiRouteTemplate[] ApiRouteTemplates =
    [
        new("GET", "/contract", "GetCognitiveMemoryApiContract", "Contract", "Returns the versioned Cognitive Memory API contract and examples."),
        new("GET", "/status", "GetCognitiveMemoryStatus", "Database", "Returns active database profile and route status."),
        new("GET", "/database/selection", "GetCognitiveMemoryDatabaseSelection", "Database", "Returns active database profile details."),
        new("GET", "/database/profiles", "ListCognitiveMemoryDatabaseProfiles", "Database", "Lists configured database profiles."),
        new("POST", "/database/profiles/postgresql", "CreateCognitiveMemoryPostgreSqlDatabaseProfile", "Database", "Creates and optionally activates a PostgreSQL profile."),
        new("POST", "/database/switch/{profileId}", "SwitchCognitiveMemoryDatabaseProfile", "Database", "Switches the active database profile."),
        new("GET", "/settings", "GetCognitiveMemorySettings", "Settings", "Returns automation and model access settings."),
        new("PUT", "/settings", "UpdateCognitiveMemorySettings", "Settings", "Updates automation and model access settings."),
        new("POST", "/ingestion/project-structure", "IngestCognitiveMemoryProjectStructure", "Ingestion", "Ingests project-structure source snapshots."),
        new("POST", "/ingestion/processes", "IngestCognitiveMemoryProcesses", "Ingestion", "Ingests process-runtime source snapshots."),
        new("POST", "/external-sources/files", "IngestCognitiveMemoryExternalFile", "Ingestion", "Ingests an uploaded external source file."),
        new("POST", "/external-sources/web-links", "IngestCognitiveMemoryExternalWebLink", "Ingestion", "Ingests an external HTTP or HTTPS web link."),
        new("GET", "/external-sources/ingestions/{operationId}", "GetCognitiveMemoryExternalSourceIngestion", "Ingestion", "Returns an external source ingestion operation."),
        new("GET", "/snapshot", "GetCognitiveMemorySnapshot", "Review", "Returns the operator review UI snapshot."),
        new("POST", "/sources/ingest", "IngestCognitiveMemorySource", "Source", "Ingests typed internal source snapshots."),
        new("POST", "/consolidation/runs", "RunCognitiveMemoryConsolidation", "Consolidation", "Runs consolidation over ingested sources."),
        new("POST", "/recall", "RecallCognitiveMemoryContext", "Recall", "Builds a recall context pack for a query with optional vector projection settings."),
        new("POST", "/review-items/{reviewItemId}/decisions", "DecideCognitiveMemoryReviewItem", "Review", "Applies a concurrency-checked review decision."),
        new("POST", "/projections/rebuild", "RebuildCognitiveMemoryProjections", "Operations", "Rebuilds stale/failed projections and can project missing durable memory records."),
        new("POST", "/automation/run", "RunCognitiveMemoryScheduledAutomation", "Operations", "Runs configured automation explicitly."),
        new("POST", "/retention/cleanup", "RunCognitiveMemoryRetentionCleanup", "Operations", "Runs explicit retention cleanup with dry-run support."),
        new("POST", "/probes/sessions", "StartCognitiveMemoryProbeSession", "Probe", "Starts a probing session."),
        new("POST", "/probes/sessions/{sessionId}/turns", "AskCognitiveMemoryProbeQuestion", "Probe", "Adds a probing turn."),
        new("POST", "/probes/turns/{turnId}/feedback", "RecordCognitiveMemoryProbeFeedback", "Probe", "Records probe feedback."),
        new("POST", "/self-regulation/assessments", "AssessCognitiveMemorySelfRegulation", "SelfRegulation", "Runs self-regulation assessment."),
        new("POST", "/answer-gate/decisions", "DecideCognitiveMemoryAnswerGate", "AnswerGate", "Produces an answer-gate decision."),
        new("POST", "/professor-reviews", "RequestCognitiveMemoryProfessorReview", "ProfessorReview", "Requests professor review."),
        new("POST", "/professor-reviews/{reviewId}/complete", "CompleteCognitiveMemoryProfessorReview", "ProfessorReview", "Completes professor review."),
        new("POST", "/epistemic-drive/scans", "ScanCognitiveMemoryEpistemicDrive", "EpistemicDrive", "Scans for learning gaps."),
        new("POST", "/epistemic-drive/proposals/{proposalId}/decisions", "DecideCognitiveMemoryLearningProposal", "EpistemicDrive", "Applies a learning proposal decision."),
        new("POST", "/cross-project/promotions", "CreateCognitiveMemoryCrossProjectPromotion", "CrossProject", "Creates a cross-project promotion candidate."),
        new("POST", "/distributed/workers", "RegisterCognitiveMemoryDistributedWorker", "Distributed", "Registers a distributed worker."),
        new("POST", "/distributed/jobs", "EnqueueCognitiveMemoryDistributedJob", "Distributed", "Enqueues a distributed job."),
        new("POST", "/distributed/jobs/claim", "ClaimCognitiveMemoryDistributedJob", "Distributed", "Claims a distributed job lease."),
        new("POST", "/distributed/jobs/{jobId}/results", "SubmitCognitiveMemoryDistributedResult", "Distributed", "Submits distributed worker output.")
    ];

    private sealed record CognitiveMemoryApiRouteTemplate(
        string Method,
        string RelativePath,
        string LegacyEndpointName,
        string Area,
        string Summary)
    {
        public CognitiveMemoryApiRouteContract ToRoute(
            string basePath,
            string endpointName)
        {
            return new CognitiveMemoryApiRouteContract(
                Method,
                $"{basePath}{RelativePath}",
                endpointName,
                Area,
                Summary);
        }
    }
}

internal sealed record CognitiveMemoryApiContractResponse(
    string Version,
    string BasePath,
    string LegacyBasePath,
    string Compatibility,
    IReadOnlyList<CognitiveMemoryApiRouteContract> Routes,
    IReadOnlyList<CognitiveMemoryApiExampleContract> Examples);

internal sealed record CognitiveMemoryApiRouteContract(
    string Method,
    string Path,
    string EndpointName,
    string Area,
    string Summary);

internal sealed record CognitiveMemoryApiExampleContract(
    string Name,
    string Method,
    string Path,
    string RequestBodyJson,
    string ResponseNotes);
