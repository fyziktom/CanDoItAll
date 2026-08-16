using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.LlmChats.Application;
using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Definitions;
using CanDoItAll.Modules.LlmChats.Operations;
using CanDoItAll.Modules.LlmChats.Ports;
using CanDoItAll.Modules.LlmChats.Ui;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using System.Xml.Linq;

namespace CanDoItAll.Tests.Unit.LlmChats;

public sealed class LlmChatUiAuthorizationFacadeTests
{
    [Fact]
    public async Task Snapshot_evaluates_read_manage_and_execute_independently()
    {
        var evaluator = new StubPolicyEvaluator(
            LlmChatUiPermission.Read,
            LlmChatUiPermission.Execute);
        var facade = new LlmChatUiAuthorizationFacade(evaluator);

        var snapshot = await facade.GetAsync();

        Assert.True(snapshot.CanRead);
        Assert.False(snapshot.CanManage);
        Assert.True(snapshot.CanExecute);
        Assert.Equal(
            [
                LlmChatUiPermission.Read,
                LlmChatUiPermission.Manage,
                LlmChatUiPermission.Execute
            ],
            evaluator.Requests);
    }

    private sealed class StubPolicyEvaluator(params LlmChatUiPermission[] allowed)
        : ILlmChatUiPolicyEvaluator
    {
        private readonly HashSet<LlmChatUiPermission> allowed = [.. allowed];

        public List<LlmChatUiPermission> Requests { get; } = [];

        public ValueTask<bool> IsAllowedAsync(
            LlmChatUiPermission permission,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(permission);
            return ValueTask.FromResult(allowed.Contains(permission));
        }
    }
}

public sealed class LlmChatDefinitionUiGatewayTests
{
    [Fact]
    public async Task Read_projection_never_exposes_system_prompt()
    {
        var details = CreateDefinitionDetails("do not expose this system prompt");
        var service = new StubDefinitionService(details);
        var gateway = new LlmChatDefinitionUiGateway(
            service,
            new FixedAuthorizationFacade(canRead: true, canManage: false, canExecute: false));

        var result = await gateway.ListPageAsync(new LlmChatDefinitionQuery());

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value!.Items);
        Assert.Equal(details.Definition.Name, item.Name);
        Assert.DoesNotContain(
            typeof(LlmChatDefinitionListItem).GetProperties(),
            property => property.Name.Contains("Prompt", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            "do not expose this system prompt",
            item.ToString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Editor_requires_manage_without_calling_application_service()
    {
        var service = new StubDefinitionService(CreateDefinitionDetails("system prompt"));
        var gateway = new LlmChatDefinitionUiGateway(
            service,
            new FixedAuthorizationFacade(canRead: true, canManage: false, canExecute: false));

        var result = await gateway.GetEditorAsync(Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal(LlmChatUiFailureCodes.Forbidden, Assert.Single(result.Failures).Code);
        Assert.Equal(0, service.GetCalls);
    }

    [Fact]
    public async Task Unknown_application_failure_is_sanitized()
    {
        var service = new StubDefinitionService(
            Result<LlmChatDefinitionDetails>.Failure(
                Error.Failure("provider body contains secret-token", "provider.internal.failure")));
        var gateway = new LlmChatDefinitionUiGateway(
            service,
            new FixedAuthorizationFacade(canRead: true, canManage: true, canExecute: true));

        var result = await gateway.GetEditorAsync(Guid.NewGuid());

        Assert.True(result.IsFailure);
        var failure = Assert.Single(result.Failures);
        Assert.Equal(LlmChatUiFailureCodes.RequestFailed, failure.Code);
        Assert.DoesNotContain("provider", failure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret-token", failure.Message, StringComparison.Ordinal);
    }

    private static LlmChatDefinitionDetails CreateDefinitionDetails(string systemPrompt)
    {
        var now = DateTimeOffset.Parse("2026-08-16T12:00:00Z");
        var id = LlmChatDefinitionId.New();
        var definition = new LlmChatDefinition(
            id,
            "Research assistant",
            "Summarizes source material.",
            string.Empty,
            LlmChatDefinitionStatus.Active,
            new LlmChatDefinitionRevisionNumber(1),
            now,
            now,
            3);
        var revision = new LlmChatDefinitionRevision(
            id,
            new LlmChatDefinitionRevisionNumber(1),
            definition.Name,
            definition.Summary,
            definition.AvatarImageUrl,
            systemPrompt,
            Guid.NewGuid(),
            ProviderKind.OpenAi,
            "Primary provider",
            "gpt-test",
            new LlmModelSettings(0.2),
            TimeSpan.FromMinutes(1),
            null,
            now,
            "Initial version");
        return new LlmChatDefinitionDetails(definition, revision, ["research"]);
    }

    private sealed class StubDefinitionService : ILlmChatDefinitionApplicationService
    {
        private readonly Result<LlmChatDefinitionDetails> result;

        public StubDefinitionService(LlmChatDefinitionDetails details)
            : this(Result<LlmChatDefinitionDetails>.Success(details))
        {
        }

        public StubDefinitionService(Result<LlmChatDefinitionDetails> result)
        {
            this.result = result;
        }

        public int GetCalls { get; private set; }

        public Task<Result<LlmChatDefinitionDetails>> CreateAsync(
            CreateLlmChatDefinitionCommand command,
            CancellationToken cancellationToken = default)
            => Task.FromResult(result);

        public Task<Result<LlmChatDefinitionDetails>> UpdateAsync(
            UpdateLlmChatDefinitionCommand command,
            CancellationToken cancellationToken = default)
            => Task.FromResult(result);

        public Task<Result<LlmChatDefinitionDetails>> ChangeStatusAsync(
            ChangeLlmChatDefinitionStatusCommand command,
            CancellationToken cancellationToken = default)
            => Task.FromResult(result);

        public Task<Result<LlmChatDefinitionDetails>> GetAsync(
            LlmChatDefinitionId definitionId,
            CancellationToken cancellationToken = default)
        {
            GetCalls++;
            return Task.FromResult(result);
        }

        public Task<Result<IReadOnlyList<LlmChatDefinitionDetails>>> ListAsync(
            LlmChatDefinitionQuery query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(result.IsSuccess
                ? Result<IReadOnlyList<LlmChatDefinitionDetails>>.Success([result.Value!])
                : Result<IReadOnlyList<LlmChatDefinitionDetails>>.Failure(result.Errors));

        public Task<Result<LlmChatPage<LlmChatDefinitionDetails, LlmChatDefinitionCursor>>> ListPageAsync(
            LlmChatDefinitionQuery query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(result.IsSuccess
                ? Result<LlmChatPage<LlmChatDefinitionDetails, LlmChatDefinitionCursor>>.Success(
                    new([result.Value!], null))
                : Result<LlmChatPage<LlmChatDefinitionDetails, LlmChatDefinitionCursor>>.Failure(result.Errors));
    }
}

public sealed class LlmChatOperationProjectionReducerTests
{
    [Fact]
    public void Duplicate_events_do_not_duplicate_transient_text()
    {
        var operationId = LlmChatOperationId.New();
        var page = new LlmChatUiOperationEventPage(
            operationId.Value,
            LlmChatOperationStatus.Running,
            false,
            string.Empty,
            [
                new LlmChatOperationAttemptStartedEvent(
                    operationId,
                    1,
                    1,
                    "gpt-test",
                    LlmStreamingDeliveryMode.Incremental,
                    DateTimeOffset.UtcNow),
                new LlmChatOperationTextDeltaEvent(
                    operationId,
                    2,
                    1,
                    "Hello",
                    DateTimeOffset.UtcNow)
            ],
            1,
            2);
        var reducer = new LlmChatOperationProjectionReducer();

        var once = reducer.Reduce(LlmChatOperationProjectionState.Initial(operationId.Value), page);
        var twice = reducer.Reduce(once, page);

        Assert.Equal("Hello", twice.TransientAssistantText);
        Assert.Equal(2, twice.Cursor);
        Assert.False(twice.RequiresAuthoritativeRefresh);
    }

    [Fact]
    public void Retention_gap_discards_transient_text_and_requires_authoritative_refresh()
    {
        var operationId = LlmChatOperationId.New();
        var state = LlmChatOperationProjectionState.Initial(operationId.Value) with
        {
            Cursor = 2,
            ActiveAttemptOrdinal = 1,
            TransientAssistantText = "stale partial"
        };
        var page = new LlmChatUiOperationEventPage(
            operationId.Value,
            LlmChatOperationStatus.Running,
            false,
            string.Empty,
            [
                new LlmChatOperationTextDeltaEvent(
                    operationId,
                    5,
                    1,
                    "unusable delta",
                    DateTimeOffset.UtcNow)
            ],
            5,
            5);

        var next = new LlmChatOperationProjectionReducer().Reduce(state, page);

        Assert.True(next.RequiresAuthoritativeRefresh);
        Assert.Empty(next.TransientAssistantText);
        Assert.Equal(5, next.Cursor);
    }
}

public sealed class LlmChatUiEventSessionGatewayTests
{
    [Fact]
    public async Task Disposing_follower_disposes_only_session_and_never_issues_cancel()
    {
        var operationId = LlmChatOperationId.New();
        var session = new StubEventSession(operationId);
        var source = new StubEventSessionSource(session);
        var gateway = new LlmChatUiEventSessionGateway(
            source,
            new FixedAuthorizationFacade(canRead: true, canManage: false, canExecute: false));

        var opened = await gateway.OpenAsync(operationId.Value);
        await opened.Value!.DisposeAsync();

        Assert.True(session.IsDisposed);
        Assert.Equal(operationId, source.OpenedOperationId);
        Assert.DoesNotContain(
            typeof(LlmChatUiEventSessionGateway).GetConstructors().Single().GetParameters(),
            parameter => parameter.ParameterType == typeof(ILlmChatOperationApplicationService));
    }

    private sealed class StubEventSessionSource(ILlmChatOperationEventSession session)
        : ILlmChatOperationEventSessionSource
    {
        public LlmChatOperationId OpenedOperationId { get; private set; }

        public ValueTask<Result<ILlmChatOperationEventSession>> OpenAsync(
            LlmChatOperationId operationId,
            CancellationToken cancellationToken = default)
        {
            OpenedOperationId = operationId;
            return ValueTask.FromResult(Result<ILlmChatOperationEventSession>.Success(session));
        }
    }

    private sealed class StubEventSession(LlmChatOperationId operationId)
        : ILlmChatOperationEventSession
    {
        public CancellationToken ProfileLifetime => CancellationToken.None;

        public int MaximumPageSize => 100;

        public bool IsDisposed { get; private set; }

        public ValueTask<LlmChatOperationEventPage> ReadAsync(
            long afterSequence,
            int take,
            TimeSpan maximumWait,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new LlmChatOperationEventPage(
                CreateOperation(operationId),
                [],
                null,
                0,
                0));

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private static LlmChatOperation CreateOperation(LlmChatOperationId operationId)
        => new(
            operationId,
            LlmChatConversationId.New(),
            LlmChatOperationKind.SendTurn,
            new LlmChatRequestFingerprint(new string('a', 64)),
            0,
            LlmChatOperationStatus.Pending,
            DateTimeOffset.UtcNow,
            0);
}

public sealed class LlmChatUiRegistrationAndArchitectureTests
{
    [Fact]
    public void Focused_registration_adds_gateways_without_http_clients_or_service_location()
    {
        var services = new ServiceCollection();

        services.AddLlmChatsUi();

        Assert.Contains(services, item => item.ServiceType == typeof(ILlmChatDefinitionUiGateway));
        Assert.Contains(services, item => item.ServiceType == typeof(ILlmChatConversationUiGateway));
        Assert.Contains(services, item => item.ServiceType == typeof(ILlmChatOperationUiGateway));
        Assert.Contains(services, item => item.ServiceType == typeof(ILlmChatProviderUiGateway));
        Assert.Contains(services, item => item.ServiceType == typeof(ILlmChatUiEventSessionGateway));
        Assert.DoesNotContain(services, item => item.ServiceType == typeof(HttpClient));
        Assert.DoesNotContain(
            typeof(LlmChatsUiServiceCollectionExtensions).Assembly.GetTypes()
                .SelectMany(type => type.GetConstructors())
                .SelectMany(constructor => constructor.GetParameters()),
            parameter => parameter.ParameterType == typeof(IServiceProvider));
    }

    [Fact]
    public void Ui_project_has_only_allowed_project_references_and_forbidden_source_is_absent()
    {
        var root = FindRepositoryRoot();
        var projectDirectory = Path.Combine(
            root,
            "src",
            "Modules",
            "CanDoItAll.Modules.LlmChats.Ui");
        var project = XDocument.Load(Path.Combine(
            projectDirectory,
            "CanDoItAll.Modules.LlmChats.Ui.csproj"));
        var references = project.Descendants("ProjectReference")
            .Select(element => Path.GetFullPath(Path.Combine(
                projectDirectory,
                element.Attribute("Include")!.Value)))
            .Select(path => Path.GetFileNameWithoutExtension(path)!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                "CanDoItAll.AppComponents",
                "CanDoItAll.Conversations.Components",
                "CanDoItAll.Modules.LlmChats"
            ],
            references);

        var source = string.Join(
            '\n',
            Directory.EnumerateFiles(projectDirectory, "*.*", SearchOption.AllDirectories)
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .Where(path => Path.GetExtension(path) is ".cs" or ".razor")
                .Select(File.ReadAllText));
        foreach (var forbidden in new[]
        {
            "CanDoItAll.Web",
            "LlmChats.Persistence",
            "AgentFramework.Core",
            "AgentFramework.Tool",
            "AgentFramework.Skill",
            "AgentFramework.Voice",
            "HttpClient",
            "IServiceProvider"
        })
        {
            Assert.DoesNotContain(forbidden, source, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("@page", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/chats", source, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root from the test output directory.");
    }
}

internal sealed class FixedAuthorizationFacade(
    bool canRead,
    bool canManage,
    bool canExecute) : ILlmChatUiAuthorizationFacade
{
    private readonly LlmChatUiAuthorizationSnapshot snapshot = new(canRead, canManage, canExecute);

    public ValueTask<LlmChatUiAuthorizationSnapshot> GetAsync(
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult(snapshot);

    public ValueTask<bool> IsAllowedAsync(
        LlmChatUiPermission permission,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult(permission switch
        {
            LlmChatUiPermission.Read => snapshot.CanRead,
            LlmChatUiPermission.Manage => snapshot.CanManage,
            LlmChatUiPermission.Execute => snapshot.CanExecute,
            _ => throw new ArgumentOutOfRangeException(nameof(permission), permission, "Unknown permission.")
        });
}
