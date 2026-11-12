using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Integration.CrmHr;

public sealed class StaffingRequestNativeBoundaryTests {
    [Fact]
    public async Task The_independent_staffing_owner_returns_its_exact_canonical_receipt_without_the_HR_facade() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var admission = await StaffingRequestConcurrencyTests.CreateProjectAsync(services);
        var owner = new StaffingRequestEditingService(services.GetRequiredService<IDbContextFactory<CrmHrDbContext>>(),
            services.GetRequiredService<ProjectWriteAdmissionService>(), services.GetRequiredService<CoordinatedDatabaseTransaction>(),
            services.GetRequiredService<CrmHrChanges>(), NullLogger<StaffingRequestEditingService>.Instance);
        var draft = StaffingRequestConcurrencyTests.NewRequest(admission);
        draft.Title = "  Canonical staffing demand  ";
        draft.Notes = "  Accepted native demand  ";
        StaffingRequestSaveReceipt? accepted = null;

        var result = await owner.SaveAsync(draft, onCommitted: receipt => accepted = receipt);

        Assert.True(result.IsSuccess);
        Assert.NotNull(accepted);
        Assert.Equal(result.Value, accepted.Id);
        Assert.Equal("Canonical staffing demand", accepted.Accepted.Title);
        Assert.Equal("Accepted native demand", accepted.Accepted.Notes);
        var current = (await owner.ReadAsync(result.Value))!;
        Assert.Equal(admission, current.ExpectedProjectAdmission);
        Assert.Equal(accepted.Accepted.Original! with { SkillIds = default }, current.Original! with { SkillIds = default });
        Assert.True(accepted.Accepted.Original.SkillIds.AsSpan().SequenceEqual(current.Original.SkillIds.AsSpan()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Current_authority_after_SQL_and_a_failed_accepted_callback_have_distinct_physical_outcomes(bool afterCommit) {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var project = await StaffingRequestConcurrencyTests.CreateProjectAsync(services);
        var owner = services.GetRequiredService<IStaffingRequestEditing>();
        var draft = StaffingRequestConcurrencyTests.NewRequest(project);
        var checks = 0;
        StaffingRequestSaveReceipt? accepted = null;
        using var authority = services.GetRequiredService<CrmHrChanges>().EnterUiWrite(_ => {
            if (!afterCommit && ++checks == 3) {
                throw new UnauthorizedAccessException("The current grant was revoked after the actual SQL flush.");
            }
            return ValueTask.CompletedTask;
        });

        var failure = await Record.ExceptionAsync(() => owner.SaveAsync(draft, onCommitted: receipt => {
            accepted = receipt;
            throw new DbUpdateConcurrencyException("The accepted callback failed after physical commit.");
        }));

        await using var context = await services.GetRequiredService<IDbContextFactory<CrmHrDbContext>>().CreateDbContextAsync();
        if (afterCommit) {
            Assert.IsType<DbUpdateConcurrencyException>(failure);
            Assert.NotNull(accepted);
            Assert.Equal(accepted.Id, (await context.Set<StaffingRequest>().SingleAsync()).Id);
        } else {
            Assert.IsType<UnauthorizedAccessException>(failure);
            Assert.Equal(3, checks);
            Assert.Null(accepted);
            Assert.Empty(await context.Set<StaffingRequest>().ToListAsync());
        }
    }

    [Fact]
    public async Task A_deleted_observed_request_cannot_be_recreated_by_a_late_save() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var project = await StaffingRequestConcurrencyTests.CreateProjectAsync(services);
        var owner = services.GetRequiredService<IStaffingRequestEditing>();
        var created = await owner.SaveAsync(StaffingRequestConcurrencyTests.NewRequest(project));
        Assert.True(created.IsSuccess);
        var shown = (await owner.ReadAsync(created.Value))!;
        Assert.True((await owner.DeleteAsync(created.Value, shown.Original)).IsSuccess);
        shown.Notes = "Late request resurrection";

        var result = await owner.SaveAsync(shown);

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == StaffingRequestEditingErrors.Conflict);
        Assert.Null(await owner.ReadAsync(created.Value));
    }
}
