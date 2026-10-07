using System.Text;
using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;
using CapabilityKind = CanDoItAll.AgentFramework.Models.CapabilityKind;

namespace CanDoItAll.AgentFramework.CapabilityAuthoring.UI;

public sealed class CapabilityAuthoringSession : IDisposable {
    public const long MaximumSkillUploadBytes = 1_048_576;
    private readonly CapabilityAuthoringOperations operations;
    private readonly NotificationService notifications;
    private readonly ILogger logger;
    private readonly CancellationTokenSource lifetime = new();
    private readonly CancellationTokenRegistration ownerRegistration;
    private readonly Guid? requestedId;
    private bool disposed;
    private long? setupRevision;
    private bool? setupSucceeded;
    private IReadOnlyList<CapabilityDiagnostic> setupDiagnostics = [];
    private IReadOnlyList<string> setupTools = [];
    private CancellationTokenSource? activeUpload;

    public CapabilityAuthoringSession(CapabilityAuthoringOperations operations, Guid? requestedId,
        CapabilityKind initialKind, CancellationToken ownerToken, NotificationService notifications, ILogger logger) {
        this.operations = operations;
        this.requestedId = requestedId;
        this.notifications = notifications;
        this.logger = logger;
        ownerRegistration = ownerToken.Register(lifetime.Cancel);
        Draft = new(new() { Kind = initialKind }, wizard: !requestedId.HasValue);
        IsLoading = requestedId.HasValue;
    }

    public CapabilityAuthoringDraft Draft { get; private set; }
    public event Action? StateChanged;
    public bool IsCurrent => !disposed && !lifetime.IsCancellationRequested;
    public bool IsLoading { get; private set; }
    public bool LoadFailed { get; private set; }
    public bool Busy { get; private set; }
    public bool SaveUnknown { get; private set; }
    public bool SetupUnknown { get; private set; }
    public string? Status { get; private set; }
    public Guid? AcceptedId { get; private set; }
    public bool? SetupSucceeded => setupRevision == Draft.Revision ? setupSucceeded : null;
    public bool SetupHistorical => setupRevision.HasValue && setupRevision != Draft.Revision;
    public IReadOnlyList<CapabilityDiagnostic> SetupDiagnostics => setupRevision == Draft.Revision ? setupDiagnostics : [];
    public IReadOnlyList<string> SetupTools => setupRevision == Draft.Revision ? setupTools : [];

    public async Task LoadAsync() {
        if (!IsCurrent || requestedId is not { } id || Busy) {
            return;
        }
        using var request = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        IsLoading = true;
        LoadFailed = false;
        try {
            var loaded = await operations.Load(id, request.Token);
            if (!IsCurrent) {
                return;
            }
            if (loaded.Id != id) {
                throw new InvalidOperationException("The capability owner returned a different identity.");
            }
            Draft.Dispose();
            Draft = new(loaded, wizard: false);
        } catch (Exception) when (!IsCurrent) {
        } catch (Exception error) {
            LoadFailed = true;
            logger.LogWarning("Capability editor acquisition failed for {CapabilityId}: {FailureType}", id, error.GetType().Name);
            notifications.Error("Capability details failed to load", "The requested capability could not be loaded. Retry or close the editor.");
        } finally {
            if (IsCurrent) {
                IsLoading = false;
            }
        }
    }

    public bool ValidateStep(bool identityOnly) => Draft.Prepare(operations.EnvironmentNameComparer, identityOnly) is not null;

    public async Task<bool> SaveAsync() {
        if (!IsCurrent || Busy || IsLoading || LoadFailed || SaveUnknown) {
            return false;
        }
        var submission = Draft.Prepare(operations.EnvironmentNameComparer);
        if (submission is null) {
            return false;
        }
        var revision = Draft.Revision;
        Busy = true;
        Status = null;
        StateChanged?.Invoke();
        CapabilityAuthoringSaveOutcome outcome;
        try {
            outcome = await operations.Save(submission, CancellationToken.None);
        } catch (Exception error) {
            logger.LogWarning("Capability save acknowledgement unavailable for {CapabilityId}: {FailureType}", submission.Id, error.GetType().Name);
            outcome = new CapabilityAuthoringSaveOutcome.Unknown();
        } finally {
            if (IsCurrent) {
                Busy = false;
            }
        }
        if (!IsCurrent) {
            return false;
        }
        switch (outcome) {
            case CapabilityAuthoringSaveOutcome.Accepted accepted when accepted.Definition.Id is { } id:
                AcceptedId = id;
                Draft.Model.Id = id;
                Draft.Model.ExpectedFingerprint = accepted.Definition.ExpectedFingerprint;
                Status = revision == Draft.Revision ? "Capability saved." : "Capability saved. Later draft edits remain unsaved.";
                notifications.Success("Capability saved", "Capability metadata was saved.");
                return revision == Draft.Revision;
            case CapabilityAuthoringSaveOutcome.Rejected rejected:
                Status = rejected.IsConflict
                    ? "The capability changed after it was opened. Your draft is retained; reopen the saved definition before resolving this conflict."
                    : "The owner rejected this save without committing it. Review the identity and configuration.";
                notifications.Warning("Capability was not saved", Status);
                return false;
            default:
                SaveUnknown = true;
                Status = "The save outcome is unknown. The request will not be repeated. Check the catalog before opening another editor.";
                notifications.Error("Capability save failed", Status);
                return false;
        }
    }

    public async Task TestSetupAsync() {
        if (!IsCurrent || Busy || IsLoading || LoadFailed || SetupUnknown || Draft.Model.Kind is not (CapabilityKind.Tool or CapabilityKind.McpServer)) {
            return;
        }
        var submission = Draft.Prepare(operations.EnvironmentNameComparer);
        if (submission is null) {
            notifications.Warning("Setup test was not started", "Review the validation messages before testing setup.");
            return;
        }
        var revision = Draft.Revision;
        var input = string.IsNullOrWhiteSpace(Draft.Tool.TestInputJson) ? "{}" : Draft.Tool.TestInputJson;
        using var request = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        Busy = true;
        StateChanged?.Invoke();
        try {
            bool succeeded;
            IReadOnlyList<CapabilityDiagnostic> diagnostics;
            IReadOnlyList<string> tools = [];
            if (submission.Kind == CapabilityKind.Tool) {
                var result = await operations.TestTool(submission, input, request.Token);
                succeeded = result.IsSuccess;
                diagnostics = result.Diagnostics;
            } else {
                var result = await operations.TestMcp(submission, request.Token);
                succeeded = result.IsSuccess;
                diagnostics = result.Diagnostics;
                tools = result.DiscoveredTools.Select(tool => tool.Name.Value).ToArray();
            }
            if (!IsCurrent) {
                return;
            }
            setupRevision = revision;
            setupSucceeded = succeeded;
            setupDiagnostics = diagnostics;
            setupTools = tools;
            if (revision == Draft.Revision) {
                var title = submission.Kind == CapabilityKind.Tool ? "Tool setup test" : "MCP setup test";
                if (succeeded) {
                    notifications.Success(title, "Setup test completed successfully.");
                } else {
                    notifications.Warning(title, "Setup test returned diagnostics.");
                }
            }
        } catch (Exception) when (!IsCurrent) {
        } catch (Exception error) {
            SetupUnknown = true;
            setupRevision = revision;
            setupSucceeded = null;
            setupDiagnostics = [];
            setupTools = [];
            Status = "The setup outcome is unknown and may have produced an effect. Review the native result before starting another test.";
            logger.LogWarning("Capability setup acknowledgement unavailable for {CapabilityId}: {FailureType}", submission.Id, error.GetType().Name);
            if (revision == Draft.Revision) {
                notifications.Error("Setup acknowledgement unavailable", Status);
            }
        } finally {
            if (IsCurrent) {
                Busy = false;
                StateChanged?.Invoke();
            }
        }
    }

    public async Task UploadAsync(IBrowserFile file) {
        if (!IsCurrent || Busy && activeUpload is null || Draft.Model.Kind != CapabilityKind.Skill || Draft.SkillMode != CapabilitySkillInputMode.Upload) {
            return;
        }
        activeUpload?.Cancel();
        Draft.Changed();
        var revision = Draft.Revision;
        using var request = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        activeUpload = request;
        Busy = true;
        StateChanged?.Invoke();
        try {
            await using var stream = file.OpenReadStream(MaximumSkillUploadBytes, request.Token);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var content = await reader.ReadToEndAsync(request.Token);
            if (!IsCurrent || !ReferenceEquals(activeUpload, request) || Draft.Revision != revision) {
                return;
            }
            Draft.Skill.InlineInstructions = content;
            Draft.UploadedFileName = file.Name;
            if (string.IsNullOrWhiteSpace(Draft.Model.Name)) {
                Draft.ChangeName(Path.GetFileNameWithoutExtension(file.Name));
            }
            Draft.Changed();
            notifications.Success("Skill file loaded", "SKILL.md content was loaded into the inline skill draft.");
        } catch (Exception) when (!IsCurrent || !ReferenceEquals(activeUpload, request) || Draft.Revision != revision) {
        } catch (Exception error) {
            logger.LogWarning("Capability skill upload failed: {FailureType}", error.GetType().Name);
            notifications.Error("Skill file upload failed", "The file could not be read within the 1 MiB limit.");
        } finally {
            if (ReferenceEquals(activeUpload, request)) {
                activeUpload = null;
                if (IsCurrent) {
                    Busy = false;
                    StateChanged?.Invoke();
                }
            }
        }
    }

    public void CompletionFailed() => Status = "The capability was saved, but the parent could not refresh. Close with the saved identity to retry the read; the definition will not be recreated.";

    public void Dispose() {
        if (disposed) {
            return;
        }
        disposed = true;
        ownerRegistration.Dispose();
        lifetime.Cancel();
        lifetime.Dispose();
        Draft.Dispose();
    }
}
