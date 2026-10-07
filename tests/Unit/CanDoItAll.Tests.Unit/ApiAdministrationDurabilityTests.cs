using System.Security.Cryptography;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Modules.Workspace.ApiAccess.Contracts;
using CanDoItAll.Modules.Workspace.ApiAccess.Presentation;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using CanDoItAll.Workspace.ApiAccess.UI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Tests.Unit;

[Trait("Category", "HostPlatform")]
public sealed class ApiAdministrationDurabilityTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Registration_fault_before_or_after_commit_is_unknown_and_never_discloses_or_reissues(bool afterCommit) {
        await using var environment = CanDoItAllTestEnvironment.Create("api-registration-fault");
        var writes = 0;
        var writer = new DurableFileWriter(new PhysicalFileSystemPathPolicyFactory(), stage => {
            if (stage == (afterCommit ? DurableFileWriteStage.Committed : DurableFileWriteStage.BeforeCommit)) {
                writes++;
                throw new IOException("Injected acknowledgement fault.");
            }
        });
        var registry = new FileApiTokenRegistry(Paths(environment, writer), writer);
        var options = Options.Create(new ApiAccessOptions { Authorization = new() {
            Enabled = true, SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        } });
        var issuer = new ApiTokenService(options, new SystemClock(), registry);
        var access = new AllowedAccess();
        var owner = new ApiTokenOwner(new(issuer, registry, access, new SystemClock()));
        var configuration = await new ApiAccessConfigurationOwner(issuer, access, options).ReadAsync(CancellationToken.None);
        using var authority = new ApiViewLifetime();
        var ledger = new ApiOperationLedger();
        using var controller = new ApiTokenIssueController(owner, configuration, authority, ledger);
        await controller.IssueAsync();
        Assert.Equal(ApiWriteState.Unknown, controller.Outcome!.State);
        var id = controller.Outcome.Identity!.Value;
        Assert.Null(controller.Disclosure);
        Assert.Equal(afterCommit, await registry.FindAsync(id) is not null);
        await controller.ObserveAsync();
        await controller.IssueAsync();
        Assert.Equal(1, writes);
        Assert.Equal(ApiWriteState.Unknown, Assert.Single(ledger.Receipts).Result.State);
        Assert.False(controller.CanIssue);
    }

    [Theory]
    [InlineData(ApiWriteAction.CreateAccount, false)]
    [InlineData(ApiWriteAction.CreateAccount, true)]
    [InlineData(ApiWriteAction.UpdateAccount, false)]
    [InlineData(ApiWriteAction.UpdateAccount, true)]
    [InlineData(ApiWriteAction.ResetPassword, false)]
    [InlineData(ApiWriteAction.ResetPassword, true)]
    public async Task Account_writer_fault_has_a_candidate_and_no_false_confirmation(ApiWriteAction action, bool afterCommit) {
        await using var environment = CanDoItAllTestEnvironment.Create("api-account-write-fault");
        var armed = false;
        var writer = new DurableFileWriter(new PhysicalFileSystemPathPolicyFactory(), stage => {
            if (armed && stage == (afterCommit ? DurableFileWriteStage.Committed : DurableFileWriteStage.BeforeCommit)) {
                throw new IOException("Injected acknowledgement fault.");
            }
        });
        var store = new FileApiUserStore(Paths(environment, writer), writer);
        var service = new ApiUserAdministrationService(store, new(), new AllowedAccess(), Options.Create(new ApiAccessOptions()), new SystemClock(), NullLogger<ApiUserAdministrationService>.Instance);
        var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));
        ApiUserDetails? before = action == ApiWriteAction.CreateAccount ? null : await service.CreateAsync(new("durable-user", "Before", password, true, []));
        armed = true;
        var owner = new ApiAccountOwner(service);
        using var command = new ApiAccountIntent(action, before?.Id, before?.Version, "durable-user", "After", true, "", password + "!");
        var result = await owner.ApplyAsync(command, CancellationToken.None);
        Assert.Equal(ApiWriteState.Unknown, result.Outcome.State);
        Assert.NotNull(result.Outcome.Identity);
        Assert.Null(result.Account);
        var observed = await owner.ObserveAsync(result.Outcome.Identity.Value, CancellationToken.None);
        if (action == ApiWriteAction.CreateAccount) {
            Assert.Equal(afterCommit, observed is not null);
        } else {
            Assert.Equal(afterCommit ? 2 : 1, observed!.Version);
        }
        Assert.Empty(command.TakePassword());
    }

    [Theory]
    [InlineData(ApiWriteAction.CreateAccount)]
    [InlineData(ApiWriteAction.UpdateAccount)]
    [InlineData(ApiWriteAction.ResetPassword)]
    [InlineData(ApiWriteAction.DeleteAccount)]
    public async Task Presentation_preserves_committed_warning_from_the_real_account_owner(ApiWriteAction action) {
        await using var environment = CanDoItAllTestEnvironment.Create("api-account-warning");
        var writer = new DurableFileWriter(new PhysicalFileSystemPathPolicyFactory());
        var store = new FileApiUserStore(Paths(environment, writer), writer);
        var logger = new FaultLogger();
        var service = new ApiUserAdministrationService(store, new(), new AllowedAccess(), Options.Create(new ApiAccessOptions()), new SystemClock(), logger);
        var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));
        ApiUserDetails? before = action == ApiWriteAction.CreateAccount ? null : await service.CreateAsync(new("warning-user", "Before", password, true, []));
        logger.Fail = true;
        using var command = new ApiAccountIntent(action, before?.Id, before?.Version, "warning-user", "After", true, "", password);
        var result = await new ApiAccountOwner(service).ApplyAsync(command, CancellationToken.None);
        Assert.Equal(ApiWriteState.CommittedWithWarning, result.Outcome.State);
        Assert.Equal(ApiFailure.Diagnostic, result.Outcome.Failure);
        Assert.NotNull(result.Outcome.Identity);
        var observed = await new ApiAccountOwner(service).ObserveAsync(result.Outcome.Identity.Value, CancellationToken.None);
        if (action == ApiWriteAction.DeleteAccount) {
            Assert.Null(observed);
        } else {
            Assert.Equal(result.Account!.Id, observed!.Id);
            Assert.Equal(result.Account.Version, observed.Version);
        }
    }

    private static ControlPlanePathResolver Paths(CanDoItAllTestEnvironment environment, DurableFileWriter writer) =>
        new(Options.Create(new ControlPlaneOptions { RootPath = environment.ControlPlaneRootPath }), environment.CreateHostEnvironment(nameof(ApiAdministrationDurabilityTests)), writer);
    private sealed class AllowedAccess : IApiTokenAdministrationAccess {
        public ValueTask<bool> CanManageAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(true);
    }
    private sealed class FaultLogger : ILogger<ApiUserAdministrationService> {
        public bool Fail { get; set; }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
            if (Fail) {
                throw new IOException("Injected diagnostic failure.");
            }
        }
    }
}
