using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration.CrmHr;

public sealed class CrmHrWorkforceConcurrencyTests {
    [Theory]
    [InlineData(WriteKind.Profile)]
    [InlineData(WriteKind.Definition)]
    [InlineData(WriteKind.Skill)]
    [InlineData(WriteKind.Capacity)]
    [InlineData(WriteKind.DeleteSkill)]
    [InlineData(WriteKind.DeleteCapacity)]
    public async Task A_concurrency_shaped_failure_after_acceptance_cannot_be_reported_as_a_rejected_write(WriteKind kind) {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var hr = services.GetRequiredService<HrService>();
        var party = await CreateWorkerAsync(services, "Postcommit workforce worker");
        var definition = await hr.SaveSkillDefinitionAsync(new() { Name = "Postcommit original definition" });
        Assert.True(definition.IsSuccess);
        Assert.True((await hr.SavePartySkillAsync(new() { PartyId = party, SkillId = definition.Value })).IsSuccess);
        Assert.True((await hr.SaveCapacityBlockAsync(NewCapacity(party))).IsSuccess);
        var workspace = (await hr.GetWorkforceWorkspaceAsync(party))!;
        var skill = Assert.Single(workspace.Skills);
        var block = Assert.Single(workspace.CapacityBlocks);
        var acknowledged = false;
        void FailAfterAcceptance() {
            acknowledged = true;
            throw new DbUpdateConcurrencyException("A postcommit callback failed.");
        }
        Func<Task> write = kind switch {
            WriteKind.Profile => async () => { await hr.SaveWorkforceProfileAsync(workspace.Profile, onCommitted: _ => FailAfterAcceptance()); },
            WriteKind.Definition => async () => { await hr.SaveSkillDefinitionAsync(new() { Name = "Postcommit accepted definition" }, onCommitted: _ => FailAfterAcceptance()); },
            WriteKind.Skill => async () => { await hr.SavePartySkillAsync(new() { Id = skill.Id, PartyId = party, SkillId = skill.SkillId, Notes = "Accepted skill", Original = skill.Original }, onCommitted: _ => FailAfterAcceptance()); },
            WriteKind.Capacity => async () => { await hr.SaveCapacityBlockAsync(new() { Id = block.Id, PartyId = party, StartDate = block.StartDate, EndDate = block.EndDate, Percentage = 75m, Notes = "Accepted block", Original = block.Original }, onCommitted: _ => FailAfterAcceptance()); },
            WriteKind.DeleteSkill => async () => { await hr.DeletePartySkillAsync(skill.Id, original: skill.Original, onCommitted: _ => FailAfterAcceptance()); },
            _ => async () => { await hr.DeleteCapacityBlockAsync(block.Id, original: block.Original, onCommitted: _ => FailAfterAcceptance()); }
        };

        var failure = await Record.ExceptionAsync(write);

        Assert.True(acknowledged);
        Assert.IsType<DbUpdateConcurrencyException>(failure);
        var current = (await hr.GetWorkforceWorkspaceAsync(party))!;
        switch (kind) {
            case WriteKind.Profile:
                Assert.NotNull(current.Profile.Id);
                break;
            case WriteKind.Definition:
                Assert.Contains(await hr.ListSkillCatalogAsync(), item => item.Name == "Postcommit accepted definition");
                break;
            case WriteKind.Skill:
                Assert.Equal("Accepted skill", Assert.Single(current.Skills).Notes);
                break;
            case WriteKind.Capacity:
                Assert.Equal("Accepted block", Assert.Single(current.CapacityBlocks).Notes);
                break;
            case WriteKind.DeleteSkill:
                Assert.Empty(current.Skills);
                break;
            case WriteKind.DeleteCapacity:
                Assert.Empty(current.CapacityBlocks);
                break;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_capacity_write_cannot_rebind_a_selected_project_after_retirement_or_recreation(bool recreate) {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var party = await CreateWorkerAsync(services, "Project-bound capacity worker");
        var projects = services.GetRequiredService<ProjectsService>();
        var selected = (await projects.CreateWithAdmissionAsync(new() { Name = "Selected capacity project" })).Value!;
        var model = NewCapacity(party);
        model.RelatedProjectId = selected.ProjectId;
        model.RelatedProjectAdmission = selected;
        await new ProjectLifecycleTestDriver(services).DeleteAsync(selected.ProjectId);
        if (recreate) {
            Assert.True((await projects.CreateWithReceiptAsync(selected.ProjectId, new() { Name = "Replacement capacity project" })).IsSuccess);
        }
        var committed = false;

        var result = await services.GetRequiredService<WorkforceCapacityEditingService>().SaveCapacityBlockAsync(model, onCommitted: _ => committed = true);

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == CrmHrEditingErrorCodes.CapacityConflict);
        Assert.False(committed);
        Assert.Empty((await services.GetRequiredService<HrService>().GetWorkforceCapacityWorkspaceAsync(party))!.CapacityBlocks);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_profile_rejects_unsupported_kind_and_reversed_dates_before_persisting(bool reversedDates) {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var profiles = services.GetRequiredService<IWorkforceProfileEditing>();
        var party = await CreateWorkerAsync(services, "Invalid workforce values");
        var model = (await profiles.GetWorkforceProfileWorkspaceAsync(party))!.Profile;
        if (reversedDates) {
            model.StartDate = new(2026, 11, 4);
            model.EndDate = new(2026, 11, 2);
        } else {
            model.WorkforceKind = (WorkforceKind)999;
        }

        var result = await profiles.SaveWorkforceProfileAsync(model);

        Assert.True(result.IsFailure);
        Assert.Null((await profiles.GetWorkforceProfileWorkspaceAsync(party))!.Profile.Id);
    }

    [Fact]
    public async Task Extracted_workforce_owners_run_without_constructing_the_legacy_HR_facade() {
        await using var application = await TestApplication.CreateAsync(new() {
            ConfigureServices = services => {
                services.RemoveAll<HrService>();
                services.AddScoped<HrService>(_ => throw new InvalidOperationException("The workforce owners must not construct the legacy HR facade."));
            }
        });
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var profiles = services.GetRequiredService<IWorkforceProfileEditing>();
        var skills = services.GetRequiredService<WorkforceSkillEditingService>();
        var capacity = services.GetRequiredService<WorkforceCapacityEditingService>();
        var party = await CreateWorkerAsync(services, "Independent owners worker");
        var draft = (await profiles.GetWorkforceProfileWorkspaceAsync(party))!.Profile;
        draft.JobTitle = "  Independent profile  ";
        WorkforceSaveReceipt<WorkforceProfileEditorModel>? profileReceipt = null;
        Assert.True((await profiles.SaveWorkforceProfileAsync(draft, onCommitted: receipt => profileReceipt = receipt)).IsSuccess);
        Assert.Equal("Independent profile", profileReceipt!.Accepted.JobTitle);
        var current = await profiles.ReadWorkforceProfileEditorAsync(party, default);
        Assert.Equal(profileReceipt.Accepted.Original, current!.Value.Profile.Original);
        var definition = await skills.SaveSkillDefinitionAsync(new() { Name = "Independent skill" });
        Assert.True(definition.IsSuccess);
        Assert.True((await skills.SavePartySkillAsync(new() { PartyId = party, SkillId = definition.Value })).IsSuccess);
        WorkforceSaveReceipt<CapacityBlockEditorModel>? blockReceipt = null;
        Assert.True((await capacity.SaveCapacityBlockAsync(NewCapacity(party), onCommitted: receipt => blockReceipt = receipt)).IsSuccess);
        var workspace = (await profiles.GetWorkforceProfileWorkspaceAsync(party))!;
        var skill = Assert.Single(workspace.Skills);
        Assert.NotNull(skill.Original);
        Assert.True((await skills.DeletePartySkillAsync(skill.Id, original: skill.Original)).IsSuccess);
        Assert.True((await capacity.DeleteCapacityBlockAsync(blockReceipt!.Id, original: blockReceipt.Accepted.Original)).IsSuccess);
    }

    [Fact]
    public async Task A_stale_profile_cannot_replace_a_peer_profile() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var hr = scope.ServiceProvider.GetRequiredService<HrService>();
        var party = await CreateWorkerAsync(scope.ServiceProvider, "Profile worker");
        var initial = (await hr.GetWorkforceWorkspaceAsync(party))!.Profile;
        initial.JobTitle = "Original title";
        Assert.True((await hr.SaveWorkforceProfileAsync(initial)).IsSuccess);
        var local = (await hr.GetWorkforceWorkspaceAsync(party))!.Profile;
        var peer = local.Snapshot();
        peer.JobTitle = "Peer title";
        Assert.True((await hr.SaveWorkforceProfileAsync(peer)).IsSuccess);
        local.Notes = "Stale local note";

        var result = await hr.SaveWorkforceProfileAsync(local);

        Assert.True(result.IsFailure);
        var saved = (await hr.GetWorkforceWorkspaceAsync(party))!.Profile;
        Assert.Equal("Peer title", saved.JobTitle);
        Assert.Empty(saved.Notes);
    }

    [Fact]
    public async Task Creating_an_existing_skill_name_cannot_overwrite_its_definition() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var hr = scope.ServiceProvider.GetRequiredService<HrService>();
        var first = await hr.SaveSkillDefinitionAsync(new() { Name = "Native skill", Description = "Original description" });
        Assert.True(first.IsSuccess);

        var result = await hr.SaveSkillDefinitionAsync(new() { Name = "Native skill", Description = "Unobserved replacement" });

        Assert.True(result.IsFailure);
        Assert.Equal("Original description", (await hr.ListSkillCatalogAsync()).Single(row => row.Id == first.Value).Description);
    }

    [Fact]
    public async Task A_party_skill_identifier_cannot_be_moved_to_another_worker() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var hr = scope.ServiceProvider.GetRequiredService<HrService>();
        var firstParty = await CreateWorkerAsync(scope.ServiceProvider, "First skill worker");
        var secondParty = await CreateWorkerAsync(scope.ServiceProvider, "Second skill worker");
        var skill = await hr.SaveSkillDefinitionAsync(new() { Name = "Native skill" });
        Assert.True(skill.IsSuccess);
        var first = await hr.SavePartySkillAsync(new() { PartyId = firstParty, SkillId = skill.Value, Notes = "Original skill" });
        Assert.True(first.IsSuccess);

        var result = await hr.SavePartySkillAsync(new() { Id = first.Value, PartyId = secondParty, SkillId = skill.Value, Notes = "Moved skill" });

        Assert.True(result.IsFailure);
        Assert.Equal("Original skill", Assert.Single((await hr.GetWorkforceWorkspaceAsync(firstParty))!.Skills).Notes);
        Assert.Empty((await hr.GetWorkforceWorkspaceAsync(secondParty))!.Skills);
    }

    [Fact]
    public async Task A_capacity_identifier_cannot_be_moved_to_another_worker() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var hr = scope.ServiceProvider.GetRequiredService<HrService>();
        var firstParty = await CreateWorkerAsync(scope.ServiceProvider, "First capacity worker");
        var secondParty = await CreateWorkerAsync(scope.ServiceProvider, "Second capacity worker");
        var model = NewCapacity(firstParty);
        var first = await hr.SaveCapacityBlockAsync(model);
        Assert.True(first.IsSuccess);
        model.Id = first.Value;
        model.PartyId = secondParty;
        model.Notes = "Moved block";

        var result = await hr.SaveCapacityBlockAsync(model);

        Assert.True(result.IsFailure);
        Assert.Equal("Original block", Assert.Single((await hr.GetWorkforceWorkspaceAsync(firstParty))!.CapacityBlocks).Notes);
        Assert.Empty((await hr.GetWorkforceWorkspaceAsync(secondParty))!.CapacityBlocks);
    }

    [Fact]
    public async Task An_unknown_capacity_update_identifier_cannot_create_a_new_block() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var hr = scope.ServiceProvider.GetRequiredService<HrService>();
        var party = await CreateWorkerAsync(scope.ServiceProvider, "Missing capacity worker");
        var model = NewCapacity(party);
        model.Id = Guid.NewGuid();

        var result = await hr.SaveCapacityBlockAsync(model);

        Assert.True(result.IsFailure);
        Assert.Empty((await hr.GetWorkforceWorkspaceAsync(party))!.CapacityBlocks);
    }

    [Fact]
    public async Task Deleting_capacity_requires_the_observed_row_instead_of_only_its_identifier() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var hr = scope.ServiceProvider.GetRequiredService<HrService>();
        var party = await CreateWorkerAsync(scope.ServiceProvider, "Delete capacity worker");
        var saved = await hr.SaveCapacityBlockAsync(NewCapacity(party));
        Assert.True(saved.IsSuccess);

        await hr.DeleteCapacityBlockAsync(saved.Value);

        Assert.Equal(saved.Value, Assert.Single((await hr.GetWorkforceWorkspaceAsync(party))!.CapacityBlocks).Id);
    }

    [Fact]
    public async Task A_stale_definition_and_party_skill_cannot_replace_current_values() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var hr = scope.ServiceProvider.GetRequiredService<HrService>();
        var party = await CreateWorkerAsync(scope.ServiceProvider, "Stale skill worker");
        var created = await hr.SaveSkillDefinitionAsync(new() { Name = "Original skill" });
        Assert.True(created.IsSuccess);
        var original = (await hr.ListSkillCatalogAsync()).Single(row => row.Id == created.Value);
        var definition = new SkillDefinitionEditorModel { Id = original.Id, Name = original.Name, Description = "Peer description", Original = original };
        Assert.True((await hr.SaveSkillDefinitionAsync(definition)).IsSuccess);
        definition.Description = "Stale description";
        Assert.True((await hr.SaveSkillDefinitionAsync(definition)).IsFailure);
        Assert.Equal("Peer description", (await hr.ListSkillCatalogAsync()).Single(row => row.Id == created.Value).Description);

        Assert.True((await hr.SavePartySkillAsync(new() { PartyId = party, SkillId = created.Value, Notes = "Original skill" })).IsSuccess);
        var row = Assert.Single((await hr.GetWorkforceWorkspaceAsync(party))!.Skills);
        var skill = new PartySkillEditorModel { Id = row.Id, PartyId = party, SkillId = row.SkillId, Notes = "Peer skill", Original = row.Original };
        Assert.NotNull(skill.Original);
        Assert.True((await hr.SavePartySkillAsync(skill)).IsSuccess);
        skill.Notes = "Stale skill";
        Assert.True((await hr.SavePartySkillAsync(skill)).IsFailure);
        var deletion = await hr.DeletePartySkillAsync(row.Id, original: row.Original);
        Assert.True(deletion.IsFailure);
        Assert.Equal("Peer skill", Assert.Single((await hr.GetWorkforceWorkspaceAsync(party))!.Skills).Notes);
    }

    [Fact]
    public async Task A_capacity_update_and_delete_require_the_displayed_row_and_keep_the_date_range_atomic() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var hr = scope.ServiceProvider.GetRequiredService<HrService>();
        var party = await CreateWorkerAsync(scope.ServiceProvider, "Stale range worker");
        Assert.True((await hr.SaveCapacityBlockAsync(NewCapacity(party))).IsSuccess);
        var row = Assert.Single((await hr.GetWorkforceWorkspaceAsync(party))!.CapacityBlocks);
        var peer = new CapacityBlockEditorModel {
            Id = row.Id, PartyId = party, StartDate = row.StartDate, EndDate = row.EndDate.AddDays(2),
            Percentage = 75m, Notes = "Peer range", Original = row.Original
        };
        Assert.NotNull(peer.Original);
        WorkforceSaveReceipt<CapacityBlockEditorModel>? accepted = null;
        Assert.True((await hr.SaveCapacityBlockAsync(peer, onCommitted: receipt => accepted = receipt)).IsSuccess);
        Assert.NotNull(accepted);
        Assert.Equal(peer.EndDate, accepted.Accepted.EndDate);
        Assert.Equal(75m, accepted.Accepted.Percentage);
        peer.StartDate = row.StartDate.AddDays(-1);

        Assert.True((await hr.SaveCapacityBlockAsync(peer)).IsFailure);
        Assert.True((await hr.DeleteCapacityBlockAsync(row.Id, original: row.Original)).IsFailure);
        var current = Assert.Single((await hr.GetWorkforceWorkspaceAsync(party))!.CapacityBlocks);
        Assert.Equal(row.StartDate, current.StartDate);
        Assert.Equal(row.EndDate.AddDays(2), current.EndDate);
        Assert.Equal(75m, current.Percentage);
        Assert.True((await hr.DeleteCapacityBlockAsync(current.Id, original: current.Original)).IsSuccess);
        Assert.Empty((await hr.GetWorkforceWorkspaceAsync(party))!.CapacityBlocks);
    }

    [Theory]
    [InlineData(ReferenceKind.Worker)]
    [InlineData(ReferenceKind.Manager)]
    [InlineData(ReferenceKind.HomeUnit)]
    public async Task Saved_workforce_reference_types_cannot_be_invalidated_by_a_later_party_edit(ReferenceKind reference) {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var hr = services.GetRequiredService<HrService>();
        var directory = services.GetRequiredService<PartyDirectoryService>();
        var worker = await CreateWorkerAsync(services, "Typed worker");
        var manager = await CreateWorkerAsync(services, "Typed manager");
        var unit = await directory.SavePartyAsync(new() { PartyType = PartyType.OrganizationUnit, DisplayName = "Typed unit" });
        Assert.True(unit.IsSuccess);
        var model = (await hr.GetWorkforceWorkspaceAsync(worker))!.Profile;
        model.ManagerPartyId = manager;
        model.HomeUnitPartyId = unit.Value;
        Assert.True((await hr.SaveWorkforceProfileAsync(model)).IsSuccess);
        var target = reference switch { ReferenceKind.Worker => worker, ReferenceKind.Manager => manager, _ => unit.Value };
        var edit = (await directory.GetPartyAsync(target))!;
        var savedType = edit.PartyType;
        edit.PartyType = savedType == PartyType.Person ? PartyType.Organization : PartyType.Person;

        var changed = await directory.SavePartyAsync(edit);

        Assert.True(changed.IsFailure);
        Assert.Equal(savedType, (await directory.GetPartyAsync(target))!.PartyType);
    }

    [Fact]
    public async Task Profile_submission_is_frozen_before_context_creation_and_receipt_precedes_failed_continuation() {
        var gate = new WriteGate();
        await using var application = await CreateAsync(gate);
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var hr = services.GetRequiredService<HrService>();
        var party = await CreateWorkerAsync(services, "Frozen profile worker");
        var model = (await hr.GetWorkforceWorkspaceAsync(party))!.Profile;
        model.JobTitle = "  Submitted title  ";
        model.RateCurrencyCode = "eur";
        gate.HoldCreation = true;
        WorkforceSaveReceipt<WorkforceProfileEditorModel>? accepted = null;
        var pending = hr.SaveWorkforceProfileAsync(model, onCommitted: receipt => {
            accepted = receipt;
            throw new ContinuationFailure();
        });
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        model.JobTitle = "  Later raw title  ";
        gate.Release.TrySetResult();

        Assert.IsType<ContinuationFailure>(await Record.ExceptionAsync(() => pending));
        Assert.NotNull(accepted);
        Assert.Equal("Submitted title", accepted.Accepted.JobTitle);
        Assert.Equal("EUR", accepted.Accepted.RateCurrencyCode);
        Assert.Equal(accepted.Id, accepted.Accepted.Id);
        var current = (await hr.GetWorkforceWorkspaceAsync(party))!.Profile;
        Assert.Equal(accepted.Accepted.Original, current.Original);
        Assert.Equal("Submitted title", current.JobTitle);
        Assert.Equal("  Later raw title  ", model.JobTitle);
    }

    [Theory]
    [InlineData(WriteKind.Profile, false)]
    [InlineData(WriteKind.Profile, true)]
    [InlineData(WriteKind.Definition, false)]
    [InlineData(WriteKind.Definition, true)]
    [InlineData(WriteKind.Skill, false)]
    [InlineData(WriteKind.Skill, true)]
    [InlineData(WriteKind.Capacity, false)]
    [InlineData(WriteKind.Capacity, true)]
    [InlineData(WriteKind.DeleteSkill, false)]
    [InlineData(WriteKind.DeleteSkill, true)]
    [InlineData(WriteKind.DeleteCapacity, false)]
    [InlineData(WriteKind.DeleteCapacity, true)]
    public async Task Current_authority_is_required_before_effects_and_after_SQL_for_each_workforce_command(WriteKind kind, bool afterSql) {
        var gate = new WriteGate();
        await using var application = await CreateAsync(gate);
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var hr = services.GetRequiredService<HrService>();
        var party = await CreateWorkerAsync(services, "Authority worker");
        var definition = await hr.SaveSkillDefinitionAsync(new() { Name = "Original definition" });
        Assert.True(definition.IsSuccess);
        Assert.True((await hr.SavePartySkillAsync(new() { PartyId = party, SkillId = definition.Value, Notes = "Original skill" })).IsSuccess);
        Assert.True((await hr.SaveCapacityBlockAsync(NewCapacity(party))).IsSuccess);
        var workspace = (await hr.GetWorkforceWorkspaceAsync(party))!;
        var skill = Assert.Single(workspace.Skills);
        var block = Assert.Single(workspace.CapacityBlocks);
        var committed = false;
        Func<Task> write = kind switch {
            WriteKind.Profile => async () => { await hr.SaveWorkforceProfileAsync(workspace.Profile, onCommitted: _ => committed = true); },
            WriteKind.Definition => async () => { await hr.SaveSkillDefinitionAsync(new() { Name = "Rejected definition" }, onCommitted: _ => committed = true); },
            WriteKind.Skill => async () => { await hr.SavePartySkillAsync(new() { Id = skill.Id, PartyId = party, SkillId = skill.SkillId, Notes = "Rejected skill", Original = skill.Original }, onCommitted: _ => committed = true); },
            WriteKind.Capacity => async () => { await hr.SaveCapacityBlockAsync(new() { Id = block.Id, PartyId = party, StartDate = block.StartDate, EndDate = block.EndDate, Percentage = 75m, Notes = "Rejected block", Original = block.Original }, onCommitted: _ => committed = true); },
            WriteKind.DeleteSkill => async () => { await hr.DeletePartySkillAsync(skill.Id, original: skill.Original, onCommitted: _ => committed = true); },
            _ => async () => { await hr.DeleteCapacityBlockAsync(block.Id, original: block.Original, onCommitted: _ => committed = true); }
        };
        gate.Observe = true;
        using var authority = services.GetRequiredService<CrmHrChanges>().EnterUiWrite(_ => {
            if (!afterSql || gate.Saved) {
                throw new UnauthorizedAccessException("The workforce grant retired.");
            }
            return ValueTask.CompletedTask;
        });

        Assert.IsType<UnauthorizedAccessException>(await Record.ExceptionAsync(write));
        Assert.False(committed);
        Assert.Equal(afterSql, gate.Saved);
        if (afterSql) {
            Assert.True(gate.HadTransaction);
        }
        var current = (await hr.GetWorkforceWorkspaceAsync(party))!;
        Assert.Null(current.Profile.Id);
        Assert.Equal("Original skill", Assert.Single(current.Skills).Notes);
        Assert.Equal("Original block", Assert.Single(current.CapacityBlocks).Notes);
        Assert.Single(await hr.ListSkillCatalogAsync());
    }

    public enum ReferenceKind { Worker, Manager, HomeUnit }
    public enum WriteKind { Profile, Definition, Skill, Capacity, DeleteSkill, DeleteCapacity }
    private sealed class ContinuationFailure : Exception;

    private static Task<TestApplication> CreateAsync(WriteGate gate) => TestApplication.CreateAsync(new() {
        ConfigureServices = services => {
            services.RemoveAll<IDbContextFactory<CrmHrDbContext>>();
            services.AddSingleton<IDbContextFactory<CrmHrDbContext>>(provider => new GatedFactory(provider.GetRequiredService<DbContextOptions<CrmHrDbContext>>(), gate));
        }
    });

    private sealed class WriteGate : SaveChangesInterceptor {
        public bool HoldCreation { get; set; }
        public bool Observe { get; set; }
        public bool Saved { get; private set; }
        public bool HadTransaction { get; private set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default) {
            if (Observe) {
                Saved = true;
                HadTransaction = eventData.Context?.Database.CurrentTransaction is not null;
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class GatedFactory(DbContextOptions<CrmHrDbContext> options, WriteGate gate) : IDbContextFactory<CrmHrDbContext> {
        private DbContextOptions<CrmHrDbContext> Options => new DbContextOptionsBuilder<CrmHrDbContext>(options).AddInterceptors(gate).Options;
        public CrmHrDbContext CreateDbContext() => new(Options);
        public async Task<CrmHrDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) {
            if (gate.HoldCreation) {
                gate.HoldCreation = false;
                gate.Entered.TrySetResult();
                await gate.Release.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            }
            return new(Options);
        }
    }

    private static CapacityBlockEditorModel NewCapacity(Guid partyId) => new() {
        PartyId = partyId, StartDate = new(2026, 11, 2), EndDate = new(2026, 11, 4), Percentage = 50m, Notes = "Original block"
    };

    private static async Task<Guid> CreateWorkerAsync(IServiceProvider services, string name) {
        var result = await services.GetRequiredService<PartyDirectoryService>().SavePartyAsync(new() {
            PartyType = PartyType.Person, DisplayName = name
        });
        Assert.True(result.IsSuccess);
        return result.Value;
    }
}
