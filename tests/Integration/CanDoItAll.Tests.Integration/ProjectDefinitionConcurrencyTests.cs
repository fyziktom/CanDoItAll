using System.Data.Common;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

public sealed class ProjectDefinitionConcurrencyTests {
    [Fact]
    public async Task Overlapping_original_drafts_have_exactly_one_winner() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var id = await CreateAsync(service);
        var first = await service.GetAsync(id);
        var second = await service.GetAsync(id);
        first.Name = "First proposal";
        second.Name = "Second proposal";

        var results = await Task.WhenAll(service.SaveEditorAsync(first), service.SaveEditorAsync(second));

        var accepted = Assert.Single(results, result => result.IsSuccess).Value!;
        var rejected = Assert.Single(results, result => result.IsFailure);
        Assert.Contains(rejected.Errors, error => error.Code == ProjectErrorCodes.DefinitionConflict);
        var current = (await service.GetDefinitionAsync(id)).Value!;
        Assert.Equal(accepted.Name, current.Scalars.Name);
        Assert.Equal(accepted.Definition, current.Expectation);
    }

    [Fact]
    public async Task Independent_project_updates_all_commit_without_serializable_page_conflicts() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var drafts = new List<ProjectEditorModel>();
        for (var index = 0; index < 8; index++) {
            var id = await CreateAsync(service);
            var draft = await service.GetAsync(id);
            draft.Name = $"Independent project {index}";
            draft.Phases[0].Goal = $"Independent goal {index}";
            drafts.Add(draft);
        }

        var results = await Task.WhenAll(drafts.Select(draft => service.SaveEditorAsync(draft)));

        Assert.All(results, result => Assert.True(result.IsSuccess,
            string.Join(", ", result.Errors.Select(error => error.Code))));
        for (var index = 0; index < drafts.Count; index++) {
            var current = (await service.GetDefinitionAsync(drafts[index].Id!.Value)).Value!;
            Assert.Equal(drafts[index].Name, current.Scalars.Name);
            Assert.Equal(drafts[index].Phases[0].Goal, Assert.Single(current.Phases).Goal);
        }
    }

    [Fact]
    public async Task Stale_full_definition_cannot_replace_a_newer_scalar_or_child_edit() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var id = await CreateAsync(service);
        var original = await service.GetAsync(id);
        var competing = await service.GetAsync(id);
        competing.Name = "Accepted name";
        competing.Phases[0].Goal = "Accepted phase goal";
        var accepted = await service.SaveEditorAsync(competing);
        Assert.True(accepted.IsSuccess);

        original.Name = "Stale name";
        original.Options.Clear();
        var stale = await service.SaveEditorAsync(original);

        Assert.Contains(stale.Errors, error => error.Code == ProjectErrorCodes.DefinitionConflict);
        var current = await service.GetDefinitionAsync(id);
        Assert.True(current.IsSuccess);
        Assert.Equal("Accepted name", current.Value!.Scalars.Name);
        Assert.Equal("Accepted phase goal", Assert.Single(current.Value.Phases).Goal);
        Assert.Single(current.Value.Options);
        Assert.Equal(accepted.Value!.Definition, current.Value.Expectation);
    }

    [Fact]
    public async Task Scalar_command_preserves_children_and_rejects_the_same_original_expectation_twice() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var id = await CreateAsync(service);
        var original = (await service.GetDefinitionAsync(id)).Value!;
        var command = new UpdateProjectDefinitionScalarsCommand(original.Expectation,
            original.Scalars with { Name = "  Normalized scalar name  " });

        var accepted = await service.SaveScalarsAsync(command);
        var stale = await service.SaveScalarsAsync(command);

        Assert.True(accepted.IsSuccess);
        Assert.Equal("Normalized scalar name", accepted.Value!.Scalars.Name);
        Assert.Contains(stale.Errors, error => error.Code == ProjectErrorCodes.DefinitionConflict);
        var current = (await service.GetDefinitionAsync(id)).Value!;
        Assert.Equal(original.Phases, current.Phases);
        Assert.Equal(original.Options, current.Options);
        Assert.Equal(accepted.Value.Definition, current.Expectation);
    }

    [Fact]
    public async Task Child_only_and_no_change_saves_advance_the_root_even_with_a_frozen_clock() {
        await using var application = await TestApplication.CreateAsync(new TestHarnessOptions {
            ConfigureServices = services => {
                services.RemoveAll<IClock>();
                services.AddSingleton<IClock>(new FixedClock());
            }
        });
        await using var scope = application.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var id = await CreateAsync(service);
        var original = await service.GetAsync(id);
        var initialRevision = original.ExpectedDefinition!.Revision;
        original.Phases[0].Goal = "Child-only update";
        var childWrite = await service.SaveEditorAsync(original);
        Assert.True(childWrite.IsSuccess);
        Assert.NotEqual(initialRevision, childWrite.Value!.Definition.Revision);

        var unchanged = await service.GetAsync(id);
        var noChange = await service.SaveEditorAsync(unchanged);
        Assert.True(noChange.IsSuccess);
        Assert.NotEqual(childWrite.Value.Definition.Revision, noChange.Value!.Definition.Revision);
        Assert.Contains((await service.SaveEditorAsync(original)).Errors,
            error => error.Code == ProjectErrorCodes.DefinitionConflict);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Existing_updates_require_original_definition_even_when_lifetime_is_known(bool editorPath) {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var id = await CreateAsync(service);
        var model = await service.GetAsync(id);
        model.ExpectedDefinition = null;
        model.Name = "Unconditional replacement";
        var errors = editorPath ? (await service.SaveEditorAsync(model)).Errors : (await service.SaveAsync(model)).Errors;

        Assert.Contains(errors, error => error.Code == ProjectErrorCodes.DefinitionExpectationRequired);
        Assert.Equal("Original project", (await service.GetAsync(id)).Name);
    }

    [Theory]
    [InlineData(InvalidChild.Unknown)]
    [InlineData(InvalidChild.Foreign)]
    [InlineData(InvalidChild.Duplicate)]
    [InlineData(InvalidChild.Empty)]
    public async Task Replacement_rejects_invalid_persisted_child_identity_without_mutating_the_owner(InvalidChild invalid) {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var id = await CreateAsync(service);
        var model = await service.GetAsync(id);
        var originalRevision = model.ExpectedDefinition;
        switch (invalid) {
            case InvalidChild.Unknown:
                model.Phases[0].Id = Guid.NewGuid();
                break;
            case InvalidChild.Foreign:
                var otherId = await CreateAsync(service);
                model.Phases[0].Id = (await service.GetAsync(otherId)).Phases[0].Id;
                break;
            case InvalidChild.Duplicate:
                model.Phases.Add(new() { Id = model.Phases[0].Id, Name = "Duplicate" });
                break;
            case InvalidChild.Empty:
                model.Phases[0].Id = Guid.Empty;
                break;
        }
        model.Name = "Must not commit";
        var rejected = await service.SaveEditorAsync(model);

        Assert.Contains(rejected.Errors, error => error.Code == ProjectErrorCodes.DefinitionChildInvalid);
        var current = await service.GetAsync(id);
        Assert.Equal("Original project", current.Name);
        Assert.Equal(originalRevision, current.ExpectedDefinition);
        Assert.Single(current.Phases);
    }

    [Fact]
    public async Task Clearing_a_persisted_optional_row_deletes_it_and_acknowledges_its_absence() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var id = await CreateAsync(service);
        var model = await service.GetAsync(id);
        var option = Assert.Single(model.Options, row => row.Id.HasValue);
        option.OptionName = " ";
        option.Notes = " ";

        var accepted = await service.SaveEditorAsync(model);

        Assert.True(accepted.IsSuccess);
        Assert.Empty(accepted.Value!.Options);
        Assert.Empty((await service.GetDefinitionAsync(id)).Value!.Options);
    }

    [Fact]
    public async Task Accepted_acknowledgement_remains_exact_after_a_later_write() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var id = await CreateAsync(service);
        var first = await service.GetAsync(id);
        first.Name = "  First accepted name  ";
        first.Phases.Add(new() { Name = "  First accepted child  " });
        var accepted = await service.SaveEditorAsync(first);
        Assert.True(accepted.IsSuccess);
        var next = await service.GetAsync(id);
        next.Name = "Later current name";
        next.Phases.Clear();
        Assert.True((await service.SaveEditorAsync(next)).IsSuccess);

        Assert.Equal("First accepted name", accepted.Value!.Name);
        Assert.Equal("First accepted child", accepted.Value.Phases[1].Name);
        Assert.NotEqual(Guid.Empty, accepted.Value.Phases[1].Id);
        var current = (await service.GetDefinitionAsync(id)).Value!;
        Assert.Equal("Later current name", current.Scalars.Name);
        Assert.Empty(current.Phases);
        Assert.NotEqual(accepted.Value.Definition, current.Expectation);
    }

    [Fact]
    public async Task Coherent_read_keeps_root_and_children_from_one_snapshot_while_another_writer_commits() {
        var probe = new DefinitionReadPause();
        await using var application = await TestApplication.CreateAsync(new TestHarnessOptions {
            ConfigureServices = services => services.ConfigureDbContext<ProjectsDbContext>(
                options => options.AddInterceptors(probe), ServiceLifetime.Singleton)
        });
        await using var scope = application.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var id = await CreateAsync(service);
        var changing = await service.GetAsync(id);
        var expected = changing.ExpectedDefinition;
        probe.Arm();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var reading = service.GetDefinitionAsync(id, deadline.Token);
        try {
            await probe.RootRead.Task.WaitAsync(deadline.Token);
            changing.Name = "New root";
            changing.Phases[0].Name = "New phase";
            Assert.True((await service.SaveEditorAsync(changing, deadline.Token)).IsSuccess);
        } finally {
            probe.Release.TrySetResult();
            await reading;
        }
        var snapshot = (await reading).Value!;
        Assert.Equal(expected, snapshot.Expectation);
        Assert.Equal("Original project", snapshot.Scalars.Name);
        Assert.Equal("Original phase", Assert.Single(snapshot.Phases).Name);
        Assert.Equal("New root", (await service.GetAsync(id)).Name);
    }

    [Fact]
    public async Task Root_compare_and_swap_rolls_back_child_changes_when_revision_changes_after_admission() {
        var probe = new DefinitionWritePause();
        await using var application = await TestApplication.CreateAsync(new TestHarnessOptions {
            ConfigureServices = services => services.ConfigureDbContext<ProjectsDbContext>(
                options => options.AddInterceptors(probe), ServiceLifetime.Singleton)
        });
        await using var scope = application.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var id = await CreateAsync(service);
        var original = (await service.GetDefinitionAsync(id)).Value!;
        var draft = await service.GetAsync(id);
        draft.Name = "Rejected root";
        draft.Phases.Clear();
        draft.Options.Clear();
        probe.Arm();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var writing = service.SaveEditorAsync(draft, deadline.Token);
        var competingRevision = Guid.NewGuid();
        try {
            await probe.Writing.Task.WaitAsync(deadline.Token);
            await using var competing = await application.Services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>()
                .CreateDbContextAsync(deadline.Token);
            await competing.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"Projects_Projects\" SET \"ConcurrencyToken\" = {competingRevision} WHERE \"Id\" = {id}", deadline.Token);
        } finally {
            probe.Release.TrySetResult();
            await writing;
        }
        Assert.Contains((await writing).Errors, error => error.Code == ProjectErrorCodes.DefinitionConflict);
        var current = (await service.GetDefinitionAsync(id)).Value!;
        Assert.Equal(competingRevision, current.Expectation.Revision);
        Assert.Equal(original.Scalars, current.Scalars);
        Assert.Equal(original.Phases, current.Phases);
        Assert.Equal(original.Options, current.Options);
    }

    [Fact]
    public async Task A_blank_option_with_another_projects_identity_cannot_delete_or_bypass_child_validation() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var id = await CreateAsync(service);
        var otherId = await CreateAsync(service);
        var original = (await service.GetDefinitionAsync(id)).Value!;
        var other = (await service.GetDefinitionAsync(otherId)).Value!;
        var draft = await service.GetAsync(id);
        draft.Options = [new() { Id = Assert.Single(other.Options).Id, Category = ProjectOptionCategory.Language }];

        var rejected = await service.SaveEditorAsync(draft);

        Assert.Contains(rejected.Errors, error => error.Code == ProjectErrorCodes.DefinitionChildInvalid);
        var current = (await service.GetDefinitionAsync(id)).Value!;
        Assert.Equal(original.Expectation, current.Expectation);
        Assert.Equal(original.Options, current.Options);
        Assert.Equal(other.Expectation, (await service.GetDefinitionAsync(otherId)).Value!.Expectation);
    }

    [Fact]
    public async Task Definition_read_of_missing_identity_is_not_a_new_project_template() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var result = await service.GetDefinitionAsync(Guid.NewGuid());
        Assert.Contains(result.Errors, error => error.Code == ProjectErrorCodes.NotFound);
        Assert.Null(result.Value);
    }

    private static async Task<Guid> CreateAsync(ProjectsService service) {
        var result = await service.SaveAsync(new() {
            Name = "Original project",
            Objective = "Original objective",
            Phases = [new() { Name = "Original phase", Goal = "Original goal" }],
            Options = [new() { Category = ProjectOptionCategory.Language, OptionName = "Original option", Notes = "Original notes" }]
        });
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    public enum InvalidChild { Unknown, Foreign, Duplicate, Empty }

    private sealed class FixedClock : IClock {
        public DateTimeOffset GetUtcNow() => new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class DefinitionReadPause : DbCommandInterceptor {
        private int armed;
        public TaskCompletionSource RootRead { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Arm() => Interlocked.Exchange(ref armed, 1);

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default) {
            if (command.CommandText.Contains("FROM \"Projects_Projects\"", StringComparison.Ordinal) &&
                Interlocked.CompareExchange(ref armed, 0, 1) == 1) {
                RootRead.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
    }

    private sealed class DefinitionWritePause : DbCommandInterceptor {
        private int armed;
        public TaskCompletionSource Writing { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Arm() => Interlocked.Exchange(ref armed, 1);

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            if (command.CommandText.Contains("UPDATE \"Projects_Projects\"", StringComparison.Ordinal) &&
                Interlocked.CompareExchange(ref armed, 0, 1) == 1) {
                Writing.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
    }
}
