using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectCreationNamespaceIntegrationTests {
    [Fact]
    public Task Reservation_and_creation_receipts_retain_original_namespace_with_nullable_history_only_schema() => RunAsync(async (services, open) => {
        var admissions = services.GetRequiredService<ProjectWriteAdmissionService>();
        var binding = await admissions.CaptureCreationBindingAsync(CancellationToken.None);
        var reservation = await admissions.ReserveCreationAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var projects = services.GetRequiredService<ProjectsService>();
        var result = await projects.CreateWithReceiptAsync(reservation, new() { Name = "Original namespace receipt" });
        Assert.True(result.IsSuccess);
        var receipt = Assert.IsType<ProjectCreationReceipt>(result.Value);
        Assert.Equal(binding.Binding.Namespace, receipt.Namespace);
        Assert.Equal(reservation, receipt.Reservation);
        var context = await open();
        var record = await context.Set<ProjectCreationReservationRecord>().SingleAsync(row => row.Id == reservation.Id);
        Assert.Equal(binding.Binding.Namespace.Value, record.NamespaceId);
        Assert.Equal(ProjectCreationReservationState.Consumed, record.State);
        Assert.Null(record.ImportedHistory);
        var entity = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(ProjectCreationReservationRecord))!;
        var property = entity.FindProperty(nameof(ProjectCreationReservationRecord.NamespaceId))!;
        Assert.True(property.IsNullable);
        Assert.Equal(typeof(Guid?), property.ClrType);
        Assert.Equal("uuid", property.GetColumnType());
        Assert.Null(property.FindAnnotation(RelationalAnnotationNames.DefaultValue));
        Assert.Null(property.GetDefaultValueSql());
        Assert.DoesNotContain(entity.GetIndexes(), index => index.Properties.Contains(property));
        Assert.Contains(entity.GetCheckConstraints(), check => check.Name == "CK_Projects_CreationReservation_Namespace");
        record.NamespaceId = null;
        await context.SaveChangesAsync();
        Assert.IsType<ProjectCreationCompensationOutcome.Declined>(await projects.CompensateCreationAsync(receipt));
        Assert.True(await context.Set<Project>().AsNoTracking().AnyAsync(project => project.Id == receipt.Project.ProjectId && project.LifetimeId == receipt.Project.LifetimeId));
        context.ChangeTracker.Clear();
        Assert.Null((await context.Set<ProjectCreationReservationRecord>().SingleAsync(row => row.Id == reservation.Id)).NamespaceId);
    });

    [Fact]
    public Task Legacy_foreign_namespace_and_imported_reservations_never_gain_current_creation_or_cancellation_authority() => RunAsync(async (services, open) => {
        var admissions = services.GetRequiredService<ProjectWriteAdmissionService>();
        var projects = services.GetRequiredService<ProjectsService>();
        var context = await open();
        foreach (var state in Enum.GetValues<UnboundState>()) {
            var reservation = await admissions.ReserveCreationAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
            var record = await context.Set<ProjectCreationReservationRecord>().SingleAsync(row => row.Id == reservation.Id);
            if (state == UnboundState.Legacy) {
                record.NamespaceId = null;
            } else if (state == UnboundState.ForeignNamespace) {
                record.NamespaceId = Guid.NewGuid();
            } else {
                record.ImportedHistory = new(Guid.NewGuid(), Guid.NewGuid());
            }
            await context.SaveChangesAsync();
            var originalNamespace = record.NamespaceId;
            var originalImport = record.ImportedHistory;
            if (state == UnboundState.Imported) {
                var refused = await projects.CreateAsync(reservation, new() { Name = "Must not create" });
                Assert.True(refused.IsFailure);
                Assert.Contains(refused.Errors, error => error.Code == ProjectErrorCodes.ReservationClosed);
            } else {
                await Assert.ThrowsAsync<InvalidOperationException>(() => projects.CreateAsync(reservation, new() { Name = "Must not create" }));
            }
            await Assert.ThrowsAsync<InvalidOperationException>(() => admissions.CaptureReservationCancellationAsync(reservation));
            await Assert.ThrowsAsync<InvalidOperationException>(() => admissions.RequireCreationGrantAsync(reservation));
            await Assert.ThrowsAsync<InvalidOperationException>(() => admissions.ReserveCreationAsync(reservation.ProjectId, reservation.RequesterId, reservation.Id));
            context.ChangeTracker.Clear();
            var retained = await context.Set<ProjectCreationReservationRecord>().SingleAsync(row => row.Id == reservation.Id);
            Assert.Equal(originalNamespace, retained.NamespaceId);
            Assert.Equal(originalImport, retained.ImportedHistory);
            Assert.Equal(ProjectCreationReservationState.Reserved, retained.State);
            Assert.Null(retained.ConsumedAtUtc);
            Assert.Null(retained.CancelledAtUtc);
            Assert.False(await context.Set<Project>().AnyAsync(project => project.Id == reservation.ProjectId));
        }
    });

    [Fact]
    public Task Consumed_creation_read_provenance_requires_exact_native_namespace_and_live_tuple() => RunAsync(async (services, open) => {
        var admissions = services.GetRequiredService<ProjectWriteAdmissionService>();
        var projects = services.GetRequiredService<ProjectsService>();
        var binding = (await admissions.CaptureCreationBindingAsync(CancellationToken.None)).Binding;
        var context = await open();
        foreach (var state in Enum.GetValues<UnboundState>()) {
            var reservation = await admissions.ReserveCreationAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
            await Assert.ThrowsAsync<InvalidOperationException>(() => admissions.RequireConsumedCreationsForReadAsync([reservation], binding.Namespace));
            Assert.True((await projects.CreateWithReceiptAsync(reservation, new() { Name = "Native provenance" })).IsSuccess);
            await admissions.RequireConsumedCreationsForReadAsync([reservation], binding.Namespace);
            await Assert.ThrowsAsync<InvalidOperationException>(() => admissions.RequireConsumedCreationsForReadAsync(
                [reservation with { RequesterId = Guid.NewGuid() }], binding.Namespace));
            var record = await context.Set<ProjectCreationReservationRecord>().SingleAsync(row => row.Id == reservation.Id);
            if (state == UnboundState.Legacy) {
                record.NamespaceId = null;
            } else if (state == UnboundState.ForeignNamespace) {
                record.NamespaceId = Guid.NewGuid();
            } else {
                record.ImportedHistory = new(Guid.NewGuid(), Guid.NewGuid());
            }
            await context.SaveChangesAsync();
            var originalNamespace = record.NamespaceId;
            var originalImport = record.ImportedHistory;
            await Assert.ThrowsAsync<InvalidOperationException>(() => admissions.RequireConsumedCreationsForReadAsync([reservation], binding.Namespace));
            context.ChangeTracker.Clear();
            var retained = await context.Set<ProjectCreationReservationRecord>().SingleAsync(row => row.Id == reservation.Id);
            Assert.Equal(originalNamespace, retained.NamespaceId);
            Assert.Equal(originalImport, retained.ImportedHistory);
            Assert.Equal(ProjectCreationReservationState.Consumed, retained.State);
            Assert.True(await context.Set<Project>().AnyAsync(project => project.Id == reservation.ProjectId && project.LifetimeId == reservation.LifetimeId));
        }
    });

    [Fact]
    public Task Hierarchy_creation_read_provenance_is_bounded_to_three_distinct_exact_endpoints() => RunAsync(async (services, open) => {
        var admissions = services.GetRequiredService<ProjectWriteAdmissionService>();
        var projects = services.GetRequiredService<ProjectsService>();
        var binding = (await admissions.CaptureCreationBindingAsync(CancellationToken.None)).Binding;
        var reservations = new List<ProjectCreationReservation>();
        for (var index = 0; index < 3; index++) {
            var reservation = await admissions.ReserveCreationAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
            Assert.True((await projects.CreateWithReceiptAsync(reservation, new() { Name = "Bounded native endpoint" })).IsSuccess);
            reservations.Add(reservation);
        }
        await admissions.RequireConsumedCreationsForReadAsync(reservations, binding.Namespace);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => admissions.RequireConsumedCreationsForReadAsync(
            [.. reservations, reservations[0]], binding.Namespace));
        await Assert.ThrowsAsync<InvalidOperationException>(() => admissions.RequireConsumedCreationsForReadAsync(
            [reservations[0], reservations[0] with { LifetimeId = Guid.NewGuid() }], binding.Namespace));
        await Assert.ThrowsAsync<InvalidOperationException>(() => admissions.RequireConsumedCreationsForReadAsync(
            reservations, new(Guid.NewGuid())));
        var context = await open();
        Assert.Equal(3, await context.Set<ProjectCreationReservationRecord>().CountAsync(row => row.State == ProjectCreationReservationState.Consumed));
        Assert.Equal(3, await context.Set<Project>().CountAsync());
        Assert.Equal(0, await context.Set<ProjectLifecycleReceiptRecord>().CountAsync());
    });

    private static async Task RunAsync(Func<IServiceProvider, Func<Task<ProjectsDbContext>>, Task> test) {
        var application = await TestApplication.CreateAsync();
        var owned = new List<IAsyncDisposable> { application };
        Exception? primary = null;
        try {
            var scope = application.Services.CreateAsyncScope();
            owned.Add(scope);
            var services = scope.ServiceProvider;
            await test(services, async () => {
                var context = await services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
                owned.Add(context);
                return context;
            });
        } catch (Exception failure) {
            primary = failure;
            throw;
        } finally {
            var resources = owned.AsEnumerable().Reverse().ToArray();
            if (primary is null) {
                await TestFixtureCleanup.DisposeAsync(resources);
            } else {
                await TestFixtureCleanup.DisposeAfterFailureAsync(primary, resources);
            }
        }
    }

    private enum UnboundState { Legacy, ForeignNamespace, Imported }
}
