using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;
using CanDoItAll.Workbench.Operators.UI.Secrets;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectSecretReferenceSession(ProjectWriteAdmission admission, ProjectStructureNode original, bool isEdit,
    IWorkspaceSecretsOwner secrets, ProjectWorkbenchService workbench, Func<bool> current, Func<bool> originalAuthority,
    Func<SecretListItem, SecretReferenceInput, Action<ProjectStructureNode>, Task<ProjectStructureNode>> write,
    Func<ProjectStructureNode, Task> observe, Action<SecretReferenceReceipt> record, ILogger logger) {
    public SecretReferenceState State { get; } = new(isEdit);
    public bool IsCurrent => current() && originalAuthority() && secrets.IsCurrent && !State.IsRetired;
    private long readGeneration;
    private SecretReferenceInput? submitted;
    private ProjectStructureNode? committed;

    public void Retire() {
        State.Retire();
        readGeneration++;
    }

    public async Task LoadAsync() {
        if (!IsCurrent || State.IsBusy) {
            return;
        }
        var generation = ++readGeneration;
        State.IsLoading = true;
        try {
            var items = await secrets.ListAsync(CancellationToken.None);
            if (IsCurrent && generation == readGeneration) {
                State.Items = items;
                State.IsUnavailable = false;
                State.Message = string.Empty;
            }
        } catch (Exception failure) {
            if (IsCurrent && generation == readGeneration) {
                State.IsUnavailable = true;
                State.Message = "Secret metadata could not be loaded. Retry the read before submitting a reference.";
            }
            Log(failure);
        } finally {
            if (IsCurrent && generation == readGeneration) {
                State.IsLoading = false;
            }
        }
    }

    public async Task UseAsync(SecretReferenceInput input) {
        if (!Begin()) {
            return;
        }
        submitted = input;
        try {
            await RequireOriginalAsync();
            var item = (await secrets.ListAsync(CancellationToken.None)).FirstOrDefault(item => item.Id == input.SecretId);
            if (item is null) {
                Refuse("The selected secret is unavailable. No project reference was submitted.");
                return;
            }
            if (!IsCurrent) {
                return;
            }
            await SaveReferenceAsync(item);
        } catch (Exception failure) {
            Failure(failure, false);
        } finally {
            State.IsBusy = false;
        }
    }

    public async Task CreateAsync(SecretReferenceInput input, SecretEditorModel draft) {
        if (!Begin()) {
            return;
        }
        submitted = input;
        var model = draft.Copy();
        var dispatched = false;
        try {
            await RequireOriginalAsync();
            if (!IsCurrent) {
                return;
            }
            dispatched = true;
            var result = await secrets.SaveAsync(model, CancellationToken.None);
            if (result.State == SettingsWriteState.Refused) {
                Refuse("The vault refused this draft. Check its required fields and original access before retrying.");
                return;
            }
            if (result.Value == Guid.Empty) {
                throw new InvalidOperationException("The vault returned no accepted identity.");
            }
            State.Draft.Create.SecretValue = string.Empty;
            draft.SecretValue = string.Empty;
            model.SecretValue = string.Empty;
            State.RequiresObservation = true;
            State.VaultWarning = result.State == SettingsWriteState.CommittedWarning
                ? "The vault record was committed; its cleanup or activity follow-up still needs native owner attention." : null;
            Receipt(new(SecretReferencePhase.VaultCommitted, result.Value, null,
                result.State == SettingsWriteState.CommittedWarning ? "Vault record created; its secondary work needs attention. The project reference is not yet confirmed."
                : "Vault record created. The project reference is not yet confirmed."));
            var item = await ReadAcceptedSecretAsync();
            await SaveReferenceAsync(item);
        } catch (Exception failure) {
            Failure(failure, dispatched);
        } finally {
            model.SecretValue = string.Empty;
            State.IsBusy = false;
        }
    }

    public async Task RetryAsync() {
        if (!IsCurrent || State.IsBusy) {
            return;
        }
        if (State.Receipt is null || State.Receipt.Phase is SecretReferencePhase.NotSubmitted or SecretReferencePhase.Refused) {
            await LoadAsync();
            return;
        }
        State.IsBusy = true;
        try {
            if (committed is not null) {
                await ObserveReferenceAsync();
            } else if (State.Receipt.Phase == SecretReferencePhase.VaultCommitted) {
                await ReadAcceptedSecretAsync();
                await RequireOriginalAsync();
                State.CanFinishReference = IsCurrent;
                State.Message = "The accepted secret metadata is available. Finish the original reference explicitly; no vault write was repeated.";
            } else {
                State.Message = "The original write is unconfirmed. Inspect its exact identity with the native owner; no write was repeated.";
            }
        } catch (Exception failure) {
            Failure(failure, false);
        } finally {
            State.IsBusy = false;
        }
    }

    public async Task FinishAsync() {
        if (!IsCurrent || State.IsBusy || !State.CanFinishReference || State.Receipt?.Phase != SecretReferencePhase.VaultCommitted) {
            return;
        }
        State.IsBusy = true;
        State.CanFinishReference = false;
        try {
            await SaveReferenceAsync(await ReadAcceptedSecretAsync());
        } catch (Exception failure) {
            Failure(failure, false);
        } finally {
            State.IsBusy = false;
        }
    }

    private bool Begin() {
        if (!IsCurrent || !State.CanEdit) {
            return false;
        }
        State.IsBusy = true;
        State.Message = string.Empty;
        return true;
    }

    private async Task RequireOriginalAsync() {
        if (!originalAuthority() || !secrets.IsCurrent) {
            throw new ProjectWriteAdmissionRejectedException(admission);
        }
        if (!isEdit && IsOriginalProjectRoot(original)) {
            var surface = await workbench.GetStructureAsync(admission.ProjectId);
            if (surface.ExpectedProjectAdmission != admission) {
                throw new ProjectWriteAdmissionRejectedException(admission);
            }
            if (!surface.Nodes.Any(IsOriginalProjectRoot)) {
                throw new ProjectStructureEditConflictException();
            }
        } else {
            await workbench.RequireContentCurrentAsync(admission, original);
        }
        if (!originalAuthority() || !secrets.IsCurrent) {
            throw new ProjectWriteAdmissionRejectedException(admission);
        }
    }

    private bool IsOriginalProjectRoot(ProjectStructureNode node) => node.IsSystemManaged &&
        node.ObjectType == ProjectObjectType.ProjectRoot && node.ProjectRole == ProjectStructureProjectRole.ActiveProject &&
        node.ArtifactId == admission.ProjectId && node.Id == ProjectWorkbenchGraphConventions.BuildProjectRootNodeKey(admission.ProjectId);

    private async Task<SecretListItem> ReadAcceptedSecretAsync() {
        if (!originalAuthority() || !secrets.IsCurrent) {
            throw new ProjectWriteAdmissionRejectedException(admission);
        }
        var id = State.Receipt?.SecretId ?? throw new InvalidOperationException("No accepted secret identity exists.");
        var items = await secrets.ListAsync(CancellationToken.None);
        var item = items.FirstOrDefault(item => item.Id == id)
            ?? throw new InvalidOperationException("The accepted secret metadata is unavailable.");
        if (IsCurrent) {
            State.Items = items;
            State.Draft.SelectedId = id;
        }
        return item;
    }

    private async Task SaveReferenceAsync(SecretListItem item) {
        await RequireOriginalAsync();
        var input = submitted ?? throw new InvalidOperationException("The original reference submission is unavailable.");
        State.CanFinishReference = false;
        State.RequiresObservation = true;
        try {
            await write(item, input, node => {
                committed = node;
                Receipt(new(SecretReferencePhase.ReferenceCommitted, item.Id, node.Id,
                    "Project reference committed. Surface observation is pending."));
            });
        } catch (Exception failure) when (committed is null && !KnownRejection(failure)) {
            Receipt(new(SecretReferencePhase.Unknown, item.Id, null, "The project reference write is unconfirmed. The secret identity is retained; do not repeat the write."));
            throw;
        }
        if (IsCurrent) {
            await ObserveReferenceAsync();
        }
    }

    private async Task ObserveReferenceAsync() {
        if (committed is not { } accepted) {
            return;
        }
        var surface = await workbench.GetStructureAsync(admission.ProjectId);
        var found = surface.Nodes.FirstOrDefault(node => node.Id == accepted.Id && node.RecordId == accepted.RecordId);
        if (surface.ExpectedProjectAdmission != admission || found is null ||
            ProjectObjectMetadataSerializer.Parse(found.MetadataJson).SecretReference?.SecretId != State.Receipt?.SecretId) {
            throw new ProjectStructureEditConflictException();
        }
        if (!IsCurrent) {
            return;
        }
        await observe(found);
        State.RequiresObservation = false;
        State.Message = "The original reference was saved and observed.";
        Receipt(State.Receipt! with { Phase = SecretReferencePhase.Observed, Message = State.Message });
    }

    private void Refuse(string message) {
        State.RequiresObservation = false;
        State.Message = message;
        Receipt(new(SecretReferencePhase.Refused, null, null, message));
    }

    private void Failure(Exception failure, bool vaultDispatched) {
        State.CanFinishReference = false;
        if (State.Receipt is { Phase: SecretReferencePhase.VaultCommitted or SecretReferencePhase.ReferenceCommitted or SecretReferencePhase.Unknown } known) {
            State.RequiresObservation = true;
            State.Message = $"{known.Message} Original project lifetime: {admission.LifetimeId:D}. Retry observation; accepted writes will not be repeated.";
            Receipt(known with { Message = State.Message });
        } else if (vaultDispatched && !KnownRejection(failure)) {
            State.RequiresObservation = true;
            State.Draft.Create.SecretValue = string.Empty;
            Receipt(new(SecretReferencePhase.Unknown, (failure as SecretMutationUnknownException)?.SecretId, null,
                "The vault write is unconfirmed. Its staged payload may exist. Inspect the original identity before creating another secret."));
        } else {
            Refuse($"The original reference is unavailable or changed. Reopen it before saving. Project lifetime: {admission.LifetimeId:D}.");
        }
        Log(failure);
    }

    private void Receipt(SecretReferenceReceipt receipt) {
        State.Receipt = receipt;
        record(receipt);
    }

    private void Log(Exception failure) => logger.LogWarning("Secret reference opening {OpeningId}, project {ProjectId}, lifetime {LifetimeId} ended with {FailureType}; secret {SecretId}, reference {NodeId}, phase {Phase}.",
        State.OpeningId, admission.ProjectId, admission.LifetimeId, failure.GetType().Name, State.Receipt?.SecretId, State.Receipt?.NodeId, State.Receipt?.Phase);

    private static bool KnownRejection(Exception failure) => failure is ProjectWriteAdmissionRejectedException or ProjectStructureEditConflictException or ArgumentException or InvalidDataException;
}
