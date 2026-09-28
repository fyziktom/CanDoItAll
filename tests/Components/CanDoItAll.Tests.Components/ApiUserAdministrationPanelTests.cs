using Bunit;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Modules.Workspace.Pages.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class ApiUserAdministrationPanelTests {
    [Fact]
    public async Task Repeated_save_is_not_dispatched_and_committed_refresh_failure_is_reported_as_a_read_failure() {
        var store = new ControlledUserStore();
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.AddSingleton<IApiUserStore>(store);
            services.AddSingleton<IApiTokenAdministrationAccess>(new LocalAccess());
        });
        var cut = harness.Context.Render<ApiUserAdministrationPanel>();
        cut.WaitForElement("[data-testid='api-user-create']").Click();
        cut.Find("[data-testid='api-user-name']").Change("save-once");
        cut.Find("[data-testid='api-user-display-name']").Change("Save once");
        cut.Find("[data-testid='api-user-password']").Change(Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24)));
        var saving = cut.Find("[data-testid='api-user-save']").ClickAsync(new MouseEventArgs());
        await store.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try {
            Assert.True(cut.Find("[data-testid='api-user-save']").HasAttribute("disabled"));
            await cut.Find("[data-testid='api-user-save']").ClickAsync(new MouseEventArgs());
            Assert.Equal(1, store.SaveCalls);
        } finally {
            store.Release.TrySetResult();
        }
        await saving;
        cut.WaitForAssertion(() => {
            Assert.Empty(cut.FindAll("[data-testid='api-user-dialog']"));
            Assert.Contains("saved", cut.Find("[data-testid='api-users-message']").TextContent, StringComparison.Ordinal);
            Assert.Contains("refreshed", cut.Find("[data-testid='api-users-error']").TextContent, StringComparison.Ordinal);
        });
        Assert.Equal(1, store.SaveCalls);
        Assert.Equal("save-once", Assert.Single(store.Users).UserName);
        cut.Find("[data-testid='api-users-refresh']").Click();
        cut.WaitForAssertion(() => {
            Assert.Empty(cut.FindAll("[data-testid='api-users-error']"));
            Assert.Contains("save-once", cut.Find("[data-testid='api-users-table']").TextContent, StringComparison.Ordinal);
        });
        Assert.Equal(1, store.SaveCalls);
    }

    private sealed class LocalAccess : IApiTokenAdministrationAccess {
        public ValueTask<bool> CanManageAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(true);
    }

    private sealed class ControlledUserStore : IApiUserStore {
        private bool failRead;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<ApiUserRecord> Users { get; } = [];
        public int SaveCalls { get; private set; }

        public Task<IReadOnlyList<ApiUserRecord>> ReadAsync(CancellationToken cancellationToken = default) {
            if (failRead) {
                failRead = false;
                throw new IOException("Injected post-commit read failure.");
            }
            return Task.FromResult<IReadOnlyList<ApiUserRecord>>(Users.ToArray());
        }

        public async Task SaveAsync(ApiUserRecord user, long? expectedVersion, CancellationToken cancellationToken = default) {
            SaveCalls++;
            Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            Users.Add(user);
            failRead = true;
        }

        public Task DeleteAsync(Guid id, long expectedVersion, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
