using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.SharedKernel;
using CanDoItAll.SharedKernel.Configuration;
using EditorModel = CanDoItAll.AgentFramework.Models.ProviderProfileEditorModel;

namespace CanDoItAll.Modules.AgentFramework;

public enum ProviderWriteDisposition { Rejected, Conflict, Committed, Unconfirmed }

public sealed record ProviderWriteResult(
    ProviderWriteDisposition Disposition,
    Guid? ProviderId = null,
    string? Message = null,
    Guid? ConcurrencyToken = null,
    ProviderMutationAttempt? Attempt = null);

public sealed record ProviderHealthCheckOutcome(ProviderHealthResult? Health, ProviderWriteResult? Persistence);

public interface IProviderEditorCommands {
    Task<ProviderWriteResult> SaveAsync(ProviderEditorSubmission submission, CancellationToken cancellationToken);
    Task<ProviderWriteResult> DeleteAsync(Guid providerId, CancellationToken cancellationToken);
    Task<ProviderHealthCheckOutcome> CheckHealthAsync(Guid providerId, bool sourceManaged, CancellationToken cancellationToken);
    Task<Result<ProviderModelPricingRefreshResult>> DiscoverModelsAsync(ProviderEditorSubmission submission, CancellationToken cancellationToken);
    Task ReconcileAsync(Guid providerId, CancellationToken cancellationToken);
    Task<ProviderMutationVerification> VerifyAsync(ProviderMutationAttempt attempt, CancellationToken cancellationToken);
}

public sealed class ProviderEditorCommands(
    IProviderRuntimeAdministrationService runtime,
    IProviderAdministrationService administration,
    IProviderCatalogReconciliation reconciliation,
    IProviderMutationVerification verification) : IProviderEditorCommands {
    public Task<ProviderWriteResult> SaveAsync(ProviderEditorSubmission submission, CancellationToken cancellationToken)
        => ExecuteWriteAsync(() => runtime.SaveProviderAsync(submission.CreateRequest(), cancellationToken), cancellationToken, submission.Attempt);

    public Task<ProviderWriteResult> DeleteAsync(Guid providerId, CancellationToken cancellationToken)
        => ExecuteWriteAsync(async () => {
            await runtime.DeleteProviderAsync(providerId, cancellationToken);
            return providerId;
        }, cancellationToken);

    public async Task<ProviderHealthCheckOutcome> CheckHealthAsync(
        Guid providerId, bool sourceManaged, CancellationToken cancellationToken) {
        if (sourceManaged) {
            return new(await runtime.TestProviderAsync(providerId, cancellationToken), null);
        }
        ProviderHealthResult? health = null;
        try {
            var write = await ExecuteWriteAsync(async () => {
                health = await runtime.TestProviderAsync(providerId, cancellationToken);
                return providerId;
            }, cancellationToken);
            return new(health, write);
        } catch (ProviderHealthDiagnosticException exception) {
            return new(new(false, exception.Message, []), null);
        }
    }

    public Task<Result<ProviderModelPricingRefreshResult>> DiscoverModelsAsync(
        ProviderEditorSubmission submission, CancellationToken cancellationToken) {
        var draft = submission.CreateRequest();
        var request = new ProviderManagement.ProviderProfileEditorModel {
            Id = draft.Id,
            Name = draft.Name,
            ConnectorPluginKey = ProviderMetadata.ResolveConnectorPluginKey(draft, null),
            ApiKeySecretId = ProviderMetadata.ResolveSecretRecordId(draft),
            Configuration = ConnectorConfigState.FromJson(draft.ConfigurationJson),
            IsPrivateProvider = draft.IsPrivateProvider,
            ModelPrices = draft.ModelPrices
        };
        request.Configuration.SetText(ProviderConnectorFieldKeys.BaseUrl, draft.BaseUrl);
        request.Configuration.SetText(ProviderConnectorFieldKeys.DefaultModel, draft.DefaultModel);
        return administration.RefreshProviderModelPricesAsync(request, cancellationToken);
    }

    public Task ReconcileAsync(Guid providerId, CancellationToken cancellationToken)
        => reconciliation.ReconcileAsync(providerId, cancellationToken);

    public Task<ProviderMutationVerification> VerifyAsync(ProviderMutationAttempt attempt, CancellationToken cancellationToken)
        => verification.VerifyAsync(attempt, cancellationToken);

    private static async Task<ProviderWriteResult> ExecuteWriteAsync(Func<Task<Guid>> write, CancellationToken cancellationToken, ProviderMutationAttempt? attempt = null) {
        cancellationToken.ThrowIfCancellationRequested();
        try {
            return new(ProviderWriteDisposition.Committed, await write());
        } catch (ProviderMutationCommittedException exception) {
            return new(ProviderWriteDisposition.Committed, exception.ProviderId, exception.Message, exception.Commit.ConcurrencyToken);
        } catch (ProviderMutationUnconfirmedException exception) {
            return new(ProviderWriteDisposition.Unconfirmed, exception.Attempt.ProviderId, exception.Message, Attempt: exception.Attempt);
        } catch (ProviderProfileConcurrencyException) {
            return new(ProviderWriteDisposition.Conflict, Message: "The provider changed. Reload before saving again.");
        } catch (ProviderProfileValidationException exception) {
            return new(ProviderWriteDisposition.Rejected, Message: exception.Message);
        } catch (SharedProviderProfileDeletionBlockedException exception) {
            return new(ProviderWriteDisposition.Rejected, Message: SharedProviderDeletionMessages.For(exception.ReferenceKinds));
        } catch (ProviderHealthDiagnosticException) {
            throw;
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (Exception) {
            return new(ProviderWriteDisposition.Unconfirmed, attempt?.ProviderId,
                Message: "The provider write is unconfirmed. Verify the canonical state before another write.", Attempt: attempt);
        }
    }
}
