using System.Globalization;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectStructureNodeEditor
{
    private static readonly HashSet<string> NonEditableFieldKeys =
    [
        "participantRef",
        "meetingRef",
        "recordingRef",
        "assigneeRef",
        "repositoryRef",
        "parentParticipantRef",
        "secretRef"
    ];

    public static bool SupportsEditingField(string key)
        => !string.IsNullOrWhiteSpace(key) && !NonEditableFieldKeys.Contains(key);

    public static IReadOnlyList<CanvasWorkbenchInputValue> BuildInputValues(ProjectStructureCreateLeafDefinition definition, ProjectStructureNode node)
    {
        var metadata = ProjectObjectMetadataSerializer.Parse(node.MetadataJson);
        return (definition.InputFields ?? [])
            .Where(field => SupportsEditingField(field.Key))
            .Select(field => new CanvasWorkbenchInputValue
            {
                Key = field.Key,
                Value = ResolveFieldValue(field.Key, node, metadata)
            })
            .ToList();
    }

    public static ProjectObjectEditRequest ComposeUpdate(
        ProjectStructureCreateLeafDefinition definition,
        ProjectStructureNode node,
        CanvasWorkbenchCreateActionRequest request)
    {
        var inputValues = (request.InputValues ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item.Key) && SupportsEditingField(item.Key))
            .GroupBy(item => item.Key.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last().Value?.Trim() ?? string.Empty, StringComparer.OrdinalIgnoreCase);
        var submittedKeys = inputValues.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var metadata = ProjectObjectMetadataSerializer.Parse(node.MetadataJson);
        var notes = ResolveNotes(definition, node, request, inputValues);
        var startUtc = ResolveDate(node.StartUtc, inputValues, submittedKeys, "startUtc");
        var endUtc = ResolveDate(node.EndUtc, inputValues, submittedKeys, "endUtc");

        switch (definition.ObjectType)
        {
            case ProjectObjectType.Meeting:
                metadata.Meeting ??= new ProjectMeetingMetadata();
                metadata.Meeting.Channel = ResolveEnum(inputValues, submittedKeys, "channel", metadata.Meeting.Channel);
                metadata.Meeting.RepeatCadence = ResolveEnum(inputValues, submittedKeys, "repeatCadence", metadata.Meeting.RepeatCadence);
                metadata.Meeting.Address = ResolveString(inputValues, submittedKeys, "address", metadata.Meeting.Address);
                metadata.Meeting.MeetingUrl = ResolveString(inputValues, submittedKeys, "meetingUrl", metadata.Meeting.MeetingUrl);
                metadata.Meeting.MapUrl = ResolveString(inputValues, submittedKeys, "mapUrl", metadata.Meeting.MapUrl);
                break;
            case ProjectObjectType.Recording:
                metadata.Recording ??= new ProjectRecordingMetadata();
                metadata.Recording.RecordingSource = ResolveString(inputValues, submittedKeys, "recordingSource", metadata.Recording.RecordingSource);
                metadata.Recording.StorageReference = ResolveString(inputValues, submittedKeys, "storageReference", metadata.Recording.StorageReference);
                metadata.Recording.DurationMinutes = ResolveInt(inputValues, submittedKeys, "durationMinutes", metadata.Recording.DurationMinutes);
                break;
            case ProjectObjectType.Transcript:
                metadata.Transcript ??= new ProjectTranscriptMetadata();
                metadata.Transcript.TranscriptText = notes;
                break;
            case ProjectObjectType.Participant:
                metadata.Participant ??= new ProjectParticipantMetadata();
                metadata.Participant.ParticipantKind = ResolveEnum(
                    inputValues,
                    submittedKeys,
                    "participantKind",
                    metadata.Participant.ParticipantKind == default ? MapParticipantKind(node.ObjectSubtype) : metadata.Participant.ParticipantKind);
                metadata.Participant.Role = ResolveString(inputValues, submittedKeys, "role", metadata.Participant.Role);
                metadata.Participant.Organization = ResolveString(inputValues, submittedKeys, "organization", metadata.Participant.Organization);
                metadata.Participant.Email = ResolveString(inputValues, submittedKeys, "email", metadata.Participant.Email);
                metadata.Participant.Phone = ResolveString(inputValues, submittedKeys, "phone", metadata.Participant.Phone);
                break;
            case ProjectObjectType.WorkItem:
                metadata.WorkItem ??= new ProjectWorkItemMetadata();
                metadata.WorkItem.WorkItemKind = ResolveEnum(
                    inputValues,
                    submittedKeys,
                    "workItemKind",
                    metadata.WorkItem.WorkItemKind == default ? MapWorkItemKind(node.ObjectSubtype) : metadata.WorkItem.WorkItemKind);
                metadata.WorkItem.SendKind = ResolveNullableEnum(inputValues, submittedKeys, "sendKind", metadata.WorkItem.SendKind);
                metadata.WorkItem.DeliveryChannel = ResolveEnum(inputValues, submittedKeys, "deliveryChannel", metadata.WorkItem.DeliveryChannel);
                metadata.WorkItem.Amount = ResolveNullableDecimal(inputValues, submittedKeys, "amount", metadata.WorkItem.Amount);
                metadata.WorkItem.CurrencyCode = ResolveString(inputValues, submittedKeys, "currencyCode", metadata.WorkItem.CurrencyCode);
                metadata.WorkItem.Description = notes;
                metadata.WorkItem.DueUtc = ResolveDate(metadata.WorkItem.DueUtc, inputValues, submittedKeys, "dueUtc");
                break;
            case ProjectObjectType.Repository:
                metadata.Repository ??= new ProjectRepositoryMetadata();
                metadata.Repository.RepositoryMode = ResolveEnum(
                    inputValues,
                    submittedKeys,
                    "repositoryMode",
                    metadata.Repository.RepositoryMode == default ? MapRepositoryMode(node.ObjectSubtype) : metadata.Repository.RepositoryMode);
                metadata.Repository.RepositoryUrl = ResolveString(inputValues, submittedKeys, "repositoryUrl", metadata.Repository.RepositoryUrl);
                metadata.Repository.LocalPath = ResolveString(inputValues, submittedKeys, "localPath", metadata.Repository.LocalPath);
                metadata.Repository.DefaultBranch = ResolveString(inputValues, submittedKeys, "defaultBranch", metadata.Repository.DefaultBranch);
                metadata.Repository.RelativePath = ResolveString(inputValues, submittedKeys, "relativePath", metadata.Repository.RelativePath);
                break;
            case ProjectObjectType.File:
                metadata.File ??= new ProjectFileMetadata();
                if (submittedKeys.Contains("mermaidText"))
                {
                    metadata.File.MermaidDiagramKind = ProjectObjectMetadataSerializer.DetectMermaidDiagramKind(notes);
                }
                break;
            case ProjectObjectType.Script:
                metadata.Script ??= new ProjectScriptMetadata();
                metadata.Script.ScriptKind = ResolveEnum(
                    inputValues,
                    submittedKeys,
                    "scriptKind",
                    metadata.Script.ScriptKind == default ? MapScriptKind(node.ObjectSubtype) : metadata.Script.ScriptKind);
                metadata.Script.ScriptPath = ResolveString(inputValues, submittedKeys, "scriptPath", metadata.Script.ScriptPath);
                metadata.Script.Command = ResolveString(inputValues, submittedKeys, "command", metadata.Script.Command);
                metadata.Script.Arguments = ResolveString(inputValues, submittedKeys, "arguments", metadata.Script.Arguments);
                metadata.Script.WorkingDirectory = ResolveString(inputValues, submittedKeys, "workingDirectory", metadata.Script.WorkingDirectory);
                break;
            case ProjectObjectType.Environment:
                metadata.Environment ??= new ProjectEnvironmentMetadata();
                metadata.Environment.EnvironmentKind = ResolveEnum(
                    inputValues,
                    submittedKeys,
                    "environmentKind",
                    metadata.Environment.EnvironmentKind == default ? MapEnvironmentKind(node.ObjectSubtype) : metadata.Environment.EnvironmentKind);
                metadata.Environment.PythonProvider = ResolveNullableEnum(inputValues, submittedKeys, "pythonProvider", metadata.Environment.PythonProvider);
                metadata.Environment.EnvironmentName = ResolveString(inputValues, submittedKeys, "environmentName", metadata.Environment.EnvironmentName);
                metadata.Environment.ProjectPath = ResolveString(inputValues, submittedKeys, "projectPath", metadata.Environment.ProjectPath);
                metadata.Environment.LaunchProfileName = ResolveString(inputValues, submittedKeys, "launchProfileName", metadata.Environment.LaunchProfileName);
                metadata.Environment.RuntimeProtocol = ResolveEnum(inputValues, submittedKeys, "runtimeProtocol", metadata.Environment.RuntimeProtocol);
                metadata.Environment.LocalhostUrl = ResolveString(inputValues, submittedKeys, "localhostUrl", metadata.Environment.LocalhostUrl);
                break;
            case ProjectObjectType.Infrastructure:
                metadata.Infrastructure ??= new ProjectInfrastructureMetadata();
                metadata.Infrastructure.InfrastructureKind = ResolveEnum(
                    inputValues,
                    submittedKeys,
                    "infrastructureKind",
                    metadata.Infrastructure.InfrastructureKind == default ? MapInfrastructureKind(node.ObjectSubtype) : metadata.Infrastructure.InfrastructureKind);
                metadata.Infrastructure.Host = ResolveString(inputValues, submittedKeys, "host", metadata.Infrastructure.Host);
                metadata.Infrastructure.Port = ResolveNullableInt(inputValues, submittedKeys, "port", metadata.Infrastructure.Port);
                metadata.Infrastructure.ProviderName = ResolveString(inputValues, submittedKeys, "providerName", metadata.Infrastructure.ProviderName);
                metadata.Infrastructure.ProviderUrl = ResolveString(inputValues, submittedKeys, "providerUrl", metadata.Infrastructure.ProviderUrl);
                metadata.Infrastructure.LoginUrl = ResolveString(inputValues, submittedKeys, "loginUrl", metadata.Infrastructure.LoginUrl);
                metadata.Infrastructure.AccountName = ResolveString(inputValues, submittedKeys, "accountName", metadata.Infrastructure.AccountName);
                metadata.Infrastructure.CpuCores = ResolveNullableDecimal(inputValues, submittedKeys, "cpuCores", metadata.Infrastructure.CpuCores);
                metadata.Infrastructure.MemoryGb = ResolveNullableDecimal(inputValues, submittedKeys, "memoryGb", metadata.Infrastructure.MemoryGb);
                metadata.Infrastructure.StorageGb = ResolveNullableDecimal(inputValues, submittedKeys, "storageGb", metadata.Infrastructure.StorageGb);
                metadata.Infrastructure.MonthlyPrice = ResolveNullableDecimal(inputValues, submittedKeys, "monthlyPrice", metadata.Infrastructure.MonthlyPrice);
                metadata.Infrastructure.DomainName = ResolveString(inputValues, submittedKeys, "domainName", metadata.Infrastructure.DomainName);
                metadata.Infrastructure.OwnerName = ResolveString(inputValues, submittedKeys, "ownerName", metadata.Infrastructure.OwnerName);
                metadata.Infrastructure.DnsRecordType = ResolveString(inputValues, submittedKeys, "dnsRecordType", metadata.Infrastructure.DnsRecordType);
                metadata.Infrastructure.DnsRecordValue = ResolveString(inputValues, submittedKeys, "dnsRecordValue", metadata.Infrastructure.DnsRecordValue);
                metadata.Infrastructure.DockerMode = ResolveString(inputValues, submittedKeys, "dockerMode", metadata.Infrastructure.DockerMode);
                metadata.Infrastructure.ProxyProvider = ResolveString(inputValues, submittedKeys, "proxyProvider", metadata.Infrastructure.ProxyProvider);
                metadata.Infrastructure.DatabaseType = ResolveString(inputValues, submittedKeys, "databaseType", metadata.Infrastructure.DatabaseType);
                metadata.Infrastructure.ConnectionReference = ResolveString(inputValues, submittedKeys, "connectionReference", metadata.Infrastructure.ConnectionReference);
                metadata.Infrastructure.FolderPath = ResolveString(inputValues, submittedKeys, "folderPath", metadata.Infrastructure.FolderPath);
                metadata.Infrastructure.AiReferenceKind = ResolveNullableEnum(inputValues, submittedKeys, "aiReferenceKind", metadata.Infrastructure.AiReferenceKind);
                metadata.Infrastructure.AiReferenceUrl = ResolveString(inputValues, submittedKeys, "aiReferenceUrl", metadata.Infrastructure.AiReferenceUrl);
                break;
            case ProjectObjectType.Link:
                metadata.Link ??= new ProjectLinkMetadata();
                metadata.Link.Url = request.Subtitle?.Trim() ?? string.Empty;
                metadata.Link.DisplayHint = request.Title?.Trim() ?? string.Empty;
                break;
        }

        return new ProjectObjectEditRequest(
            request.Title?.Trim() ?? string.Empty,
            request.Subtitle?.Trim() ?? string.Empty,
            notes,
            startUtc,
            endUtc,
            ProjectObjectMetadataSerializer.Serialize(metadata));
    }

    private static string ResolveFieldValue(string key, ProjectStructureNode node, ProjectObjectMetadataEnvelope metadata)
        => key switch
        {
            "startUtc" => FormatDateTimeLocal(node.StartUtc),
            "endUtc" => FormatDateTimeLocal(node.EndUtc),
            "channel" => ToCamelCaseToken(metadata.Meeting?.Channel),
            "repeatCadence" => ToCamelCaseToken(metadata.Meeting?.RepeatCadence),
            "meetingUrl" => metadata.Meeting?.MeetingUrl ?? string.Empty,
            "address" => metadata.Meeting?.Address ?? string.Empty,
            "mapUrl" => metadata.Meeting?.MapUrl ?? string.Empty,
            "recordingSource" => metadata.Recording?.RecordingSource ?? string.Empty,
            "storageReference" => metadata.Recording?.StorageReference ?? string.Empty,
            "durationMinutes" => metadata.Recording?.DurationMinutes > 0 ? metadata.Recording.DurationMinutes.ToString(CultureInfo.InvariantCulture) : string.Empty,
            "transcriptText" => metadata.Transcript?.TranscriptText ?? node.Notes,
            "participantKind" => ToCamelCaseToken(metadata.Participant?.ParticipantKind == default ? MapParticipantKind(node.ObjectSubtype) : metadata.Participant?.ParticipantKind),
            "role" => metadata.Participant?.Role ?? string.Empty,
            "organization" => metadata.Participant?.Organization ?? string.Empty,
            "email" => metadata.Participant?.Email ?? string.Empty,
            "phone" => metadata.Participant?.Phone ?? string.Empty,
            "workItemKind" => ToCamelCaseToken(metadata.WorkItem?.WorkItemKind == default ? MapWorkItemKind(node.ObjectSubtype) : metadata.WorkItem?.WorkItemKind),
            "dueUtc" => FormatDateTimeLocal(metadata.WorkItem?.DueUtc),
            "sendKind" => ToCamelCaseToken(metadata.WorkItem?.SendKind),
            "deliveryChannel" => ToCamelCaseToken(metadata.WorkItem?.DeliveryChannel),
            "amount" => metadata.WorkItem?.Amount?.ToString("0.##", CultureInfo.InvariantCulture) ?? string.Empty,
            "currencyCode" => metadata.WorkItem?.CurrencyCode ?? string.Empty,
            "repositoryMode" => ToCamelCaseToken(metadata.Repository?.RepositoryMode == default ? MapRepositoryMode(node.ObjectSubtype) : metadata.Repository?.RepositoryMode),
            "repositoryUrl" => metadata.Repository?.RepositoryUrl ?? string.Empty,
            "localPath" => metadata.Repository?.LocalPath ?? string.Empty,
            "defaultBranch" => metadata.Repository?.DefaultBranch ?? string.Empty,
            "relativePath" => metadata.Repository?.RelativePath ?? string.Empty,
            "mermaidText" => node.Notes,
            "scriptKind" => ToCamelCaseToken(metadata.Script?.ScriptKind == default ? MapScriptKind(node.ObjectSubtype) : metadata.Script?.ScriptKind),
            "scriptPath" => metadata.Script?.ScriptPath ?? string.Empty,
            "command" => metadata.Script?.Command ?? string.Empty,
            "arguments" => metadata.Script?.Arguments ?? string.Empty,
            "workingDirectory" => metadata.Script?.WorkingDirectory ?? string.Empty,
            "environmentKind" => ToCamelCaseToken(metadata.Environment?.EnvironmentKind == default ? MapEnvironmentKind(node.ObjectSubtype) : metadata.Environment?.EnvironmentKind),
            "pythonProvider" => ToCamelCaseToken(metadata.Environment?.PythonProvider),
            "environmentName" => metadata.Environment?.EnvironmentName ?? string.Empty,
            "projectPath" => metadata.Environment?.ProjectPath ?? string.Empty,
            "launchProfileName" => metadata.Environment?.LaunchProfileName ?? string.Empty,
            "runtimeProtocol" => ToCamelCaseToken(metadata.Environment?.RuntimeProtocol),
            "localhostUrl" => metadata.Environment?.LocalhostUrl ?? string.Empty,
            "infrastructureKind" => ToCamelCaseToken(metadata.Infrastructure?.InfrastructureKind == default ? MapInfrastructureKind(node.ObjectSubtype) : metadata.Infrastructure?.InfrastructureKind),
            "host" => metadata.Infrastructure?.Host ?? string.Empty,
            "port" => metadata.Infrastructure?.Port?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            "providerName" => metadata.Infrastructure?.ProviderName ?? string.Empty,
            "providerUrl" => metadata.Infrastructure?.ProviderUrl ?? string.Empty,
            "loginUrl" => metadata.Infrastructure?.LoginUrl ?? string.Empty,
            "accountName" => metadata.Infrastructure?.AccountName ?? string.Empty,
            "cpuCores" => metadata.Infrastructure?.CpuCores?.ToString("0.##", CultureInfo.InvariantCulture) ?? string.Empty,
            "memoryGb" => metadata.Infrastructure?.MemoryGb?.ToString("0.##", CultureInfo.InvariantCulture) ?? string.Empty,
            "storageGb" => metadata.Infrastructure?.StorageGb?.ToString("0.##", CultureInfo.InvariantCulture) ?? string.Empty,
            "monthlyPrice" => metadata.Infrastructure?.MonthlyPrice?.ToString("0.##", CultureInfo.InvariantCulture) ?? string.Empty,
            "domainName" => metadata.Infrastructure?.DomainName ?? string.Empty,
            "ownerName" => metadata.Infrastructure?.OwnerName ?? string.Empty,
            "dnsRecordType" => metadata.Infrastructure?.DnsRecordType ?? string.Empty,
            "dnsRecordValue" => metadata.Infrastructure?.DnsRecordValue ?? string.Empty,
            "dockerMode" => metadata.Infrastructure?.DockerMode ?? string.Empty,
            "proxyProvider" => metadata.Infrastructure?.ProxyProvider ?? string.Empty,
            "databaseType" => metadata.Infrastructure?.DatabaseType ?? string.Empty,
            "connectionReference" => metadata.Infrastructure?.ConnectionReference ?? string.Empty,
            "folderPath" => metadata.Infrastructure?.FolderPath ?? string.Empty,
            "aiReferenceKind" => ToCamelCaseToken(metadata.Infrastructure?.AiReferenceKind),
            "aiReferenceUrl" => metadata.Infrastructure?.AiReferenceUrl ?? string.Empty,
            _ => string.Empty
        };

    private static string ResolveNotes(
        ProjectStructureCreateLeafDefinition definition,
        ProjectStructureNode node,
        CanvasWorkbenchCreateActionRequest request,
        IReadOnlyDictionary<string, string> inputValues)
    {
        if (inputValues.TryGetValue("transcriptText", out var transcriptText))
        {
            return transcriptText;
        }

        if (inputValues.TryGetValue("mermaidText", out var mermaidText))
        {
            return mermaidText;
        }

        return definition.ShowDefaultTextFields
            ? request.Notes?.Trim() ?? string.Empty
            : node.Notes;
    }

    private static string ResolveString(
        IReadOnlyDictionary<string, string> inputValues,
        IReadOnlySet<string> submittedKeys,
        string key,
        string currentValue)
        => submittedKeys.Contains(key) && inputValues.TryGetValue(key, out var value) ? value : currentValue;

    private static int ResolveInt(
        IReadOnlyDictionary<string, string> inputValues,
        IReadOnlySet<string> submittedKeys,
        string key,
        int currentValue)
        => !submittedKeys.Contains(key)
            ? currentValue
            : inputValues.TryGetValue(key, out var value) && int.TryParse(value, out var parsed)
                ? parsed
                : 0;

    private static int? ResolveNullableInt(
        IReadOnlyDictionary<string, string> inputValues,
        IReadOnlySet<string> submittedKeys,
        string key,
        int? currentValue)
        => !submittedKeys.Contains(key)
            ? currentValue
            : inputValues.TryGetValue(key, out var value) && int.TryParse(value, out var parsed)
                ? parsed
                : null;

    private static decimal? ResolveNullableDecimal(
        IReadOnlyDictionary<string, string> inputValues,
        IReadOnlySet<string> submittedKeys,
        string key,
        decimal? currentValue)
        => !submittedKeys.Contains(key)
            ? currentValue
            : inputValues.TryGetValue(key, out var value) && decimal.TryParse(value, out var parsed)
                ? parsed
                : null;

    private static DateTimeOffset? ResolveDate(
        DateTimeOffset? currentValue,
        IReadOnlyDictionary<string, string> inputValues,
        IReadOnlySet<string> submittedKeys,
        string key)
        => !submittedKeys.Contains(key)
            ? currentValue
            : inputValues.TryGetValue(key, out var value) && DateTimeOffset.TryParse(value, out var parsed)
                ? parsed
                : null;

    private static TEnum ResolveEnum<TEnum>(
        IReadOnlyDictionary<string, string> inputValues,
        IReadOnlySet<string> submittedKeys,
        string key,
        TEnum currentValue)
        where TEnum : struct, Enum
        => !submittedKeys.Contains(key)
            ? currentValue
            : inputValues.TryGetValue(key, out var value) && Enum.TryParse<TEnum>(value, true, out var parsed)
                ? parsed
                : currentValue;

    private static TEnum? ResolveNullableEnum<TEnum>(
        IReadOnlyDictionary<string, string> inputValues,
        IReadOnlySet<string> submittedKeys,
        string key,
        TEnum? currentValue)
        where TEnum : struct, Enum
        => !submittedKeys.Contains(key)
            ? currentValue
            : inputValues.TryGetValue(key, out var value) && Enum.TryParse<TEnum>(value, true, out var parsed)
                ? parsed
                : null;

    private static string FormatDateTimeLocal(DateTimeOffset? value)
        => value?.ToLocalTime().ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture) ?? string.Empty;

    private static string ToCamelCaseToken<TEnum>(TEnum? value)
        where TEnum : struct, Enum
    {
        if (!value.HasValue)
        {
            return string.Empty;
        }

        var token = value.Value.ToString();
        return string.IsNullOrWhiteSpace(token)
            ? string.Empty
            : char.ToLowerInvariant(token[0]) + token[1..];
    }

    private static ProjectParticipantKind MapParticipantKind(string subtype) => subtype switch
    {
        "team-block" => ProjectParticipantKind.TeamBlock,
        "team-section" => ProjectParticipantKind.TeamSection,
        "freelancer" => ProjectParticipantKind.Freelancer,
        "partner" => ProjectParticipantKind.Partner,
        "ai-agent" => ProjectParticipantKind.AiAgent,
        _ => ProjectParticipantKind.Hr
    };

    private static ProjectWorkItemKind MapWorkItemKind(string subtype) => subtype switch
    {
        "issue" => ProjectWorkItemKind.Issue,
        "revision" => ProjectWorkItemKind.Revision,
        "feedback" => ProjectWorkItemKind.Feedback,
        "payment" => ProjectWorkItemKind.Payment,
        "send" => ProjectWorkItemKind.Send,
        _ => ProjectWorkItemKind.Task
    };

    private static ProjectRepositoryMode MapRepositoryMode(string subtype) => subtype switch
    {
        "remote" => ProjectRepositoryMode.RemoteGitHub,
        "folder" => ProjectRepositoryMode.LocalFolder,
        _ => ProjectRepositoryMode.LocalRepository
    };

    private static ProjectScriptKind MapScriptKind(string subtype) => subtype switch
    {
        "powershell" => ProjectScriptKind.PowerShell,
        "ef-migration" => ProjectScriptKind.EfMigration,
        "tailwind-watch" => ProjectScriptKind.TailwindWatch,
        _ => ProjectScriptKind.Console
    };

    private static ProjectEnvironmentKind MapEnvironmentKind(string subtype) => subtype switch
    {
        "python" => ProjectEnvironmentKind.PythonEnvironment,
        "dotnet-watch" => ProjectEnvironmentKind.DotNetWatch,
        "dotnet-release" => ProjectEnvironmentKind.DotNetRelease,
        _ => ProjectEnvironmentKind.DotNetRuntime
    };

    private static ProjectInfrastructureKind MapInfrastructureKind(string subtype) => subtype switch
    {
        "domain" => ProjectInfrastructureKind.Domain,
        "dns-record" => ProjectInfrastructureKind.DnsRecord,
        "docker-mode" => ProjectInfrastructureKind.DockerMode,
        "database" => ProjectInfrastructureKind.Database,
        "deployment-folder" => ProjectInfrastructureKind.DeploymentFolder,
        "key-reference" => ProjectInfrastructureKind.KeyReference,
        "ai-link" => ProjectInfrastructureKind.AiLink,
        _ => ProjectInfrastructureKind.RemoteServer
    };
}
