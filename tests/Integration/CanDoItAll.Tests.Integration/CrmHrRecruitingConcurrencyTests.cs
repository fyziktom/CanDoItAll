using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.CrmHr;

public sealed class CrmHrRecruitingConcurrencyTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_task_cannot_rebind_a_selected_project_after_retirement_or_recreation(bool recreate) {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var recruiting = services.GetRequiredService<RecruitingService>();
        var applicationId = await CreateApplicationAsync(services);
        var candidateId = (await recruiting.GetRecruitmentWorkspaceAsync(applicationId)).Application.PartyId!.Value;
        var projects = services.GetRequiredService<ProjectsService>();
        var selected = (await projects.CreateWithAdmissionAsync(new() { Name = "Selected lifecycle project" })).Value!;
        var draft = new LifecycleTaskEditorModel {
            PartyId = candidateId, OwnerPartyId = candidateId, Title = "Observed project task", DueDate = new(2026, 11, 3),
            RelatedProjectId = selected.ProjectId, RelatedProjectAdmission = selected
        };
        await new ProjectLifecycleTestDriver(services).DeleteAsync(selected.ProjectId);
        if (recreate) {
            Assert.True((await projects.CreateWithReceiptAsync(selected.ProjectId, new() { Name = "Replacement lifecycle project" })).IsSuccess);
        }
        var committed = false;

        var result = await services.GetRequiredService<RecruitmentLifecycleEditingService>().SaveLifecycleTaskAsync(draft, onCommitted: _ => committed = true);

        Assert.True(result.IsFailure);
        Assert.False(committed);
        Assert.Empty((await recruiting.GetRecruitmentWorkspaceAsync(applicationId)).LifecycleTasks);
    }
    [Fact]
    public async Task Inline_candidate_creation_returns_its_exact_accepted_identity_and_display_name() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var recruiting = scope.ServiceProvider.GetRequiredService<RecruitingService>();
        RecruitmentSaveReceipt<RecruitmentApplicationEditorModel>? accepted = null;

        var result = await recruiting.SaveRecruitmentApplicationAsync(new() {
            CandidateName = "Inline accepted candidate",
            CandidateEmail = "accepted@example.test",
            DesiredRole = "Inline application role"
        }, onCommitted: receipt => accepted = receipt);

        Assert.True(result.IsSuccess);
        Assert.NotNull(accepted);
        Assert.Equal("Inline accepted candidate", accepted.Accepted.CandidateName);
        Assert.Equal("accepted@example.test", accepted.Accepted.CandidateEmail);
        Assert.NotNull(accepted.Accepted.PartyId);
        Assert.Equal(accepted.Id, accepted.Accepted.Original?.Id);
        var saved = await recruiting.GetRecruitmentWorkspaceAsync(result.Value);
        Assert.Equal(accepted.Accepted.PartyId, saved.Application.PartyId);
        Assert.Equal("accepted@example.test", saved.CandidatePrimaryEmail);
    }

    [Theory]
    [InlineData(ReferenceKind.Candidate)]
    [InlineData(ReferenceKind.Interviewer)]
    [InlineData(ReferenceKind.TaskOwner)]
    public async Task A_later_party_type_change_cannot_invalidate_saved_recruiting_references(ReferenceKind kind) {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var recruiting = services.GetRequiredService<RecruitingService>();
        var parties = services.GetRequiredService<PartyDirectoryService>();
        var selected = await CreateApplicationAsync(services);
        var candidateId = (await recruiting.GetRecruitmentWorkspaceAsync(selected)).Application.PartyId!.Value;
        var referencedId = candidateId;
        if (kind != ReferenceKind.Candidate) {
            var referenced = await parties.SavePartyAsync(new() { DisplayName = "Referenced recruiter", PartyType = PartyType.Person });
            Assert.True(referenced.IsSuccess);
            referencedId = referenced.Value;
        }
        if (kind == ReferenceKind.Interviewer) {
            Assert.True((await recruiting.SaveRecruitmentInterviewAsync(new() {
                ApplicationId = selected, ScheduledAtLocal = new(2026, 11, 5, 10, 0, 0), InterviewerPartyId = referencedId
            })).IsSuccess);
        } else if (kind == ReferenceKind.TaskOwner) {
            Assert.True((await recruiting.SaveLifecycleTaskAsync(NewTask(candidateId, referencedId))).IsSuccess);
        }
        var replacement = (await parties.GetPartyAsync(referencedId))!;
        replacement.PartyType = PartyType.Organization;

        var result = await parties.SavePartyAsync(replacement);

        Assert.True(result.IsFailure);
        Assert.Equal(PartyType.Person, (await parties.GetPartyAsync(referencedId))!.PartyType);
    }

    public enum ReferenceKind { Candidate, Interviewer, TaskOwner }

    [Fact]
    public async Task A_stale_application_cannot_replace_the_current_manual_decision() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var recruiting = services.GetRequiredService<RecruitingService>();
        var selected = await CreateApplicationAsync(services);
        var first = (await recruiting.GetRecruitmentWorkspaceAsync(selected)).Application;
        var stale = first.Snapshot();
        first.Notes = "Accepted peer notes";
        Assert.True((await recruiting.SaveRecruitmentApplicationAsync(first)).IsSuccess);
        stale.DesiredRole = "Stale replacement role";

        var result = await recruiting.SaveRecruitmentApplicationAsync(stale);

        Assert.True(result.IsFailure);
        var current = (await recruiting.GetRecruitmentWorkspaceAsync(selected)).Application;
        Assert.Equal("Accepted peer notes", current.Notes);
        Assert.Equal("Concurrent candidate role", current.DesiredRole);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task An_interview_identifier_cannot_move_a_row_or_create_a_missing_update(bool missing) {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var recruiting = services.GetRequiredService<RecruitingService>();
        var first = await CreateApplicationAsync(services);
        var second = await CreateApplicationAsync(services);
        var created = await recruiting.SaveRecruitmentInterviewAsync(new() {
            ApplicationId = first,
            ScheduledAtLocal = new DateTime(2026, 11, 5, 10, 0, 0),
            Feedback = "Keep this feedback"
        });
        Assert.True(created.IsSuccess);

        var result = await recruiting.SaveRecruitmentInterviewAsync(new() {
            Id = missing ? Guid.NewGuid() : created.Value,
            ApplicationId = second,
            ScheduledAtLocal = new DateTime(2026, 11, 6, 11, 0, 0),
            Feedback = "Unobserved replacement"
        });

        Assert.True(result.IsFailure);
        Assert.Equal("Keep this feedback", Assert.Single((await recruiting.GetRecruitmentWorkspaceAsync(first)).Interviews).Feedback);
        Assert.Empty((await recruiting.GetRecruitmentWorkspaceAsync(second)).Interviews);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_lifecycle_task_identifier_cannot_move_a_row_or_create_a_missing_update(bool missing) {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var recruiting = services.GetRequiredService<RecruitingService>();
        var first = await CreateApplicationAsync(services);
        var second = await CreateApplicationAsync(services);
        var firstParty = (await recruiting.GetRecruitmentWorkspaceAsync(first)).Application.PartyId!.Value;
        var secondParty = (await recruiting.GetRecruitmentWorkspaceAsync(second)).Application.PartyId!.Value;
        var created = await recruiting.SaveLifecycleTaskAsync(NewTask(firstParty, firstParty));
        Assert.True(created.IsSuccess);
        var replacement = NewTask(secondParty, secondParty);
        replacement.Id = missing ? Guid.NewGuid() : created.Value;
        replacement.Title = "Unobserved replacement task";

        var result = await recruiting.SaveLifecycleTaskAsync(replacement);

        Assert.True(result.IsFailure);
        Assert.Equal("Original lifecycle task", Assert.Single((await recruiting.GetRecruitmentWorkspaceAsync(first)).LifecycleTasks).Title);
        Assert.Empty((await recruiting.GetRecruitmentWorkspaceAsync(second)).LifecycleTasks);
    }

    [Fact]
    public async Task An_unobserved_lifecycle_delete_cannot_remove_the_displayed_task() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var recruiting = services.GetRequiredService<RecruitingService>();
        var selected = await CreateApplicationAsync(services);
        var partyId = (await recruiting.GetRecruitmentWorkspaceAsync(selected)).Application.PartyId!.Value;
        var created = await recruiting.SaveLifecycleTaskAsync(NewTask(partyId, partyId));
        Assert.True(created.IsSuccess);

        await recruiting.DeleteLifecycleTaskAsync(created.Value);

        Assert.Single((await recruiting.GetRecruitmentWorkspaceAsync(selected)).LifecycleTasks);
    }

    [Fact]
    public async Task Conversion_cannot_use_a_changed_application_preflight() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var recruiting = services.GetRequiredService<RecruitingService>();
        var selected = await CreateApplicationAsync(services);
        var shown = await recruiting.GetRecruitmentWorkspaceAsync(selected);
        var peer = shown.Application.Snapshot();
        peer.DesiredRole = "Peer accepted role after conversion preflight";
        Assert.True((await recruiting.SaveRecruitmentApplicationAsync(peer)).IsSuccess);

        var result = await recruiting.ConvertCandidateAsync(shown.Conversion);

        Assert.True(result.IsFailure);
        var current = await recruiting.GetRecruitmentWorkspaceAsync(selected);
        Assert.Equal(RecruitmentStage.Offer, current.Application.Stage);
        Assert.False(current.HasWorkforceProfile);
    }

    internal static LifecycleTaskEditorModel NewTask(Guid partyId, Guid ownerId) => new() {
        PartyId = partyId,
        OwnerPartyId = ownerId,
        DueDate = new(2026, 11, 6),
        Title = "Original lifecycle task"
    };

    internal static async Task<Guid> CreateApplicationAsync(IServiceProvider services) {
        var party = await services.GetRequiredService<PartyDirectoryService>().SavePartyAsync(new() {
            DisplayName = "Concurrent recruitment candidate " + Guid.NewGuid().ToString("N"),
            PartyType = PartyType.Person,
            LifecycleStatus = PartyLifecycleStatus.Candidate
        });
        Assert.True(party.IsSuccess);
        var created = await services.GetRequiredService<RecruitingService>().SaveRecruitmentApplicationAsync(new() {
            PartyId = party.Value,
            DesiredRole = "Concurrent candidate role",
            Stage = RecruitmentStage.Offer,
            Decision = RecruitmentDecision.Approved
        });
        Assert.True(created.IsSuccess);
        return created.Value;
    }
}
