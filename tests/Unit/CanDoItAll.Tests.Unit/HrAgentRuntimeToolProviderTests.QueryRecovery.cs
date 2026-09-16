using System.Reflection;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed partial class HrAgentRuntimeToolProviderTests {
    private const string PrivateQueryFailure = "Private query diagnostics must not enter a saved tool failure.";
    private const string UnknownQueryErrorCode = "fixture.unrecognized-query-error";

    [Theory]
    [InlineData(HrAgentToolPolicy.HrCrmSearch, CrmHrAgentQueryErrorCodes.SearchRequired)]
    [InlineData(HrAgentToolPolicy.HrCrmSearch, CrmHrAgentQueryErrorCodes.SearchTooLong)]
    [InlineData(HrAgentToolPolicy.HrCrmSearch, CrmHrAgentQueryErrorCodes.TakeOutOfRange)]
    [InlineData(HrAgentToolPolicy.HrCrmItemSummaryGet, CrmHrAgentQueryErrorCodes.RecordKindInvalid)]
    [InlineData(HrAgentToolPolicy.HrCrmItemSummaryGet, CrmHrAgentQueryErrorCodes.RecordIdRequired)]
    [InlineData(HrAgentToolPolicy.HrCrmItemSummaryGet, CrmHrAgentQueryErrorCodes.RecordNotFound)]
    public async Task Known_owner_query_failures_have_bounded_no_effect_evidence(string toolName, string errorCode) {
        var fixture = CreateQueryFailureFixture([Error.Failure(PrivateQueryFailure, errorCode)]);

        var failure = await InvokeFailedQueryAsync(fixture, toolName);

        var evidence = Assert.IsAssignableFrom<IAgentToolFailureEffectEvidence>(failure);
        Assert.Equal(errorCode, evidence.ErrorCode);
        Assert.Equal(AgentToolEffectState.None, evidence.EffectState);
        Assert.True(evidence.IsSafeToExpose);
        Assert.True(evidence.CanRetryWithCorrectedInput);
        Assert.DoesNotContain(PrivateQueryFailure, evidence.SafeMessage, StringComparison.Ordinal);
        Assert.DoesNotContain(PrivateQueryFailure, failure.Message, StringComparison.Ordinal);
        Assert.Equal(1, fixture.Owner.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unknown_or_mixed_query_errors_are_not_claimed_as_known_no_effect_outcomes(bool mixed) {
        var unknown = Error.Failure(PrivateQueryFailure, UnknownQueryErrorCode);
        var fixture = CreateQueryFailureFixture(mixed
            ? [Error.Failure(PrivateQueryFailure, CrmHrAgentQueryErrorCodes.RecordNotFound), unknown]
            : [unknown]);

        var failure = await InvokeFailedQueryAsync(fixture, HrAgentToolPolicy.HrCrmItemSummaryGet);

        Assert.False(failure is IAgentToolFailure);
        Assert.Equal(1, fixture.Owner.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Thrown_query_infrastructure_and_cancellation_failures_propagate(bool cancelled) {
        Exception expected = cancelled ? new OperationCanceledException(PrivateQueryFailure) : new IOException(PrivateQueryFailure);
        var fixture = CreateQueryFailureFixture([], expected);

        var failure = await InvokeFailedQueryAsync(fixture, HrAgentToolPolicy.HrCrmItemSummaryGet);

        Assert.Same(expected, failure);
        Assert.False(failure is IAgentToolFailure);
        Assert.Equal(1, fixture.Owner.Calls);
    }

    [Theory]
    [InlineData(HrAgentToolPolicy.HrCrmSearch)]
    [InlineData(HrAgentToolPolicy.HrCrmItemSummaryGet)]
    public async Task Saved_query_failure_requires_typed_provenance_and_current_read_authority(string toolName) {
        var fixture = CreateQueryFailureFixture([Error.Failure(PrivateQueryFailure, CrmHrAgentQueryErrorCodes.RecordNotFound)]);
        var failure = Assert.IsAssignableFrom<IAgentToolFailureEffectEvidence>(await InvokeFailedQueryAsync(fixture, toolName));
        var metadata = Assert.Single(fixture.Provider.GetToolMetadata(fixture.Context), item => item.ToolName == toolName);
        var value = new AgentToolFailureResult(false, failure.ErrorCode, failure.SafeMessage, failure.CanRetryWithCorrectedInput) {
            EffectState = failure.EffectState
        };
        var saved = ManagedToolDisclosureTestData.Create(metadata, result: value, effectState: AgentToolEffectState.None) with {
            IsTypedFailure = true
        };
        var authorize = metadata.AuthorizeResultDisclosureAsync!;
        await using (var lease = await authorize(saved, default)) {
            Assert.Null(lease);
        }
        Assert.Equal(1, fixture.Owner.Calls);

        fixture.Workspace.Agents = [fixture.Context.Agent with { ConfigurationJson = "{}" }];
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => authorize(saved, default).AsTask());
        Assert.Equal(1, fixture.Owner.Calls);
        fixture.Workspace.Agents = [fixture.Context.Agent];
        await using (var lease = await authorize(saved, default)) {
            Assert.Null(lease);
        }
        await Assert.ThrowsAnyAsync<Exception>(() => authorize(saved with { IsTypedFailure = false }, default).AsTask());
        var privateValue = ManagedToolDisclosureTestData.Create(metadata,
            result: value with { Message = PrivateQueryFailure }, effectState: AgentToolEffectState.None) with { IsTypedFailure = true };
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => authorize(privateValue, default).AsTask());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => authorize(saved with { EffectState = AgentToolEffectState.Committed }, default).AsTask());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => authorize(saved with { EffectState = AgentToolEffectState.NotCommitted }, default).AsTask());
    }

    private static QueryFailureFixture CreateQueryFailureFixture(Error[] errors, Exception? fault = null) {
        var context = CreateContext(HrAgentCapabilityKeys.ToolNameToCapabilityKey.Values, allowCrmScope: true);
        var workspace = DispatchProxy.Create<IAgentFrameworkWorkspaceService, DisclosureWorkspace>();
        var state = (DisclosureWorkspace)(object)workspace;
        state.Agents = [context.Agent];
        state.Capabilities = context.Capabilities;
        var owner = new QueryFailureOwner(errors, fault);
        return new(CreateDisclosureProvider(workspace, owner, new ThrowingCrmPartyCommandService()), context, state, owner);
    }

    private static async Task<Exception> InvokeFailedQueryAsync(QueryFailureFixture fixture, string toolName) {
        var tools = await fixture.Provider.CreateToolsAsync(fixture.Context, default);
        var function = Assert.IsAssignableFrom<AIFunction>(Assert.Single(tools, item => item.Name == toolName));
        object request = toolName == HrAgentToolPolicy.HrCrmSearch
            ? new CrmHrAgentSearchQuery("query", CrmHrAgentRecordKind.Party)
            : new CrmHrAgentItemReference(CrmHrAgentRecordKind.Party, Guid.NewGuid());
        var arguments = new AIFunctionArguments {
            ["request"] = JsonSerializer.SerializeToElement(request, function.JsonSerializerOptions)
        };
        return await Assert.ThrowsAnyAsync<Exception>(async () => {
            await function.InvokeAsync(arguments);
        });
    }

    private sealed record QueryFailureFixture(HrAgentRuntimeToolProvider Provider, AgentRuntimeToolProviderContext Context,
        DisclosureWorkspace Workspace, QueryFailureOwner Owner);

    private sealed class QueryFailureOwner(Error[] errors, Exception? fault) : ICrmHrAgentQueryService {
        public int Calls { get; private set; }

        public Task<Result<IReadOnlyList<CrmHrAgentQueryItem>>> SearchAsync(CrmHrAgentSearchQuery query,
            CancellationToken cancellationToken = default) => ReadAsync<IReadOnlyList<CrmHrAgentQueryItem>>();

        public Task<Result<CrmHrAgentQueryItem>> GetSummaryAsync(CrmHrAgentItemReference reference,
            CancellationToken cancellationToken = default) => ReadAsync<CrmHrAgentQueryItem>();

        private Task<Result<T>> ReadAsync<T>() {
            Calls++;
            return fault is null ? Task.FromResult(Result<T>.Failure(errors)) : Task.FromException<Result<T>>(fault);
        }
    }
}
