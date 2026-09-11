using System.Collections.Immutable;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Application;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Ports;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Agents.SimpleChats;

public sealed class HrSimpleChatAdministration(
    ILlmChatDefinitionApplicationService definitions,
    ILlmChatDefinitionCreateReceiptService receipts,
    ILlmChatProviderResolver providers,
    ILlmChatRuntimeLeaseFactory leaseFactory,
    ILlmChatOperationScopeAccessor operationScope,
    HrSimpleChatRuntimeAuthorization authorization,
    HrSimpleChatProposalCodec codec) {

    private const string DefinitionEffectSourceKind = "simple-chat-definition";

    internal async ValueTask<AgentToolReceiptObservation> ReconcileCancelledCreateAsync(AgentToolReceiptReconciliationClaim claim,
        CancellationToken cancellationToken) {
        await using var lease = await leaseFactory.AcquireAsync(cancellationToken);
        EnsureCurrent(lease);
        using var scope = operationScope.Push(new(LlmChatOperationId.New(), lease.Identity));
        var admitted = await claim.RequireAsync(lease.CancellationToken);
        if (admitted.Payload.ToolName != HrSimpleChatToolPolicy.Get(HrSimpleChatOperation.Create).ToolName) {
            throw new InvalidOperationException("This receipt reconciler only handles its owned create operation.");
        }

        codec.Read<CreateLlmChatDefinitionCommand>(admitted.Payload);
        await RequireAccessAsync();
        var result = await receipts.FindReceiptAsync(CreateKey(admitted.Session, admitted.Binding.IntentId.Value), lease.CancellationToken);
        EnsureSuccess(result);
        EnsureCurrent(lease);
        await claim.RequireAsync(lease.CancellationToken);
        await RequireAccessAsync();
        EnsureCurrent(lease);
        return result.Value is { } receipt
            ? new(new(DefinitionEffectSourceKind, receipt.DefinitionId.Value.ToString("D")),
                AgentToolProtocolEnvelope.Create("simple-chat-create-receipt", 1,
                    JsonSerializer.Serialize(OriginalIdentity(receipt), HrSimpleChatProposalCodec.SerializerOptions)))
            : AgentToolReceiptObservation.NotObserved;

        async Task RequireAccessAsync() {
            try {
                await authorization.RequireCancelledReceiptAsync(admitted, lease.Identity, lease.CancellationToken);
            } catch (HrSimpleChatAdministrationException exception) when (exception.Code == "hr-simple-chat.authorization-denied") {
                throw new AgentToolReceiptAccessDeniedException();
            }
        }
    }

    public Task<HrSimpleChatDefinitionPage> SearchAsync(
        AgentRuntimeToolProviderContext context, HrSimpleChatSearchRequest request, CancellationToken cancellationToken) {
        var prepared = codec.PrepareSearch(request);
        var normalized = codec.Read<HrSimpleChatSearchRequest>(prepared);
        return ExecuteAsync(context, HrSimpleChatOperation.Search, async (_, token) => {
            var page = Require(await definitions.ListPageAsync(normalized.ToOwnerQuery(), token));
            return new HrSimpleChatDefinitionPage(page.Items.Select(Summary).ToImmutableArray(),
                page.NextCursor is { } cursor ? new(cursor.DefinitionId.Value, cursor.UpdatedAtUtc) : null);
        }, cancellationToken);
    }

    public Task<IReadOnlyList<LlmChatProviderOption>> OptionsAsync(
        AgentRuntimeToolProviderContext context, CancellationToken cancellationToken)
        => ExecuteAsync(context, HrSimpleChatOperation.Options,
            async (_, token) => Require(await providers.ListOptionsAsync(token)), cancellationToken);

    public Task<HrSimpleChatDefinitionSettings> SettingsAsync(
        AgentRuntimeToolProviderContext context, HrSimpleChatDefinitionVersion request, CancellationToken cancellationToken) {
        var proposed = codec.PrepareSettings(request);
        return ExecuteAsync(context, HrSimpleChatOperation.Settings, async (session, token) => {
            var admission = await authorization.RequireApprovedProposalAsync(session, proposed, token);
            var expected = codec.Read<HrSimpleChatDefinitionVersion>(admission.Payload);
            var current = await ReadExpectedAsync(expected, token);
            return new HrSimpleChatDefinitionSettings(Summary(current), Settings(current));
        }, cancellationToken);
    }

    public Task<HrSimpleChatCreateResponse> CreateAsync(
        AgentRuntimeToolProviderContext context, CreateLlmChatDefinitionCommand request, CancellationToken cancellationToken) {
        var proposed = codec.PrepareCreate(request);
        return ExecuteAsync(context, HrSimpleChatOperation.Create, async (session, token) => {
            var admission = await authorization.RequireApprovedProposalAsync(session, proposed, token);
            var approvedDefinition = codec.Read<CreateLlmChatDefinitionCommand>(admission.Payload);
            var key = CreateKey(session, admission.IntentId.Value);
            var response = Require(await receipts.CreateOnceAsync(new(key, approvedDefinition), token));
            AgentToolInvocationEffectScope.RecordCommitted(DefinitionEffectSourceKind, response.Receipt.DefinitionId.Value.ToString("D"));
            return new HrSimpleChatCreateResponse(OriginalIdentity(response.Receipt), response.WasReplay);
        }, cancellationToken);
    }

    public Task<HrSimpleChatDefinitionSummary> UpdateAsync(
        AgentRuntimeToolProviderContext context, HrSimpleChatUpdateRequest request, CancellationToken cancellationToken) {
        var proposed = codec.PrepareUpdate(request);
        return ExecuteAsync(context, HrSimpleChatOperation.Update, async (session, token) => {
            var admission = await authorization.RequireApprovedProposalAsync(session, proposed, token);
            var approved = codec.Read<HrSimpleChatUpdateRequest>(admission.Payload);
            await ReadExpectedAsync(approved.Expected, token);
            var value = approved.Definition;
            var changed = Require(await definitions.UpdateAsync(new(
                new(approved.Expected.DefinitionId), value.Name, value.Summary, value.AvatarImageUrl,
                value.SystemPrompt, value.ProviderProfileId, value.Model, value.Settings, value.Timeout,
                value.ResponseFormat, value.RevisionReason, approved.Expected.ConcurrencyToken, value.Tags), token));
            AgentToolInvocationEffectScope.RecordCommitted(DefinitionEffectSourceKind, changed.Definition.Id.Value.ToString("D"));
            return Summary(changed);
        }, cancellationToken);
    }

    public Task<HrSimpleChatDefinitionSummary> ChangeStatusAsync(
        AgentRuntimeToolProviderContext context, HrSimpleChatStatusRequest request, CancellationToken cancellationToken) {
        var proposed = codec.PrepareStatus(request);
        return ExecuteAsync(context, HrSimpleChatOperation.Status, async (session, token) => {
            var admission = await authorization.RequireApprovedProposalAsync(session, proposed, token);
            var approved = codec.Read<HrSimpleChatStatusRequest>(admission.Payload);
            await ReadExpectedAsync(approved.Expected, token);
            var changed = Require(await definitions.ChangeStatusAsync(new(new(approved.Expected.DefinitionId),
                approved.Status, approved.Expected.ConcurrencyToken), token));
            AgentToolInvocationEffectScope.RecordCommitted(DefinitionEffectSourceKind, changed.Definition.Id.Value.ToString("D"));
            return Summary(changed);
        }, cancellationToken);
    }

    public Task<HrSimpleChatOriginalIdentity?> FindReceiptAsync(
        AgentRuntimeToolProviderContext context, HrSimpleChatReceiptRequest request, CancellationToken cancellationToken) {
        var prepared = codec.PrepareReceipt(request);
        var normalized = codec.Read<HrSimpleChatReceiptRequest>(prepared);
        return ExecuteAsync<HrSimpleChatOriginalIdentity?>(context, HrSimpleChatOperation.Receipt, async (session, token) => {
            var result = await receipts.FindReceiptAsync(CreateKey(session, normalized.IntentId), token);
            EnsureSuccess(result);
            var receipt = result.Value;
            return receipt is null ? null : OriginalIdentity(receipt);
        }, cancellationToken);
    }

    internal async ValueTask<IAsyncDisposable> AcquireAdmissionAuthorizationAsync(
        AgentRuntimeToolProviderContext context, HrSimpleChatToolOperation operation,
        AgentToolPreparedPayload payload, CancellationToken cancellationToken) {
        if (!string.Equals(operation.ToolName, payload.ToolName, StringComparison.Ordinal) ||
            operation.Effect != payload.Effect || operation.Recovery != payload.Recovery) {
            throw new HrSimpleChatAdministrationException("hr-simple-chat.authorization-denied",
                "The saved proposal does not match this provider-owned operation policy.");
        }

        var lease = await leaseFactory.AcquireAsync(cancellationToken);
        try {
            EnsureCurrent(lease);
            await authorization.RequireSessionAsync(context, operation, lease.Identity, lease.CancellationToken);
            EnsureCurrent(lease);
            return lease;
        } catch {
            await lease.DisposeAsync();
            throw;
        }
    }

    internal async ValueTask<IAsyncDisposable?> AcquireResultDisclosureAsync(
        AgentRuntimeToolProviderContext context, HrSimpleChatToolOperation operation,
        AgentToolResultDisclosure disclosure, CancellationToken cancellationToken) {
        if (operation.ToolName != disclosure.Payload.ToolName || operation.Effect != disclosure.Payload.Effect ||
            operation.Recovery != disclosure.Payload.Recovery) {
            throw new HrSimpleChatAdministrationException("hr-simple-chat.authorization-denied",
                "The saved result does not match this provider-owned operation policy.");
        }

        var lease = await leaseFactory.AcquireAsync(cancellationToken);
        try {
            EnsureCurrent(lease);
            using var scope = operationScope.Push(new(LlmChatOperationId.New(), lease.Identity));
            var session = await authorization.RequireResultDisclosureAsync(context, operation, lease.Identity, lease.CancellationToken);
            if (disclosure.EffectState != AgentToolEffectState.NotCommitted) {
                switch (operation.Operation) {
                    case HrSimpleChatOperation.Settings:
                        await ReadExpectedAsync(codec.Read<HrSimpleChatDefinitionVersion>(disclosure.Payload), lease.CancellationToken);
                        break;
                    case HrSimpleChatOperation.Create:
                        var result = disclosure.Result.Deserialize<HrSimpleChatCreateResponse>(HrSimpleChatProposalCodec.SerializerOptions)
                            ?? throw new InvalidOperationException("The saved create result has no original receipt identity.");
                        if (result.Receipt.IntentId != disclosure.IntentId.Value) {
                            throw new InvalidOperationException("The saved receipt does not match its durable proposal intent.");
                        }
                        await RequireReceiptAsync(disclosure.IntentId.Value, result.Receipt);
                        break;
                    case HrSimpleChatOperation.Receipt:
                        var original = disclosure.Result.ValueKind == JsonValueKind.Null ? null
                            : disclosure.Result.Deserialize<HrSimpleChatOriginalIdentity>(HrSimpleChatProposalCodec.SerializerOptions);
                        if (original is not null) {
                            var request = codec.Read<HrSimpleChatReceiptRequest>(disclosure.Payload);
                            await RequireReceiptAsync(request.IntentId, original);
                        }
                        break;
                }
            }

            EnsureCurrent(lease);
            await authorization.RequireResultDisclosureAsync(context, operation, lease.Identity, lease.CancellationToken);
            EnsureCurrent(lease);
            return lease;

            async Task RequireReceiptAsync(Guid intentId, HrSimpleChatOriginalIdentity expected) {
                var found = await receipts.FindReceiptAsync(CreateKey(session, intentId), lease.CancellationToken);
                EnsureSuccess(found);
                if (found.Value is not { } receipt || OriginalIdentity(receipt) != expected) {
                    throw new HrSimpleChatAdministrationException("hr-simple-chat.receipt-unavailable",
                        "The original owner receipt could not be confirmed for disclosure; the saved effect remains recorded.");
                }
            }
        } catch {
            await lease.DisposeAsync();
            throw;
        }
    }

    private async Task<T> ExecuteAsync<T>(AgentRuntimeToolProviderContext context, HrSimpleChatOperation operation,
        Func<AgentToolSessionAdmission, CancellationToken, Task<T>> action, CancellationToken cancellationToken) {
        if (HrSimpleChatRuntimeToolProvider.Unavailability(context, HrSimpleChatToolPolicy.Get(operation)) is { } unavailable) {
            throw new HrSimpleChatAdministrationException(unavailable.Code, unavailable.Message);
        }

        await using var lease = await leaseFactory.AcquireAsync(cancellationToken);
        EnsureCurrent(lease);
        using var scope = operationScope.Push(new(LlmChatOperationId.New(), lease.Identity));
        var policy = HrSimpleChatToolPolicy.Get(operation);
        var session = await authorization.RequireSessionAsync(context, policy, lease.Identity, lease.CancellationToken);
        EnsureCurrent(lease);
        var result = await action(session, lease.CancellationToken);
        EnsureCurrent(lease);
        await authorization.RequireSessionAsync(context, policy, lease.Identity, lease.CancellationToken);
        EnsureCurrent(lease);
        return result;
    }

    private async Task<LlmChatDefinitionDetails> ReadExpectedAsync(HrSimpleChatDefinitionVersion expected, CancellationToken cancellationToken) {
        var current = Require(await definitions.GetAsync(new(expected.DefinitionId), cancellationToken));
        if (current.Definition.Id.Value != expected.DefinitionId || current.Revision.DefinitionId.Value != expected.DefinitionId ||
            current.Definition.CurrentRevision.Value != expected.Revision ||
            current.Revision.Revision.Value != expected.Revision ||
            current.Definition.ConcurrencyToken != expected.ConcurrencyToken) {
            throw new HrSimpleChatAdministrationException("hr-simple-chat.revision-conflict",
                "The definition changed after the proposal. Review the current revision in a new proposal.");
        }

        return current;
    }

    private static LlmChatDefinitionCreateKey CreateKey(AgentToolSessionAdmission session, Guid intentId)
        => new(new(HrSimpleChatToolPolicy.CreateProducer, session.AgentId.ToString("N"),
            $"agent-chat:{session.Reference.ChatSessionId:N}"), new(intentId));

    private static HrSimpleChatOriginalIdentity OriginalIdentity(LlmChatDefinitionCreateReceipt receipt)
        => new(receipt.Key.IntentId.Value, receipt.DefinitionId.Value, receipt.DefinitionRevision.Value,
            receipt.OriginalConcurrencyToken, receipt.CreatedAtUtc);

    private static HrSimpleChatDefinitionSummary Summary(LlmChatDefinitionDetails details)
        => new(new(details.Definition.Id.Value, details.Definition.CurrentRevision.Value, details.Definition.ConcurrencyToken),
            details.Definition.Name, details.Definition.Summary, details.Definition.AvatarImageUrl,
            details.Definition.Status, details.NormalizedTags.ToImmutableArray());

    private static CreateLlmChatDefinitionCommand Settings(LlmChatDefinitionDetails details)
        => new(details.Definition.Name, details.Definition.Summary, details.Definition.AvatarImageUrl,
            details.Revision.SystemPrompt, details.Revision.ProviderProfileId, details.Revision.Model,
            details.Revision.Settings, details.Revision.Timeout, details.Revision.ResponseFormat,
            details.Revision.Reason, details.NormalizedTags.ToImmutableArray());

    private static void EnsureCurrent(ILlmChatRuntimeLease lease) {
        if (lease.EnsureCurrent().IsFailure) {
            throw new LlmChatRuntimeProfileChangedException();
        }

        lease.CancellationToken.ThrowIfCancellationRequested();
    }

    private static T Require<T>(Result<T> result) {
        EnsureSuccess(result);
        return result.Value ?? throw new InvalidOperationException("The Simple Chat owner returned no required result.");
    }

    private static void EnsureSuccess(Result result) {
        if (result.IsFailure) {
            var error = result.Errors.First();
            throw new HrSimpleChatAdministrationException(error.Code,
                "The Simple Chat owner rejected the operation. Review the validation or concurrency conflict before proposing another change.");
        }
    }
}
