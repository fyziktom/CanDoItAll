using System.Security.Cryptography;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Tests.Unit;

[Trait("Category", "HostPlatform")]
public sealed class ApiAdministrationOutcomeTests {
    [Fact]
    public async Task Issuance_captures_every_field_and_scope_before_permission_yields() {
        await using var environment = CanDoItAllTestEnvironment.Create("api-capture");
        var writer = new DurableFileWriter(new PhysicalFileSystemPathPolicyFactory());
        var registry = new FileApiTokenRegistry(Paths(environment, writer), writer);
        var access = new HeldAccess();
        var issuer = new CapturingIssuer();
        var service = new ApiTokenAdministrationService(issuer, registry, access, new SystemClock());
        var request = new ApiTokenIssueRequest { Subject = "original", DisplayName = "Original", LifetimeMinutes = 17, Scopes = [ApiAccessScopeNames.ReadRuntime] };
        var issuing = service.IssueAsync(request);
        await access.Entered.Task;
        request.Subject = "later";
        request.DisplayName = "Later";
        request.LifetimeMinutes = 29;
        request.Scopes[0] = ApiAccessScopeNames.ReadAgents;
        access.Release.SetResult(true);
        await issuing;
        Assert.Equal("original", issuer.Captured!.Subject);
        Assert.Equal("Original", issuer.Captured.DisplayName);
        Assert.Equal(17, issuer.Captured.LifetimeMinutes);
        Assert.Equal([ApiAccessScopeNames.ReadRuntime], issuer.Captured.Scopes);
    }

    [Theory]
    [InlineData(AccountWrite.Create)]
    [InlineData(AccountWrite.Update)]
    [InlineData(AccountWrite.Reset)]
    [InlineData(AccountWrite.Delete)]
    public async Task A_logger_fault_after_durable_write_does_not_lose_the_known_result(AccountWrite action) {
        await using var environment = CanDoItAllTestEnvironment.Create("api-log-outcome");
        var writer = new DurableFileWriter(new PhysicalFileSystemPathPolicyFactory());
        var store = new FileApiUserStore(Paths(environment, writer), writer);
        var logger = new FaultLogger();
        var service = new ApiUserAdministrationService(store, new ApiPasswordService(), new AllowedAccess(), Options.Create(new ApiAccessOptions()), new SystemClock(), logger);
        var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));
        ApiUserDetails? before = null;
        if (action != AccountWrite.Create) {
            before = await service.CreateAsync(new("outcome-user", "Before", password, true, []));
        }
        logger.Fail = true;
        ApiUserDetails? result = null;
        var failure = await Record.ExceptionAsync(async () => {
            switch (action) {
                case AccountWrite.Create:
                    result = await service.CreateAsync(new("outcome-user", "Created", password, true, []));
                    break;
                case AccountWrite.Update:
                    result = await service.UpdateAsync(before!.Id, new(before.UserName, "Updated", false, [], before.Version));
                    break;
                case AccountWrite.Reset:
                    result = await service.ResetPasswordAsync(before!.Id, new(password + "!", before.Version));
                    break;
                case AccountWrite.Delete:
                    await service.DeleteAsync(before!.Id, before.Version);
                    break;
            }
        });
        var reopened = new FileApiUserStore(Paths(environment, writer), writer);
        if (action == AccountWrite.Delete) {
            Assert.Empty(await reopened.ReadAsync());
        } else {
            var saved = Assert.Single(await reopened.ReadAsync());
            Assert.Equal(action == AccountWrite.Create ? 1 : 2, saved.Version);
            if (result is not null) {
                Assert.Equal(saved.Id, result.Id);
                Assert.Equal(saved.Version, result.Version);
            }
        }
        Assert.Null(failure);
        if (action != AccountWrite.Delete) {
            Assert.NotNull(result);
        }
    }

    public enum AccountWrite { Create, Update, Reset, Delete }

    private static ControlPlanePathResolver Paths(CanDoItAllTestEnvironment environment, DurableFileWriter writer) =>
        new(Options.Create(new ControlPlaneOptions { RootPath = environment.ControlPlaneRootPath }), environment.CreateHostEnvironment(nameof(ApiAdministrationOutcomeTests)), writer);

    private sealed class AllowedAccess : IApiTokenAdministrationAccess {
        public ValueTask<bool> CanManageAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(true);
    }

    private sealed class HeldAccess : IApiTokenAdministrationAccess {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ValueTask<bool> CanManageAsync(CancellationToken cancellationToken = default) {
            Entered.TrySetResult();
            return new(Release.Task);
        }
    }

    private sealed class CapturingIssuer : IApiTokenService {
        public ApiTokenIssueRequest? Captured { get; private set; }
        public ApiAccessStatus GetStatus() => throw new NotSupportedException();
        public ApiTokenIssueResult IssueToken(ApiTokenIssueRequest request) {
            Captured = request;
            return new("nonusable-fixture", "Bearer", DateTimeOffset.UtcNow, request.Subject, request.DisplayName, request.Scopes);
        }
    }

    private sealed class FaultLogger : ILogger<ApiUserAdministrationService> {
        public bool Fail { get; set; }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
            if (Fail) {
                throw new IOException("Injected diagnostic sink fault.");
            }
        }
    }
}
