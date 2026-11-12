using CanDoItAll.Modules.CrmHr;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration.CrmHr;

public sealed class CrmHrRecruitingNativeBoundaryTests {
    [Theory]
    [InlineData(WriteKind.Application)]
    [InlineData(WriteKind.Interview)]
    [InlineData(WriteKind.Task)]
    [InlineData(WriteKind.Conversion)]
    public async Task Native_commands_freeze_their_submission_before_the_first_asynchronous_database_boundary(WriteKind kind) {
        var gate = new ContextGate();
        await using var application = await TestApplication.CreateAsync(new() {
            ConfigureServices = services => {
                services.RemoveAll<IDbContextFactory<CrmHrDbContext>>();
                services.AddSingleton<IDbContextFactory<CrmHrDbContext>>(provider => new GatedFactory(provider.GetRequiredService<DbContextOptions<CrmHrDbContext>>(), gate));
            }
        });
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var recruiting = services.GetRequiredService<RecruitingService>();
        var selected = await CrmHrRecruitingConcurrencyTests.CreateApplicationAsync(services);
        var shown = await recruiting.GetRecruitmentWorkspaceAsync(selected);
        var partyId = shown.Application.PartyId!.Value;
        shown.Application.Notes = "Submitted application";
        shown.Conversion.JobTitle = "Submitted conversion";
        var interview = new RecruitmentInterviewEditorModel {
            ApplicationId = selected, ScheduledAtLocal = new(2026, 11, 5, 10, 0, 0), Feedback = "Submitted interview"
        };
        var task = CrmHrRecruitingConcurrencyTests.NewTask(partyId, partyId);
        task.Title = "Submitted task";
        gate.Hold = true;
        var write = kind switch {
            WriteKind.Application => recruiting.SaveRecruitmentApplicationAsync(shown.Application),
            WriteKind.Interview => recruiting.SaveRecruitmentInterviewAsync(interview),
            WriteKind.Task => recruiting.SaveLifecycleTaskAsync(task),
            _ => recruiting.ConvertCandidateAsync(shown.Conversion)
        };
        try {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            shown.Application.Notes = "Later application";
            shown.Conversion.JobTitle = "Later conversion";
            shown.Conversion.ApplicationId = Guid.NewGuid();
            interview.Feedback = "Later interview";
            interview.ApplicationId = Guid.NewGuid();
            task.Title = "Later task";
            task.PartyId = Guid.NewGuid();
        } finally {
            gate.Release.TrySetResult();
        }

        Assert.True((await write).IsSuccess);
        var current = await recruiting.GetRecruitmentWorkspaceAsync(selected);
        switch (kind) {
            case WriteKind.Application:
                Assert.Equal("Submitted application", current.Application.Notes);
                break;
            case WriteKind.Interview:
                Assert.Equal("Submitted interview", Assert.Single(current.Interviews).Feedback);
                break;
            case WriteKind.Task:
                Assert.Equal("Submitted task", Assert.Single(current.LifecycleTasks).Title);
                break;
            case WriteKind.Conversion:
                Assert.Equal("Submitted conversion", (await services.GetRequiredService<IWorkforceProfileEditing>().GetWorkforceProfileWorkspaceAsync(partyId))!.Profile.JobTitle);
                break;
        }
    }

    [Theory]
    [InlineData(WriteKind.Application, false)]
    [InlineData(WriteKind.Interview, false)]
    [InlineData(WriteKind.Task, false)]
    [InlineData(WriteKind.Conversion, false)]
    [InlineData(WriteKind.DeleteInterview, false)]
    [InlineData(WriteKind.DeleteTask, false)]
    [InlineData(WriteKind.Application, true)]
    [InlineData(WriteKind.Interview, true)]
    [InlineData(WriteKind.Task, true)]
    [InlineData(WriteKind.Conversion, true)]
    [InlineData(WriteKind.DeleteInterview, true)]
    [InlineData(WriteKind.DeleteTask, true)]
    public async Task Native_commit_authority_and_acceptance_distinguish_a_rollback_from_a_committed_followup_failure(WriteKind kind, bool afterCommit) {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var recruiting = services.GetRequiredService<RecruitingService>();
        var selected = await CrmHrRecruitingConcurrencyTests.CreateApplicationAsync(services);
        var workspace = await recruiting.GetRecruitmentWorkspaceAsync(selected);
        var partyId = workspace.Application.PartyId!.Value;
        Assert.True((await recruiting.SaveRecruitmentInterviewAsync(new() {
            ApplicationId = selected, ScheduledAtLocal = new(2026, 11, 5, 10, 0, 0), Feedback = "Original feedback"
        })).IsSuccess);
        Assert.True((await recruiting.SaveLifecycleTaskAsync(CrmHrRecruitingConcurrencyTests.NewTask(partyId, partyId))).IsSuccess);
        workspace = await recruiting.GetRecruitmentWorkspaceAsync(selected);
        var interview = Assert.Single(workspace.Interviews);
        var task = Assert.Single(workspace.LifecycleTasks);
        workspace.Application.Notes = "Accepted application notes";
        workspace.Conversion.JobTitle = "Accepted conversion title";
        var accepted = false;
        void Acknowledge() {
            accepted = true;
            if (afterCommit) {
                throw new DbUpdateConcurrencyException("A postcommit continuation failed.");
            }
        }
        Func<Task> write = kind switch {
            WriteKind.Application => async () => {
                await recruiting.SaveRecruitmentApplicationAsync(workspace.Application, onCommitted: _ => Acknowledge());
            },
            WriteKind.Interview => async () => {
                await recruiting.SaveRecruitmentInterviewAsync(new() {
                    Id = interview.Id, ApplicationId = selected, ScheduledAtLocal = interview.ScheduledAtUtc.LocalDateTime,
                    InterviewType = interview.InterviewType, Feedback = "Accepted feedback", Original = interview.Original
                }, onCommitted: _ => Acknowledge());
            },
            WriteKind.Task => async () => {
                await recruiting.SaveLifecycleTaskAsync(new() {
                    Id = task.Id, PartyId = partyId, OwnerPartyId = partyId, DueDate = task.DueDate,
                    Title = "Accepted task", Original = task.Original
                }, onCommitted: _ => Acknowledge());
            },
            WriteKind.Conversion => async () => {
                await recruiting.ConvertCandidateAsync(workspace.Conversion, onCommitted: _ => Acknowledge());
            },
            WriteKind.DeleteInterview => async () => {
                await recruiting.DeleteRecruitmentInterviewAsync(interview.Id, original: interview.Original, onCommitted: _ => Acknowledge());
            },
            _ => async () => {
                await recruiting.DeleteLifecycleTaskAsync(task.Id, original: task.Original, onCommitted: _ => Acknowledge());
            }
        };
        var checks = 0;
        using var admission = services.GetRequiredService<CrmHrChanges>().EnterUiWrite(_ => {
            if (!afterCommit && ++checks == 3) {
                throw new UnauthorizedAccessException("Current write authority was revoked after SQL.");
            }
            return ValueTask.CompletedTask;
        });

        var error = await Record.ExceptionAsync(write);

        if (afterCommit) {
            Assert.IsType<DbUpdateConcurrencyException>(error);
        } else {
            Assert.IsType<UnauthorizedAccessException>(error);
            Assert.Equal(3, checks);
        }
        Assert.Equal(afterCommit, accepted);
        var current = await recruiting.GetRecruitmentWorkspaceAsync(selected);
        switch (kind) {
            case WriteKind.Application:
                Assert.Equal(afterCommit ? "Accepted application notes" : "", current.Application.Notes);
                break;
            case WriteKind.Interview:
                Assert.Equal(afterCommit ? "Accepted feedback" : "Original feedback", Assert.Single(current.Interviews).Feedback);
                break;
            case WriteKind.Task:
                Assert.Equal(afterCommit ? "Accepted task" : "Original lifecycle task", Assert.Single(current.LifecycleTasks).Title);
                break;
            case WriteKind.Conversion:
                Assert.Equal(afterCommit ? RecruitmentStage.Hired : RecruitmentStage.Offer, current.Application.Stage);
                Assert.Equal(afterCommit, current.HasWorkforceProfile);
                var party = (await services.GetRequiredService<PartyDirectoryService>().GetPartyAsync(partyId))!;
                Assert.Equal(afterCommit ? PartyLifecycleStatus.Active : PartyLifecycleStatus.Candidate, party.LifecycleStatus);
                break;
            case WriteKind.DeleteInterview:
                Assert.Equal(afterCommit ? 0 : 1, current.Interviews.Count);
                break;
            case WriteKind.DeleteTask:
                Assert.Equal(afterCommit ? 0 : 1, current.LifecycleTasks.Count);
                break;
        }
    }

    [Fact]
    public async Task A_rejected_inline_application_leaves_no_party_application_or_audit_fragment() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var checks = 0;
        using var admission = services.GetRequiredService<CrmHrChanges>().EnterUiWrite(_ => {
            if (++checks == 3) {
                throw new UnauthorizedAccessException("Revoked after the actual combined SQL flush.");
            }
            return ValueTask.CompletedTask;
        });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => services.GetRequiredService<RecruitingService>().SaveRecruitmentApplicationAsync(new() {
            CandidateName = "Rollback inline candidate", DesiredRole = "Must roll back", CandidateEmail = "rollback@example.test"
        }));

        await using var context = await services.GetRequiredService<IDbContextFactory<CrmHrDbContext>>().CreateDbContextAsync();
        Assert.Empty(await context.Set<Party>().Where(row => row.DisplayName == "Rollback inline candidate").ToListAsync());
        Assert.Empty(await context.Set<RecruitmentApplication>().ToListAsync());
        Assert.Empty(await context.Set<CrmHrAuditEntry>().ToListAsync());
    }

    [Fact]
    public async Task Conversion_reuses_the_observed_profile_and_preserves_fields_outside_its_editor() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var recruiting = services.GetRequiredService<RecruitingService>();
        var selected = await CrmHrRecruitingConcurrencyTests.CreateApplicationAsync(services);
        var partyId = (await recruiting.GetRecruitmentWorkspaceAsync(selected)).Application.PartyId!.Value;
        var profiles = services.GetRequiredService<IWorkforceProfileEditing>();
        var profile = (await profiles.GetWorkforceProfileWorkspaceAsync(partyId))!.Profile;
        profile.EmployeeCode = "KEEP-CODE";
        profile.InternalCostRate = 153.75m;
        profile.EndDate = new(2027, 11, 6);
        Assert.True((await profiles.SaveWorkforceProfileAsync(profile)).IsSuccess);
        var shown = await recruiting.GetRecruitmentWorkspaceAsync(selected);
        shown.Conversion.JobTitle = "  Canonical converted title  ";
        RecruitmentConversionReceipt? accepted = null;

        var result = await recruiting.ConvertCandidateAsync(shown.Conversion, onCommitted: receipt => accepted = receipt);

        Assert.True(result.IsSuccess);
        Assert.NotNull(accepted);
        Assert.Equal("Canonical converted title", accepted.Workforce.JobTitle);
        Assert.Equal("KEEP-CODE", accepted.Workforce.EmployeeCode);
        Assert.Equal(153.75m, accepted.Workforce.InternalCostRate);
        Assert.Equal(new DateOnly(2027, 11, 6), accepted.Workforce.EndDate);
        Assert.Equal(RecruitmentStage.Hired, accepted.Application.Stage);
        Assert.Equal(accepted.Original, (await recruiting.GetRecruitmentWorkspaceAsync(selected)).Conversion.Original);
        Assert.Equal(accepted.Workforce.Id, (await profiles.GetWorkforceProfileWorkspaceAsync(partyId))!.Profile.Id);
        var stale = shown.Conversion.Snapshot();
        Assert.True((await recruiting.ConvertCandidateAsync(stale)).IsFailure);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_stale_child_replacement_and_delete_preserve_the_current_row(bool lifecycle) {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var recruiting = services.GetRequiredService<RecruitingService>();
        var selected = await CrmHrRecruitingConcurrencyTests.CreateApplicationAsync(services);
        var partyId = (await recruiting.GetRecruitmentWorkspaceAsync(selected)).Application.PartyId!.Value;
        if (lifecycle) {
            Assert.True((await recruiting.SaveLifecycleTaskAsync(CrmHrRecruitingConcurrencyTests.NewTask(partyId, partyId))).IsSuccess);
            var row = Assert.Single((await recruiting.GetRecruitmentWorkspaceAsync(selected)).LifecycleTasks);
            var model = new LifecycleTaskEditorModel {
                Id = row.Id, PartyId = partyId, OwnerPartyId = partyId, DueDate = row.DueDate, Title = "Peer task", Original = row.Original
            };
            Assert.True((await recruiting.SaveLifecycleTaskAsync(model)).IsSuccess);
            model.Title = "Stale task";
            Assert.True((await recruiting.SaveLifecycleTaskAsync(model)).IsFailure);
            Assert.True((await recruiting.DeleteLifecycleTaskAsync(row.Id, original: row.Original)).IsFailure);
            Assert.Equal("Peer task", Assert.Single((await recruiting.GetRecruitmentWorkspaceAsync(selected)).LifecycleTasks).Title);
        } else {
            Assert.True((await recruiting.SaveRecruitmentInterviewAsync(new() {
                ApplicationId = selected, ScheduledAtLocal = new(2026, 11, 5, 10, 0, 0)
            })).IsSuccess);
            var row = Assert.Single((await recruiting.GetRecruitmentWorkspaceAsync(selected)).Interviews);
            var model = new RecruitmentInterviewEditorModel {
                Id = row.Id, ApplicationId = selected, ScheduledAtLocal = row.ScheduledAtUtc.LocalDateTime, Feedback = "Peer feedback", Original = row.Original
            };
            Assert.True((await recruiting.SaveRecruitmentInterviewAsync(model)).IsSuccess);
            model.Feedback = "Stale feedback";
            Assert.True((await recruiting.SaveRecruitmentInterviewAsync(model)).IsFailure);
            Assert.True((await recruiting.DeleteRecruitmentInterviewAsync(row.Id, original: row.Original)).IsFailure);
            Assert.Equal("Peer feedback", Assert.Single((await recruiting.GetRecruitmentWorkspaceAsync(selected)).Interviews).Feedback);
        }
    }

    [Fact]
    public async Task Extracted_recruiting_writers_run_without_constructing_the_legacy_recruiting_facade() {
        await using var application = await TestApplication.CreateAsync(new() {
            ConfigureServices = services => {
                services.RemoveAll<RecruitingService>();
                services.AddScoped<RecruitingService>(_ => throw new InvalidOperationException("The extracted writers must run independently."));
            }
        });
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var parties = services.GetRequiredService<PartyDirectoryService>();
        var partyResult = await parties.SavePartyAsync(new() { DisplayName = "Independent recruiting candidate", PartyType = PartyType.Person });
        Assert.True(partyResult.IsSuccess);
        RecruitmentSaveReceipt<RecruitmentApplicationEditorModel>? accepted = null;
        var result = await services.GetRequiredService<RecruitmentApplicationEditingService>().SaveRecruitmentApplicationAsync(new() {
            PartyId = partyResult.Value, DesiredRole = "Independent role", Stage = RecruitmentStage.Offer, Decision = RecruitmentDecision.Approved
        }, onCommitted: receipt => accepted = receipt);
        Assert.True(result.IsSuccess);
        Assert.NotNull(accepted);
        var port = services.GetRequiredService<IRecruitmentApplicationEditing>();
        var observed = await port.ReadApplicationAsync(result.Value, [], default);
        Assert.NotNull(observed);
        Assert.Equal(accepted.Accepted.Original, observed.Model.Original);
        Assert.Equal("Independent recruiting candidate", observed.Names[partyResult.Value]);
        RecruitmentSaveReceipt<RecruitmentInterviewEditorModel>? interview = null;
        Assert.True((await services.GetRequiredService<RecruitmentInterviewEditingService>().SaveRecruitmentInterviewAsync(new() {
            ApplicationId = result.Value, ScheduledAtLocal = new(2026, 11, 5, 10, 0, 0)
        }, onCommitted: receipt => interview = receipt)).IsSuccess);
        Assert.True((await services.GetRequiredService<RecruitmentInterviewEditingService>()
            .DeleteRecruitmentInterviewAsync(interview!.Id, original: interview.Accepted.Original)).IsSuccess);
        RecruitmentSaveReceipt<LifecycleTaskEditorModel>? task = null;
        Assert.True((await services.GetRequiredService<RecruitmentLifecycleEditingService>().SaveLifecycleTaskAsync(
            CrmHrRecruitingConcurrencyTests.NewTask(partyResult.Value, partyResult.Value), onCommitted: receipt => task = receipt)).IsSuccess);
        Assert.True((await services.GetRequiredService<RecruitmentLifecycleEditingService>()
            .DeleteLifecycleTaskAsync(task!.Id, original: task.Accepted.Original)).IsSuccess);
        var profile = (await services.GetRequiredService<IWorkforceProfileEditing>().GetWorkforceProfileWorkspaceAsync(partyResult.Value))!.Profile;
        var party = (await parties.GetPartyAsync(partyResult.Value))!;
        var converted = await services.GetRequiredService<RecruitmentConversionService>().ConvertCandidateAsync(new() {
            ApplicationId = result.Value, JobTitle = "Independent conversion",
            Original = new(accepted.Accepted.Original!, profile.Original!, party.LifecycleStatus, party.UpdatedAtUtc!.Value)
        });
        Assert.True(converted.IsSuccess);
    }

    public enum WriteKind { Application, Interview, Task, Conversion, DeleteInterview, DeleteTask }

    private sealed class ContextGate {
        public bool Hold { get; set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class GatedFactory(DbContextOptions<CrmHrDbContext> options, ContextGate gate) : IDbContextFactory<CrmHrDbContext> {
        public CrmHrDbContext CreateDbContext() => new(options);
        public async Task<CrmHrDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) {
            if (gate.Hold) {
                gate.Hold = false;
                gate.Entered.TrySetResult();
                await gate.Release.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            }
            return new(options);
        }
    }
}
