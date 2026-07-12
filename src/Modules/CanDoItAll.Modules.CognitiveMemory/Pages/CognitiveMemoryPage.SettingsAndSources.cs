using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Voice;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.Common;
using CanDoItAll.Memory.SourceGateway;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;
using System.Text;

namespace CanDoItAll.Modules.CognitiveMemory.Pages;

public partial class CognitiveMemoryPage
{
    internal async Task LoadAutomationSettingsAsync()
    {
        try
        {
            var settings = await AutomationSettingsService.GetAsync(CancellationToken.None);
            isEnabled = settings.IsEnabled;
            automationScheduleMode = settings.ScheduleMode;
            nightlyLocalTime = settings.NightlyLocalTime;
            idleMinutes = settings.IdleMinutes;
            scheduledLocalTimesText = string.Join(Environment.NewLine, settings.ScheduledLocalTimes);
            autoIngestProjectStructure = settings.AutoIngestProjectStructure;
            autoIngestProcessRuntime = settings.AutoIngestProcessRuntime;
            autoConsolidateAfterIngestion = settings.AutoConsolidateAfterIngestion;
            modelAccessMode = settings.ModelAccessMode;
            defaultProviderProfileIdText = settings.DefaultProviderProfileId?.ToString("D") ?? string.Empty;
            defaultAgentIdText = settings.DefaultAgentId?.ToString("D") ?? string.Empty;

            var referenceData = await AgentReferenceDataProvider.GetAsync(
                AgentReferenceDataRequest.AgentsAndProviders(),
                CancellationToken.None);
            var allowedProviderIds = settings.AllowedProviderProfileIds.ToHashSet();
            modelProviderOptions = referenceData.Providers
                .Where(provider => provider.Purpose == ProviderProfilePurpose.Chat)
                .OrderByDescending(provider => provider.IsEnabled)
                .ThenBy(provider => provider.Kind)
                .ThenBy(provider => provider.Name, StringComparer.OrdinalIgnoreCase)
                .Select(provider => new CognitiveMemoryProviderSelection(
                    provider.Id,
                    provider.Name,
                    provider.Kind,
                    provider.DefaultModel,
                    provider.BaseUrl,
                    provider.IsEnabled,
                    CognitiveMemoryModelAccessPolicy.IsLocalProvider(provider),
                    allowedProviderIds.Contains(provider.Id)))
                .ToList();
            modelAgentOptions = referenceData.Agents
                .OrderBy(agent => agent.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            modelAccessStatus = BuildModelAccessStatus(settings);
        }
        catch (Exception exception)
        {
            errorMessage = exception.Message;
            NotificationService.Error("Memory settings failed", exception.Message);
        }
    }


    internal async Task SaveAutomationSettingsAsync()
    {
        if (isBusy)
        {
            return;
        }

        isBusy = true;
        errorMessage = string.Empty;

        try
        {
            var settings = await AutomationSettingsService.SaveAsync(new CognitiveMemoryAutomationSettingsUpdate(
                isEnabled,
                automationScheduleMode,
                nightlyLocalTime,
                idleMinutes,
                ParseScheduledLocalTimes(),
                autoIngestProjectStructure,
                autoIngestProcessRuntime,
                autoConsolidateAfterIngestion,
                modelAccessMode,
                ParseOptionalGuid(defaultProviderProfileIdText, "Default provider"),
                ParseOptionalGuid(defaultAgentIdText, "Default agent"),
                SelectedAllowedProviderProfileIds(),
                OperatorActorId));
            scheduledLocalTimesText = string.Join(Environment.NewLine, settings.ScheduledLocalTimes);
            defaultProviderProfileIdText = settings.DefaultProviderProfileId?.ToString("D") ?? string.Empty;
            defaultAgentIdText = settings.DefaultAgentId?.ToString("D") ?? string.Empty;
            modelAccessStatus = BuildModelAccessStatus(settings);
            NotificationService.Success("Memory settings saved", $"{(settings.IsEnabled ? "Enabled" : "Disabled")} / {FormatLabel(settings.ScheduleMode)} / {FormatLabel(settings.ModelAccessMode)}");
        }
        catch (Exception exception)
        {
            errorMessage = exception.Message;
            NotificationService.Error("Memory settings failed", exception.Message);
        }
        finally
        {
            isBusy = false;
        }
    }

    internal async Task IngestProjectStructureAsync()
    {
        await RunManualSourceIngestionAsync(MemorySourceKind.WorkbenchProjectStructure);
    }

    internal async Task IngestProcessesAsync()
    {
        await RunManualSourceIngestionAsync(MemorySourceKind.ProcessRuntime);
    }

    internal async Task RunConfiguredAutomationAsync()
    {
        if (isBusy)
        {
            return;
        }

        isBusy = true;
        errorMessage = string.Empty;
        automationRunProgress = 15;
        automationRunStatus = $"Starting {FormatLabel(automationScheduleMode).ToLowerInvariant()} automation.";
        BumpUiRevision();
        await InvokeAsync(StateHasChanged);
        await Task.Yield();

        try
        {
            var result = await ScheduledAutomationRunner.RunAsync(new CognitiveMemoryScheduledAutomationRunRequest(
                ProjectId,
                ResolveAutomationTriggerKind(automationScheduleMode),
                OperatorActorId,
                Take: manualSourceTake));

            automationRunProgress = 100;
            automationRunStatus = result.Executed
                ? $"Executed: {result.SourceIngestionRuns} ingestion run(s), {result.SourceItemsCreated} source item(s) created, {result.ConsolidationRuns} consolidation run(s)."
                : FirstNonEmpty(result.Warnings.FirstOrDefault(), "Automation trigger did not run.");
            if (result.Warnings.Count > 0)
            {
                automationRunStatus = $"{automationRunStatus} {string.Join(" ", result.Warnings)}";
            }

            NotificationService.Success("Memory automation finished", automationRunStatus);
            await ReloadSnapshotAsync();
        }
        catch (Exception exception)
        {
            automationRunProgress = 100;
            automationRunStatus = exception.Message;
            errorMessage = exception.Message;
            BumpUiRevision();
            NotificationService.Error("Memory automation failed", exception.Message);
        }
        finally
        {
            isBusy = false;
        }
    }

    internal async Task RebuildProjectionRecordsAsync()
    {
        if (isBusy)
        {
            return;
        }

        isBusy = true;
        errorMessage = string.Empty;
        projectionRebuildProgress = 15;
        projectionRebuildStatus = "Starting projection rebuild.";
        BumpUiRevision();
        await InvokeAsync(StateHasChanged);
        await Task.Yield();

        try
        {
            var result = await ProjectionRebuildService.RebuildAsync(new CognitiveMemoryProjectionRebuildRequest(
                ProjectId,
                Take: 50,
                OperatorActorId));

            projectionRebuildProgress = 100;
            projectionRebuildStatus = $"{FormatLabel(result.Status)}: {result.ProjectedCount} projected, {result.FailedCount} failed, {result.SkippedCount} skipped from {result.SelectedCount} selected.";
            if (result.Warnings.Count > 0)
            {
                projectionRebuildStatus = $"{projectionRebuildStatus} {string.Join(" ", result.Warnings)}";
            }

            NotificationService.Success("Projection rebuild finished", projectionRebuildStatus);
            await ReloadSnapshotAsync();
        }
        catch (Exception exception)
        {
            projectionRebuildProgress = 100;
            projectionRebuildStatus = exception.Message;
            errorMessage = exception.Message;
            BumpUiRevision();
            NotificationService.Error("Projection rebuild failed", exception.Message);
        }
        finally
        {
            isBusy = false;
        }
    }

    internal async Task RunManualSourceIngestionAsync(MemorySourceKind sourceKind)
    {
        if (isBusy)
        {
            return;
        }

        isBusy = true;
        errorMessage = string.Empty;
        manualIngestionProgress = 15;
        manualIngestionStatus = $"Starting {FormatLabel(sourceKind).ToLowerInvariant()} ingestion.";
        BumpUiRevision();
        await InvokeAsync(StateHasChanged);
        await Task.Yield();

        try
        {
            var scopeId = ResolveManualScopeId(sourceKind);
            var result = await SourceIngestionService.IngestAsync(new CognitiveMemorySourceIngestionRequest(
                sourceKind,
                scopeId,
                new CognitiveMemoryIdempotencyKey($"ui:{sourceKind}:{Guid.NewGuid():N}"),
                Take: manualSourceTake,
                ProjectId: ProjectId ?? (sourceKind == MemorySourceKind.WorkbenchProjectStructure ? scopeId : null)));

            manualIngestionProgress = 100;
            manualIngestionStatus = $"{FormatLabel(result.Status)}: {result.CreatedSourceItemCount} created, {result.UpdatedSourceItemCount} updated, {result.CreatedEvidenceAnchorCount} anchors.";
            NotificationService.Success("Memory ingestion finished", manualIngestionStatus);
            await ReloadSnapshotAsync();
        }
        catch (Exception exception)
        {
            manualIngestionProgress = 100;
            manualIngestionStatus = exception.Message;
            errorMessage = exception.Message;
            BumpUiRevision();
            NotificationService.Error("Memory ingestion failed", exception.Message);
        }
        finally
        {
            isBusy = false;
        }
    }

    internal async Task UploadExternalSourceAsync(InputFileChangeEventArgs args)
    {
        if (isBusy)
        {
            return;
        }

        var file = args.File;
        isBusy = true;
        errorMessage = string.Empty;
        externalSourceProgress = 15;
        externalSourceStatus = $"Uploading {file.Name}.";
        BumpUiRevision();
        await InvokeAsync(StateHasChanged);
        await Task.Yield();

        try
        {
            await using var stream = file.OpenReadStream(10 * 1024 * 1024);
            lastExternalSourceResult = await ExternalSourceIngestionService.IngestFileAsync(
                ProjectId,
                file.Name,
                file.ContentType,
                stream,
                file.Size,
                OperatorActorId);

            ApplyExternalSourceResult(lastExternalSourceResult);
            NotificationService.Success("External source ingested", externalSourceStatus);
            await ReloadSnapshotAsync();
        }
        catch (Exception exception)
        {
            externalSourceProgress = 100;
            externalSourceStatus = exception.Message;
            errorMessage = exception.Message;
            BumpUiRevision();
            NotificationService.Error("External source failed", exception.Message);
        }
        finally
        {
            isBusy = false;
        }
    }

    internal async Task IngestExternalLinkAsync()
    {
        if (isBusy)
        {
            return;
        }

        if (!Uri.TryCreate(externalSourceUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
        {
            externalSourceStatus = "Enter an absolute HTTP or HTTPS URL.";
            BumpUiRevision();
            return;
        }

        isBusy = true;
        errorMessage = string.Empty;
        externalSourceProgress = 15;
        externalSourceStatus = $"Fetching {uri.Host}.";
        BumpUiRevision();
        await InvokeAsync(StateHasChanged);
        await Task.Yield();

        try
        {
            lastExternalSourceResult = await ExternalSourceIngestionService.IngestWebsiteAsync(
                ProjectId,
                uri,
                OperatorActorId);

            ApplyExternalSourceResult(lastExternalSourceResult);
            NotificationService.Success("Website ingested", externalSourceStatus);
            await ReloadSnapshotAsync();
        }
        catch (Exception exception)
        {
            externalSourceProgress = 100;
            externalSourceStatus = exception.Message;
            errorMessage = exception.Message;
            BumpUiRevision();
            NotificationService.Error("Website ingestion failed", exception.Message);
        }
        finally
        {
            isBusy = false;
        }
    }

    internal IReadOnlyList<string> ParseScheduledLocalTimes()
    {
        if (string.IsNullOrWhiteSpace(scheduledLocalTimesText))
        {
            return [];
        }

        return scheduledLocalTimesText
            .Split(['\r', '\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    internal IReadOnlyList<Guid> SelectedAllowedProviderProfileIds()
        => modelProviderOptions
            .Where(provider => provider.IsAllowed)
            .Select(provider => provider.Id)
            .ToList();

    internal static Guid? ParseOptionalGuid(
        string value,
        string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Guid.TryParse(value.Trim(), out var id) && id != Guid.Empty
            ? id
            : throw new InvalidOperationException($"{fieldName} must be a valid provider or agent id.");
    }

    internal static string BuildModelAccessStatus(CognitiveMemoryAutomationSettings settings)
    {
        if (!settings.IsEnabled)
        {
            return "Cognitive Memory is disabled globally and will not be injected into agent, workflow, or automation calls.";
        }

        return settings.ModelAccessMode switch
        {
            CognitiveMemoryModelAccessMode.Disabled => "Cognitive Memory model access is disabled.",
            CognitiveMemoryModelAccessMode.LocalProvidersOnly => "Cognitive Memory context is limited to local providers.",
            CognitiveMemoryModelAccessMode.SelectedProvidersOnly => "Cognitive Memory context is limited to the selected provider allow-list.",
            _ => "Cognitive Memory context can be injected for any enabled provider."
        };
    }

    internal Guid ResolveManualScopeId(MemorySourceKind sourceKind)
    {
        if (string.IsNullOrWhiteSpace(manualSourceScopeText))
        {
            if (ProjectId.HasValue)
            {
                return ProjectId.Value;
            }

            if (sourceKind == MemorySourceKind.ProcessRuntime)
            {
                return Guid.Empty;
            }

            throw new InvalidOperationException("Project structure ingestion requires a project id or scope id.");
        }

        return Guid.TryParse(manualSourceScopeText, out var scopeId)
            ? scopeId
            : throw new InvalidOperationException("Scope id must be a GUID.");
    }

    internal void ApplyExternalSourceResult(CognitiveMemoryExternalSourceIngestResult result)
    {
        externalSourceProgress = result.ProgressPercent;
        externalSourceStatus = result.FailureMessage is null
            ? $"{FormatLabel(result.Status)}: {result.StatusMessage}"
            : result.FailureMessage;
    }

    internal static CognitiveMemoryAutomationTriggerKind ResolveAutomationTriggerKind(
        CognitiveMemoryAutomationScheduleMode scheduleMode)
        => scheduleMode switch
        {
            CognitiveMemoryAutomationScheduleMode.Nightly => CognitiveMemoryAutomationTriggerKind.Nightly,
            CognitiveMemoryAutomationScheduleMode.IdleTimeout => CognitiveMemoryAutomationTriggerKind.IdleTimeout,
            CognitiveMemoryAutomationScheduleMode.ScheduledMoments => CognitiveMemoryAutomationTriggerKind.ScheduledMoment,
            _ => CognitiveMemoryAutomationTriggerKind.Manual
        };
}
