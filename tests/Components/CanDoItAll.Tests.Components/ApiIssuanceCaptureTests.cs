using Bunit;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Modules.Workspace.Pages.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class ApiIssuanceCaptureTests {
    [Fact]
    public async Task Immediate_inputs_are_captured_before_held_access_and_later_fields_remain_separate() {
        var issuer = new RecordingIssuer();
        var access = new HeldAccess();
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.AddSingleton<IApiTokenService>(issuer);
            services.AddSingleton<IApiTokenAdministrationAccess>(access);
        });
        var cut = harness.Context.Render<WorkspaceApiAccessHost>();
        cut.WaitForElement("[data-testid='api-token-subject']");
        await cut.InvokeAsync(() => {
            cut.Find("[data-testid='api-token-subject']").Input("original-subject");
            cut.Find("[data-testid='api-token-name']").Input("Original name");
            cut.Find("#api-token-lifetime").Input("17");
            cut.Find("[data-testid='api-token-scopes']").Input(ApiAccessScopeNames.ReadRuntime);
        });
        access.Hold = true;
        Task issuing = Task.CompletedTask;
        await cut.InvokeAsync(() => { issuing = cut.Find("form").SubmitAsync(); });
        await access.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try {
            await cut.InvokeAsync(() => {
                cut.Find("[data-testid='api-token-subject']").Input("later-subject");
                cut.Find("[data-testid='api-token-name']").Input("Later name");
                cut.Find("#api-token-lifetime").Input("29");
                cut.Find("[data-testid='api-token-scopes']").Input(ApiAccessScopeNames.ReadAgents);
            });
        } finally {
            access.Release.TrySetResult(true);
        }
        await issuing;
        Assert.Equal("original-subject", issuer.Captured!.Subject);
        Assert.Equal("Original name", issuer.Captured.DisplayName);
        Assert.Equal(17, issuer.Captured.LifetimeMinutes);
        Assert.Equal([ApiAccessScopeNames.ReadRuntime], issuer.Captured.Scopes);
        Assert.Equal("later-subject", cut.Find("[data-testid='api-token-subject']").GetAttribute("value"));
        Assert.Equal("29", cut.Find("#api-token-lifetime").GetAttribute("value"));
    }

    private sealed class HeldAccess : IApiTokenAdministrationAccess {
        public bool Hold { get; set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ValueTask<bool> CanManageAsync(CancellationToken cancellationToken = default) {
            if (!Hold) {
                return ValueTask.FromResult(true);
            }
            Entered.TrySetResult();
            return new(Release.Task);
        }
    }

    private sealed class RecordingIssuer : IApiTokenService {
        public ApiTokenIssueRequest? Captured { get; private set; }
        public ApiAccessStatus GetStatus() => new(true, true, true, true, true, "fixture", "fixture", 30, 60);
        public ApiTokenIssueResult IssueToken(ApiTokenIssueRequest request) {
            Captured = request;
            return new("nonusable-form-fixture", "Bearer", DateTimeOffset.UtcNow.AddMinutes(17), request.Subject, request.DisplayName, request.Scopes);
        }
    }
}
