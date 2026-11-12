using System.Globalization;
using System.Text.Json;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Tests.Support;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

[Trait("Category", "Playwright")]
[Trait("Category", "HostPlatform")]
public sealed class ProjectLifecyclePendingBrowserTests {
    private const string Origin = "https://projects-lifecycle-pending.test";
    private const string ModulePath = "/js/projectLifecyclePendingSubmissions.js";

    [Fact]
    public async Task First_entered_refusal_cannot_clear_original_evidence_before_owner_acceptance_or_closed_absence() {
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        IBrowser? browser = null;
        IBrowserContext? context = null;
        Exception? primary = null;
        try {
            browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
            context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1920, Height = 1080 } });
            var module = await File.ReadAllTextAsync(Path.Combine(PlaywrightTestHostPaths.RepositoryRoot,
                "src", "App", "CanDoItAll.Web", "wwwroot", "js", "projectLifecyclePendingSubmissions.js"));
            await context.RouteAsync(Origin + "/**", route => route.FulfillAsync(new() {
                ContentType = route.Request.Url.EndsWith(ModulePath, StringComparison.Ordinal) ? "text/javascript" : "text/html",
                Body = route.Request.Url.EndsWith(ModulePath, StringComparison.Ordinal) ? module : "<!doctype html><title>Lifecycle evidence storage proof</title>"
            }));
            var first = await PageAsync(context);
            var other = await PageAsync(context);
            var codec = new ProjectLifecycleRecoveryCodec();
            var original = Original(codec);
            var key = Key(original.Submission.Operation);
            var prepared = await first.EvaluateAsync<JsonElement>("x => pending.prepare(x)", Input(original));
            var attempt = Guid.NewGuid().ToString("D");
            await first.EvaluateAsync<JsonElement>("x => pending.beginDispatch(x.key, x.revision, x.attempt, false)",
                new { key, revision = Revision(prepared), attempt });
            var refused = await first.EvaluateAsync<JsonElement>("x => pending.observeAttempt(x.key, x.attempt, 'rejected')", new { key, attempt });
            Assert.Equal("unconfirmed", refused.GetProperty("record").GetProperty("stage").GetString());
            Assert.True(refused.GetProperty("record").GetProperty("hadUnconfirmedDispatch").GetBoolean());
            var clearFailure = await Assert.ThrowsAsync<PlaywrightException>(() => first.EvaluateAsync<JsonElement>(
                "x => pending.clearResolved(x.key, x.revision)", new { key, revision = Revision(refused) }));
            Assert.Contains("RemovalNotAllowed", clearFailure.Message);
            var cancelFailure = await Assert.ThrowsAsync<PlaywrightException>(() => other.EvaluateAsync<JsonElement>(
                "x => pending.cancelPrepared(x.key, x.revision)", new { key, revision = Revision(refused) }));
            Assert.Contains("RemovalNotAllowed", cancelFailure.Message);
            var beforeAcceptance = await other.EvaluateAsync<JsonElement>("x => pending.read(x)", key);
            Assert.Equal(original.Payload, beforeAcceptance.GetProperty("frozenRequest").GetString());
            Assert.Equal(long.MaxValue.ToString(CultureInfo.InvariantCulture), beforeAcceptance.GetProperty("key").GetProperty("epoch").GetString());
            Assert.Equal(Revision(refused), beforeAcceptance.GetProperty("revision").GetInt64());

            var now = DateTimeOffset.UtcNow;
            var deletion = Assert.IsType<ProjectLifecycleCommand.Delete>(original.Submission.Command);
            var accepted = new ProjectLifecycleAccepted(original.Submission, 1, now, now.AddDays(7),
                new ProjectLifecycleAcknowledgement.DeletionCommitted(new(new(new(original.Submission.Operation.DatabaseProfileId,
                    original.Submission.Operation.Namespace, new(Guid.NewGuid())), deletion.Expected.Project), [])));
            var payload = codec.EncodeAccepted(original.Submission, accepted).Payload;
            var stored = await other.EvaluateAsync<JsonElement>("x => pending.observeAccepted(x.key, x.original, x.accepted)",
                new { key, original = original.Payload, accepted = payload });
            Assert.Equal("accepted", stored.GetProperty("record").GetProperty("stage").GetString());
            Assert.Equal("unchanged", (await first.EvaluateAsync<JsonElement>(
                "x => pending.observeAttempt(x.key, x.attempt, 'rejected')", new { key, attempt })).GetProperty("status").GetString());
            var retained = await first.EvaluateAsync<JsonElement>("x => pending.read(x)", key);
            Assert.Equal(payload, retained.GetProperty("acceptedPayload").GetString());
            Assert.Equal(original.Payload, retained.GetProperty("frozenRequest").GetString());
            Assert.Equal("applied", (await first.EvaluateAsync<JsonElement>("x => pending.clearResolved(x.key, x.revision)",
                new { key, revision = Revision(stored) })).GetProperty("status").GetString());

            var absentOriginal = Original(codec);
            var absentKey = Key(absentOriginal.Submission.Operation);
            var absentPrepared = await first.EvaluateAsync<JsonElement>("x => pending.prepare(x)", Input(absentOriginal));
            var absentAttempt = Guid.NewGuid().ToString("D");
            await first.EvaluateAsync<JsonElement>("x => pending.beginDispatch(x.key, x.revision, x.attempt, false)",
                new { key = absentKey, revision = Revision(absentPrepared), attempt = absentAttempt });
            var absentRefusal = await first.EvaluateAsync<JsonElement>("x => pending.observeAttempt(x.key, x.attempt, 'rejected')",
                new { key = absentKey, attempt = absentAttempt });
            await Assert.ThrowsAsync<PlaywrightException>(() => first.EvaluateAsync<JsonElement>(
                "x => pending.clearResolved(x.key, x.revision)", new { key = absentKey, revision = Revision(absentRefusal) }));
            var absence = codec.EncodeClosedAbsent(absentOriginal.Submission,
                new(absentOriginal.Submission.Operation, now, now.AddMinutes(1), now.AddDays(7))).Payload;
            var closed = await other.EvaluateAsync<JsonElement>("x => pending.observeClosedAbsent(x.key, x.original, x.absence)",
                new { key = absentKey, original = absentOriginal.Payload, absence });
            Assert.Equal("closedAbsent", closed.GetProperty("record").GetProperty("stage").GetString());
            Assert.Equal("applied", (await first.EvaluateAsync<JsonElement>("x => pending.clearResolved(x.key, x.revision)",
                new { key = absentKey, revision = Revision(closed) })).GetProperty("status").GetString());
            Assert.Equal(JsonValueKind.Null, (await other.EvaluateAsync<JsonElement>("x => pending.read(x)", absentKey)).ValueKind);
        } catch (Exception failure) {
            primary = failure;
            throw;
        } finally {
            if (primary is null) {
                await TestFixtureCleanup.DisposeAsync(context, browser);
            } else {
                await TestFixtureCleanup.DisposeAfterFailureAsync(primary, context, browser);
            }
        }
    }

    private static async Task<IPage> PageAsync(IBrowserContext context) {
        var page = await context.NewPageAsync();
        page.SetDefaultTimeout(15_000);
        await page.GotoAsync(Origin);
        await page.EvaluateAsync("async path => { window.pending = await import(path); }", ModulePath);
        await page.EvaluateAsync("x => pending.initialize(x)", new {
            maximumRecordCount = 4, maximumTotalBytes = 131_072, maximumFrozenRequestBytes = 4096,
            maximumAcceptedBytes = 16_384, maximumPageSize = 4
        });
        return page;
    }

    private static ProjectLifecycleFrozenSubmission Original(ProjectLifecycleRecoveryCodec codec) {
        var profile = Guid.NewGuid();
        return codec.Freeze(new(new(profile, new(Guid.NewGuid()), new(long.MaxValue), new("pc1:" + new string('a', 64)), Guid.NewGuid()),
            ProjectLifecycleInterpretation.VersionOne, new ProjectLifecycleCommand.Delete(new(new(profile, Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()))));
    }

    private static object Input(ProjectLifecycleFrozenSubmission original) => new {
        key = Key(original.Submission.Operation), frozenRequest = original.Payload, kind = (int)original.Submission.Command.Kind,
        targetProjectIds = new[] { Assert.IsType<ProjectLifecycleCommand.Delete>(original.Submission.Command).Expected.Project.ProjectId.ToString("D") }
    };

    private static object Key(ProjectLifecycleOperation operation) => new {
        databaseProfileId = operation.DatabaseProfileId.ToString("D"), @namespace = operation.Namespace.Value.ToString("D"),
        callerBinding = operation.CallerBinding.Value, epoch = operation.Epoch.Value.ToString(CultureInfo.InvariantCulture), id = operation.Id.ToString("D")
    };

    private static long Revision(JsonElement change) => change.GetProperty("record").GetProperty("revision").GetInt64();
}
