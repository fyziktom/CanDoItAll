using CanDoItAll.Components.CanvasLib;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

internal sealed record ProjectStructurePendingLink(string TargetNodeId, ProjectObjectLinkKind LinkKind);

internal sealed record ProjectStructurePreparedCreateRequest(
    ProjectObjectCreateRequest Request,
    IReadOnlyList<ProjectStructurePendingLink> PendingLinks);

internal static class ProjectStructureCreateRequestComposer
{
    public static ProjectStructurePreparedCreateRequest Compose(
        ProjectStructureCreateLeafDefinition definition,
        CanvasWorkbenchCreateActionRequest request,
        string? parentNodeId,
        (double? X, double? Y) placement)
    {
        var subtype = string.IsNullOrWhiteSpace(request.ObjectSubtype) ? definition.ObjectSubtype : request.ObjectSubtype;
        var inputValues = (request.InputValues ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item.Key))
            .GroupBy(item => item.Key.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last().Value?.Trim() ?? string.Empty, StringComparer.OrdinalIgnoreCase);
        var metadata = new ProjectObjectMetadataEnvelope();
        var nodeReferences = new ProjectNodeReferenceCollection();
        var pendingLinks = new List<ProjectStructurePendingLink>();
        var notes = ResolveNotes(definition, request, inputValues);
        var startUtc = ParseDateTimeOffset(inputValues, "startUtc");
        var endUtc = ParseDateTimeOffset(inputValues, "endUtc");

        switch (definition.ObjectType)
        {
            case ProjectObjectType.Meeting:
                metadata.Meeting = new ProjectMeetingMetadata
                {
                    Channel = ParseEnum(inputValues, "channel", ProjectMeetingChannel.Unknown),
                    RepeatCadence = ParseEnum(inputValues, "repeatCadence", ProjectRepeatCadence.None),
                    Address = GetValue(inputValues, "address"),
                    MeetingUrl = GetValue(inputValues, "meetingUrl"),
                    MapUrl = GetValue(inputValues, "mapUrl")
                };
                AddLinkIfPresent(pendingLinks, inputValues, "participantRef", ProjectObjectLinkKind.Uses);
                break;
            case ProjectObjectType.Recording:
                metadata.Recording = new ProjectRecordingMetadata
                {
                    RecordingSource = GetValue(inputValues, "recordingSource"),
                    StorageReference = GetValue(inputValues, "storageReference"),
                    DurationMinutes = ParseInt(inputValues, "durationMinutes")
                };
                AddLinkIfPresent(pendingLinks, inputValues, "meetingRef", ProjectObjectLinkKind.BelongsTo);
                break;
            case ProjectObjectType.Transcript:
                metadata.Transcript = new ProjectTranscriptMetadata
                {
                    TranscriptText = GetValue(inputValues, "transcriptText"),
                    SummaryText = string.Empty
                };
                notes = string.IsNullOrWhiteSpace(metadata.Transcript.TranscriptText) ? notes : metadata.Transcript.TranscriptText;
                AddLinkIfPresent(pendingLinks, inputValues, "recordingRef", ProjectObjectLinkKind.DerivedFrom);
                break;
            case ProjectObjectType.Participant:
                metadata.Participant = new ProjectParticipantMetadata
                {
                    ParticipantKind = ParseEnum(inputValues, "participantKind", ProjectNodeKindRegistry.ResolveParticipantKind(subtype)),
                    Role = GetValue(inputValues, "role"),
                    Organization = GetValue(inputValues, "organization"),
                    Email = GetValue(inputValues, "email"),
                    Phone = GetValue(inputValues, "phone")
                };
                nodeReferences.ParticipantParentNodeId = ParseNodeGuid(inputValues, "parentParticipantRef");
                AddLinkIfPresent(pendingLinks, inputValues, "parentParticipantRef", ProjectObjectLinkKind.BelongsTo);
                break;
            case ProjectObjectType.WorkItem:
                metadata.WorkItem = new ProjectWorkItemMetadata
                {
                    WorkItemKind = ParseEnum(inputValues, "workItemKind", ProjectNodeKindRegistry.ResolveWorkItemKind(subtype)),
                    SendKind = TryParseNullableEnum<ProjectSendKind>(inputValues, "sendKind"),
                    DeliveryChannel = ParseEnum(inputValues, "deliveryChannel", ProjectMessageChannel.None),
                    Amount = ParseDecimalNullable(inputValues, "amount"),
                    CurrencyCode = GetValue(inputValues, "currencyCode"),
                    Description = notes,
                    DueUtc = ParseDateTimeOffset(inputValues, "dueUtc")
                };
                nodeReferences.WorkItemAssigneeNodeId = ParseNodeGuid(inputValues, "assigneeRef");
                nodeReferences.WorkItemRepositoryResourceId = ParseNodeGuid(inputValues, "repositoryRef");
                AddLinkIfPresent(pendingLinks, inputValues, "assigneeRef", ProjectObjectLinkKind.Uses);
                AddLinkIfPresent(pendingLinks, inputValues, "repositoryRef", ProjectObjectLinkKind.Uses);
                break;
            case ProjectObjectType.Repository:
                metadata.Repository = new ProjectRepositoryMetadata
                {
                    RepositoryMode = ParseEnum(inputValues, "repositoryMode", ProjectNodeKindRegistry.ResolveRepositoryMode(subtype)),
                    RepositoryUrl = GetValue(inputValues, "repositoryUrl"),
                    LocalPath = GetValue(inputValues, "localPath"),
                    DefaultBranch = GetValue(inputValues, "defaultBranch"),
                    RelativePath = GetValue(inputValues, "relativePath")
                };
                nodeReferences.RepositoryResourceId = ParseNodeGuid(inputValues, "resourceRef");
                break;
            case ProjectObjectType.File:
                metadata.File = new ProjectFileMetadata
                {
                    FileSubtype = ParseEnum(inputValues, "fileSubtype", ProjectNodeKindRegistry.ResolveFileSubtype(ProjectObjectType.File, subtype)),
                    MermaidDiagramKind = string.Equals(subtype, "mermaid", StringComparison.OrdinalIgnoreCase)
                        ? ProjectObjectMetadataSerializer.DetectMermaidDiagramKind(notes)
                        : MermaidDiagramKind.Unknown,
                    IsClipboardCapture = string.Equals(subtype, "screenshot", StringComparison.OrdinalIgnoreCase),
                    SourceHint = GetValue(inputValues, "sourceHint"),
                    ExternalPath = GetValue(inputValues, "externalPath")
                };
                break;
            case ProjectObjectType.Script:
                metadata.Script = new ProjectScriptMetadata
                {
                    ScriptKind = ParseEnum(inputValues, "scriptKind", ProjectNodeKindRegistry.ResolveScriptKind(subtype)),
                    ScriptPath = GetValue(inputValues, "scriptPath"),
                    Command = GetValue(inputValues, "command"),
                    Arguments = GetValue(inputValues, "arguments"),
                    WorkingDirectory = GetValue(inputValues, "workingDirectory"),
                    TerminalRoute = GetValue(inputValues, "terminalRoute")
                };
                break;
            case ProjectObjectType.Environment:
                metadata.Environment = new ProjectEnvironmentMetadata
                {
                    EnvironmentKind = ParseEnum(inputValues, "environmentKind", ProjectNodeKindRegistry.ResolveEnvironmentKind(subtype)),
                    PythonProvider = TryParseNullableEnum<ProjectPythonProvider>(inputValues, "pythonProvider"),
                    EnvironmentName = GetValue(inputValues, "environmentName"),
                    ProjectPath = GetValue(inputValues, "projectPath"),
                    LaunchProfileName = GetValue(inputValues, "launchProfileName"),
                    RuntimeProtocol = ParseEnum(inputValues, "runtimeProtocol", ProjectRuntimeProtocol.Https),
                    LocalhostUrl = GetValue(inputValues, "localhostUrl")
                };
                nodeReferences.EnvironmentRepositoryResourceId = ParseNodeGuid(inputValues, "repositoryRef");
                AddLinkIfPresent(pendingLinks, inputValues, "repositoryRef", ProjectObjectLinkKind.Uses);
                break;
            case ProjectObjectType.Infrastructure:
                metadata.Infrastructure = new ProjectInfrastructureMetadata
                {
                    InfrastructureKind = ParseEnum(inputValues, "infrastructureKind", ProjectNodeKindRegistry.ResolveInfrastructureKind(subtype)),
                    Host = GetValue(inputValues, "host"),
                    Port = ParseNullableInt(inputValues, "port"),
                    ProviderName = GetValue(inputValues, "providerName"),
                    ProviderUrl = GetValue(inputValues, "providerUrl"),
                    LoginUrl = GetValue(inputValues, "loginUrl"),
                    AccountName = GetValue(inputValues, "accountName"),
                    CpuCores = ParseNullableDecimal(inputValues, "cpuCores"),
                    MemoryGb = ParseNullableDecimal(inputValues, "memoryGb"),
                    StorageGb = ParseNullableDecimal(inputValues, "storageGb"),
                    MonthlyPrice = ParseNullableDecimal(inputValues, "monthlyPrice"),
                    DomainName = GetValue(inputValues, "domainName"),
                    OwnerName = GetValue(inputValues, "ownerName"),
                    DnsRecordType = GetValue(inputValues, "dnsRecordType"),
                    DnsRecordValue = GetValue(inputValues, "dnsRecordValue"),
                    DockerMode = GetValue(inputValues, "dockerMode"),
                    ProxyProvider = GetValue(inputValues, "proxyProvider"),
                    DatabaseType = GetValue(inputValues, "databaseType"),
                    ConnectionReference = GetValue(inputValues, "connectionReference"),
                    FolderPath = GetValue(inputValues, "folderPath"),
                    StoragePurpose = GetValue(inputValues, "storagePurpose"),
                    StoragePathPrefix = GetValue(inputValues, "storagePathPrefix"),
                    AiReferenceKind = TryParseNullableEnum<ProjectAiReferenceKind>(inputValues, "aiReferenceKind"),
                    AiReferenceUrl = GetValue(inputValues, "aiReferenceUrl")
                };
                nodeReferences.InfrastructureStorageCatalogId = ParseGuid(inputValues, "storageCatalogId");
                nodeReferences.InfrastructureSecretReferenceId = ParseNodeGuid(inputValues, "secretRef");
                AddLinkIfPresent(pendingLinks, inputValues, "secretRef", ProjectObjectLinkKind.Uses);
                break;
            case ProjectObjectType.Link:
                metadata.Link = new ProjectLinkMetadata
                {
                    Url = request.Subtitle?.Trim() ?? string.Empty,
                    Channel = ParseEnum(inputValues, "deliveryChannel", ProjectMessageChannel.None),
                    DisplayHint = request.Title?.Trim() ?? string.Empty
                };
                break;
        }

        var metadataJson = ProjectObjectMetadataSerializer.Serialize(metadata);
        return new ProjectStructurePreparedCreateRequest(
            new ProjectObjectCreateRequest(
                definition.ObjectType,
                request.Title?.Trim() ?? string.Empty,
                request.Subtitle?.Trim() ?? string.Empty,
                notes,
                parentNodeId,
                placement.X,
                placement.Y,
                startUtc,
                endUtc,
                subtype,
                request.UploadedFile is null
                    ? null
                    : new ProjectObjectMediaPayload(request.UploadedFile.FileName, request.UploadedFile.ContentType, request.UploadedFile.Base64Data),
                metadataJson,
                null,
                nodeReferences.IsEmpty ? null : nodeReferences),
            pendingLinks);
    }

    private static string ResolveNotes(ProjectStructureCreateLeafDefinition definition, CanvasWorkbenchCreateActionRequest request, IReadOnlyDictionary<string, string> inputValues)
    {
        var overrideKeys = new[] { "transcriptText", "mermaidText" };
        foreach (var key in overrideKeys)
        {
            if (inputValues.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return !string.IsNullOrWhiteSpace(request.Notes)
            ? request.Notes.Trim()
            : definition.ObjectType == ProjectObjectType.Note && !string.IsNullOrWhiteSpace(request.Title)
                ? request.Title.Trim()
                : string.Empty;
    }

    private static string GetValue(IReadOnlyDictionary<string, string> inputValues, string key)
        => inputValues.TryGetValue(key, out var value) ? value : string.Empty;

    private static void AddLinkIfPresent(List<ProjectStructurePendingLink> pendingLinks, IReadOnlyDictionary<string, string> inputValues, string key, ProjectObjectLinkKind linkKind)
    {
        if (inputValues.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            pendingLinks.Add(new ProjectStructurePendingLink(value.Trim(), linkKind));
        }
    }

    private static DateTimeOffset? ParseDateTimeOffset(IReadOnlyDictionary<string, string> inputValues, string key)
        => inputValues.TryGetValue(key, out var value) && DateTimeOffset.TryParse(value, out var parsed)
            ? parsed
            : null;

    private static int ParseInt(IReadOnlyDictionary<string, string> inputValues, string key)
        => ParseNullableInt(inputValues, key) ?? 0;

    private static int? ParseNullableInt(IReadOnlyDictionary<string, string> inputValues, string key)
        => inputValues.TryGetValue(key, out var value) && int.TryParse(value, out var parsed) ? parsed : null;

    private static decimal? ParseDecimalNullable(IReadOnlyDictionary<string, string> inputValues, string key)
        => ParseNullableDecimal(inputValues, key);

    private static decimal? ParseNullableDecimal(IReadOnlyDictionary<string, string> inputValues, string key)
        => inputValues.TryGetValue(key, out var value) && decimal.TryParse(value, out var parsed) ? parsed : null;

    private static Guid? ParseNodeGuid(IReadOnlyDictionary<string, string> inputValues, string key)
        => inputValues.TryGetValue(key, out var value) && value.StartsWith("custom:", StringComparison.OrdinalIgnoreCase) && Guid.TryParse(value["custom:".Length..], out var parsed)
            ? parsed
            : null;

    private static Guid? ParseGuid(IReadOnlyDictionary<string, string> inputValues, string key)
        => inputValues.TryGetValue(key, out var value) && Guid.TryParse(value, out var parsed) ? parsed : null;

    private static TEnum ParseEnum<TEnum>(IReadOnlyDictionary<string, string> inputValues, string key, TEnum fallback)
        where TEnum : struct, Enum
        => inputValues.TryGetValue(key, out var value) && Enum.TryParse<TEnum>(value, true, out var parsed) ? parsed : fallback;

    private static TEnum? TryParseNullableEnum<TEnum>(IReadOnlyDictionary<string, string> inputValues, string key)
        where TEnum : struct, Enum
        => inputValues.TryGetValue(key, out var value) && Enum.TryParse<TEnum>(value, true, out var parsed) ? parsed : null;

}
