using System.IO.Compression;
using CanDoItAll.Infrastructure.BackgroundJobs;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CognitiveMemory;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CanDoItAll.Tests.Integration;

public sealed class DatabaseSwitchIntegrationTests
{
    [Fact]
    public async Task SwitchAsync_saves_activation_for_next_start_without_changing_running_context()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-runtime-switch");
        await using var provider = DatabaseProfileControlPlaneIntegrationHost.BuildServiceProvider(testEnvironment);

        var profileService = provider.GetRequiredService<IDatabaseProfileService>();
        var initialTestProfile = testEnvironment.CreatePostgreSqlProfile("runtime-switch-alpha");
        var initialSaveResult = await profileService.SaveAsync(TestDatabaseProfileEditorFactory.CreatePostgreSqlEditor(
            initialTestProfile,
            "PostgreSQL alpha"));
        Assert.True(initialSaveResult.IsSuccess, string.Join(" ", initialSaveResult.Errors.Select(error => error.Message)));
        Assert.True((await profileService.ActivateAsync(initialSaveResult.Value)).IsSuccess);

        var runtimeAccessor = provider.GetRequiredService<IDatabaseProfileRuntimeAccessor>();
        var switchCoordinator = provider.GetRequiredService<IDatabaseSwitchCoordinator>();
        var runtimeState = provider.GetRequiredService<IDatabaseRuntimeState>();
        var bootstrapper = provider.GetRequiredService<IAppDatabaseBootstrapper>();
        var dbContextFactory = provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var profileFactory = provider.GetRequiredService<IProfileAppDbContextFactory>();
        var initialProfile = runtimeAccessor.ResolveProfile(initialSaveResult.Value);
        await bootstrapper.EnsureCurrentProfileReadyAsync();

        await using (var initialContext = await dbContextFactory.CreateDbContextAsync())
        {
            initialContext.Set<BackgroundJobRecord>().Add(new BackgroundJobRecord
            {
                JobType = "alpha",
                Description = "alpha profile job",
                CorrelationId = Guid.NewGuid(),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
            await initialContext.SaveChangesAsync();
        }

        var targetTestProfile = testEnvironment.CreatePostgreSqlProfile("runtime-switch-beta");
        var saveResult = await profileService.SaveAsync(TestDatabaseProfileEditorFactory.CreatePostgreSqlEditor(
            targetTestProfile,
            "PostgreSQL beta"));

        Assert.True(saveResult.IsSuccess);

        var targetProfile = runtimeAccessor.ResolveProfile(saveResult.Value);
        await bootstrapper.EnsureProfileReadyAsync(targetProfile);

        await using (var targetContext = await profileFactory.CreateDbContextForProfileAsync(targetProfile))
        {
            targetContext.Set<BackgroundJobRecord>().Add(new BackgroundJobRecord
            {
                JobType = "beta",
                Description = "beta profile job",
                CorrelationId = Guid.NewGuid(),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
            await targetContext.SaveChangesAsync();
        }

        var processIdBeforeSwitch = Environment.ProcessId;
        var firstSwitch = await switchCoordinator.SwitchAsync(targetProfile.Profile.Id);

        Assert.True(firstSwitch.IsSuccess);
        Assert.Equal(processIdBeforeSwitch, firstSwitch.Value!.ProcessId);
        Assert.True(firstSwitch.Value.RequiresRestart);
        Assert.False(firstSwitch.Value.RuntimeChangedInProcess);
        Assert.Equal(initialProfile.Profile.Id, firstSwitch.Value.RuntimeProfileId);
        Assert.Equal(targetProfile.Profile.Id, firstSwitch.Value.PendingRestartProfileId);

        await using (var stillRunningContext = await dbContextFactory.CreateDbContextAsync())
        {
            var descriptions = await stillRunningContext.Set<BackgroundJobRecord>()
                .OrderBy(job => job.Description)
                .Select(job => job.Description)
                .ToListAsync();

            Assert.Equal(["alpha profile job"], descriptions);
        }

        var persistedSelection = await profileService.GetCurrentSelectionAsync();
        Assert.Equal(targetProfile.Profile.Id, persistedSelection.ActiveProfileId);

        var runtimeSnapshot = runtimeState.GetSnapshot();
        Assert.Equal(initialProfile.Profile.Id, runtimeSnapshot.ActiveProfileId);
        Assert.Equal(0, runtimeSnapshot.Generation);

        await using var restartedProvider = DatabaseProfileControlPlaneIntegrationHost.BuildServiceProvider(testEnvironment);
        var restartedRuntimeAccessor = restartedProvider.GetRequiredService<IDatabaseProfileRuntimeAccessor>();
        var restartedBootstrapper = restartedProvider.GetRequiredService<IAppDatabaseBootstrapper>();
        var restartedDbContextFactory = restartedProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var restartedProfile = restartedRuntimeAccessor.ResolveCurrentProfile();
        Assert.Equal(targetProfile.Profile.Id, restartedProfile.Profile.Id);
        await restartedBootstrapper.EnsureCurrentProfileReadyAsync();

        await using (var restartedContext = await restartedDbContextFactory.CreateDbContextAsync())
        {
            var descriptions = await restartedContext.Set<BackgroundJobRecord>()
                .OrderBy(job => job.Description)
                .Select(job => job.Description)
                .ToListAsync();

            Assert.Equal(["beta profile job"], descriptions);
        }
    }
}

public sealed class DatabaseDriverBootstrapIntegrationTests
{
    [Fact]
    public async Task PostgreSql_driver_can_create_and_bootstrap_an_empty_database()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-postgres-driver");
        var availability = await PostgresTestAvailability.EnsureAvailableAsync("C:\\repositories\\CanDoItAll");
        Assert.True(availability.IsAvailable, availability.Message);

        var baseBuilder = new NpgsqlConnectionStringBuilder(availability.ConnectionString);
        var databaseName = $"candoitall_switch_{Guid.NewGuid():N}"[..30];

        await using var provider = DatabaseProfileControlPlaneIntegrationHost.BuildServiceProvider(testEnvironment);
        var profileService = provider.GetRequiredService<IDatabaseProfileService>();
        var runtimeAccessor = provider.GetRequiredService<IDatabaseProfileRuntimeAccessor>();
        var driverRegistry = provider.GetRequiredService<IDatabaseDriverRegistry>();
        var bootstrapper = provider.GetRequiredService<IAppDatabaseBootstrapper>();
        var profileFactory = provider.GetRequiredService<IProfileAppDbContextFactory>();

        var saveResult = await profileService.SaveAsync(new DatabaseProfileEditorModel
        {
            DisplayName = "Docker postgres target",
            ProviderKind = DatabaseProviderKind.PostgreSql,
            SourceKind = DatabaseProfileSourceKind.PostgresConnection,
            PostgresHost = baseBuilder.Host ?? "127.0.0.1",
            PostgresPort = baseBuilder.Port,
            PostgresDatabaseName = databaseName,
            PostgresUsername = baseBuilder.Username ?? "candoitall",
            PostgresPassword = baseBuilder.Password ?? "candoitall",
            PostgresAdminDatabaseName = baseBuilder.Database,
            WorkspaceRoot = Path.Combine(testEnvironment.RootPath, "postgres-workspace")
        });

        Assert.True(saveResult.IsSuccess);

        var profile = runtimeAccessor.ResolveProfile(saveResult.Value);
        var driver = driverRegistry.Resolve(DatabaseProviderKind.PostgreSql);

        try
        {
            await driver.CreateEmptyAsync(profile);
            await driver.EnsureDatabaseAsync(profile);
            await bootstrapper.EnsureProfileReadyAsync(profile);

            await using var dbContext = await profileFactory.CreateDbContextForProfileAsync(profile);
            Assert.True(await dbContext.Database.CanConnectAsync());
            Assert.Contains(
                await dbContext.Database.GetAppliedMigrationsAsync(),
                migrationId => migrationId.Contains("InitialPostgreSqlBaseline", StringComparison.Ordinal));
        }
        finally
        {
            var adminBuilder = new NpgsqlConnectionStringBuilder(availability.ConnectionString)
            {
                Database = string.IsNullOrWhiteSpace(baseBuilder.Database) ? "postgres" : baseBuilder.Database
            };

            await using var adminConnection = new NpgsqlConnection(adminBuilder.ConnectionString);
            await adminConnection.OpenAsync();
            await using var dropCommand = adminConnection.CreateCommand();
            dropCommand.CommandText = $"drop database if exists \"{databaseName}\" with (force);";
            await dropCommand.ExecuteNonQueryAsync();
        }
    }
}

public sealed class DatabaseTransferIntegrationTests
{
    [Fact]
    public async Task Project_transfer_copies_all_project_and_workbench_records_between_profiles()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-project-transfer");
        var sourceTestProfile = testEnvironment.CreatePostgreSqlProfile("project-transfer-source");
        Guid sourceProfileId;
        await using (var setupProvider = DatabaseProfileControlPlaneIntegrationHost.BuildServiceProvider(testEnvironment))
        {
            var setupProfileService = setupProvider.GetRequiredService<IDatabaseProfileService>();
            var sourceSaveResult = await setupProfileService.SaveAsync(TestDatabaseProfileEditorFactory.CreatePostgreSqlEditor(
                sourceTestProfile,
                "PostgreSQL project transfer source"));
            Assert.True(sourceSaveResult.IsSuccess, DescribeErrors(sourceSaveResult.Errors));
            Assert.True((await setupProfileService.ActivateAsync(sourceSaveResult.Value)).IsSuccess);
            sourceProfileId = sourceSaveResult.Value;
        }

        await using var provider = DatabaseProfileControlPlaneIntegrationHost.BuildServiceProvider(testEnvironment);

        var runtimeAccessor = provider.GetRequiredService<IDatabaseProfileRuntimeAccessor>();
        var profileService = provider.GetRequiredService<IDatabaseProfileService>();
        var bootstrapper = provider.GetRequiredService<IAppDatabaseBootstrapper>();
        var profileFactory = provider.GetRequiredService<IProfileAppDbContextFactory>();

        var sourceProfile = runtimeAccessor.ResolveCurrentProfile();
        Assert.Equal(sourceProfileId, sourceProfile.Profile.Id);
        await bootstrapper.EnsureCurrentProfileReadyAsync();

        var sourceProjectName = $"Transferred Project {Guid.NewGuid():N}"[..32];
        var sourceNodeTitle = "Transfer note";
        Guid sourceProjectId;
        string sourceNodeKey;

        await using (var sourceScope = provider.CreateAsyncScope())
        {
            var saveResult = await sourceScope.ServiceProvider.GetRequiredService<ProjectsService>().SaveAsync(new ProjectEditorModel
            {
                Name = sourceProjectName,
                Description = "Transfer source description",
                Objective = "Transfer source objective",
                CurrentPhase = "Discovery",
                Phases =
                [
                    new ProjectPhaseEditorModel
                    {
                        Name = "Discovery",
                        Goal = "Confirm transfer coverage",
                        Status = ProjectPhaseStatus.Active
                    }
                ],
                Options =
                [
                    new ProjectOptionEditorModel
                    {
                        Category = ProjectOptionCategory.Language,
                        OptionName = "C#",
                        Notes = "Transfer option"
                    }
                ]
            });
            Assert.True(saveResult.IsSuccess, DescribeErrors(saveResult.Errors));
            sourceProjectId = saveResult.Value;

            await sourceScope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>().SeedProjectObjectsAsync(
                sourceProjectId,
                [
                    new ProjectObjectSeedRequest(
                        ProjectObjectType.Note,
                        sourceNodeTitle,
                        "Transfer subtitle",
                        "Transfer notes")
                ]);
        }

        await using (var sourceContext = await profileFactory.CreateDbContextForProfileAsync(sourceProfile))
        {
            var seededNode = await sourceContext.Set<ProjectObjectRecord>()
                .SingleAsync(item => item.ProjectId == sourceProjectId && item.Title == sourceNodeTitle);
            sourceNodeKey = seededNode.NodeKey;

            sourceContext.Set<ProjectStructureProjectionLayoutRecord>().Add(new ProjectStructureProjectionLayoutRecord
            {
                ProjectId = sourceProjectId,
                NodeKey = sourceNodeKey,
                PositionX = 42,
                PositionY = 84,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
            sourceContext.Set<ProjectWorkbenchViewStateRecord>().Add(new ProjectWorkbenchViewStateRecord
            {
                ProjectId = sourceProjectId,
                SurfaceKind = "structure",
                StateJson = "{\"zoom\":1.25}",
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
            await sourceContext.SaveChangesAsync();
        }

        var targetTestProfile = testEnvironment.CreatePostgreSqlProfile("project-transfer-target");
        var targetSaveResult = await profileService.SaveAsync(TestDatabaseProfileEditorFactory.CreatePostgreSqlEditor(
            targetTestProfile,
            "PostgreSQL project transfer target"));
        Assert.True(targetSaveResult.IsSuccess, DescribeErrors(targetSaveResult.Errors));

        var targetProfile = runtimeAccessor.ResolveProfile(targetSaveResult.Value);
        await bootstrapper.EnsureProfileReadyAsync(targetProfile);

        await using (var targetContext = await profileFactory.CreateDbContextForProfileAsync(targetProfile))
        {
            targetContext.Set<Project>().Add(new Project
            {
                Name = "Target-only Project",
                Slug = "target-only-project",
                Description = "Should be replaced",
                Objective = "Should be replaced",
                CurrentPhase = "Legacy",
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
            await targetContext.SaveChangesAsync();
        }

        DatabaseTransferResult transferResult;
        await using (var transferScope = provider.CreateAsyncScope())
        {
            var transferService = transferScope.ServiceProvider.GetRequiredService<IDatabaseTransferService>();

            var previews = await transferService.PreviewAsync(sourceProfile.Profile.Id, targetProfile.Profile.Id);
            var projectPreview = Assert.Single(previews, item => item.Descriptor.Key == "projects");
            Assert.True(projectPreview.IsAvailable);
            Assert.True(projectPreview.SourceRecordCount >= 6);
            Assert.True(projectPreview.TargetRecordCount >= 1);

            transferResult = await transferService.TransferAsync(new DatabaseTransferRequest
            {
                SourceProfileId = sourceProfile.Profile.Id,
                TargetProfileId = targetProfile.Profile.Id,
                ItemKeys = ["projects"]
            });
        }

        Assert.True(transferResult.IsSuccess, DescribeTransferResults(transferResult));
        var itemResult = Assert.Single(transferResult.Items);
        Assert.Equal("projects", itemResult.Key);
        Assert.True(itemResult.RecordsCopied >= 6);

        await using (var targetContext = await profileFactory.CreateDbContextForProfileAsync(targetProfile))
        {
            var projects = await targetContext.Set<Project>().ToListAsync();
            var transferredProject = Assert.Single(projects, item => item.Id == sourceProjectId);
            Assert.Equal(sourceProjectName, transferredProject.Name);
            Assert.DoesNotContain(projects, item => item.Name == "Target-only Project");

            Assert.True(await targetContext.Set<ProjectPhase>().AnyAsync(item => item.ProjectId == sourceProjectId && item.Name == "Discovery"));
            Assert.True(await targetContext.Set<ProjectOptionSelection>().AnyAsync(item => item.ProjectId == sourceProjectId && item.OptionName == "C#"));

            var transferredNode = await targetContext.Set<ProjectObjectRecord>()
                .SingleAsync(item => item.ProjectId == sourceProjectId && item.NodeKey == sourceNodeKey);
            Assert.Equal(sourceNodeTitle, transferredNode.Title);
            Assert.True(await targetContext.Set<ProjectNodeBindingRecord>().AnyAsync(item => item.ProjectObjectId == transferredNode.Id));
            Assert.True(await targetContext.Set<ProjectStructureProjectionLayoutRecord>().AnyAsync(item => item.ProjectId == sourceProjectId && item.NodeKey == sourceNodeKey));
            Assert.True(await targetContext.Set<ProjectWorkbenchViewStateRecord>().AnyAsync(item => item.ProjectId == sourceProjectId && item.SurfaceKind == "structure" && item.StateJson == "{\"zoom\":1.25}"));
        }
    }

    [Fact]
    public async Task DatabaseTransferService_CopiesCognitiveMemorySourceTruthIntoCleanTarget()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-cognitive-memory-source-truth-transfer");
        await using var provider = DatabaseProfileControlPlaneIntegrationHost.BuildServiceProvider(testEnvironment);

        var profileService = provider.GetRequiredService<IDatabaseProfileService>();
        var runtimeAccessor = provider.GetRequiredService<IDatabaseProfileRuntimeAccessor>();
        var bootstrapper = provider.GetRequiredService<IAppDatabaseBootstrapper>();
        var profileFactory = provider.GetRequiredService<IProfileAppDbContextFactory>();
        var now = DateTimeOffset.UtcNow;
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var manifestId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var sourceItemId = Guid.Parse("10000000-0000-0000-0000-000000000002");
        var evidenceAnchorId = Guid.Parse("10000000-0000-0000-0000-000000000003");
        var operationId = Guid.Parse("10000000-0000-0000-0000-000000000004");

        var sourceTestProfile = testEnvironment.CreatePostgreSqlProfile("cognitive-memory-transfer-source");
        var sourceSaveResult = await profileService.SaveAsync(TestDatabaseProfileEditorFactory.CreatePostgreSqlEditor(
            sourceTestProfile,
            "Cognitive memory source truth transfer source"));
        Assert.True(sourceSaveResult.IsSuccess, DescribeErrors(sourceSaveResult.Errors));

        var sourceProfile = runtimeAccessor.ResolveProfile(sourceSaveResult.Value);
        await bootstrapper.EnsureProfileReadyAsync(sourceProfile);
        await using (var sourceContext = await profileFactory.CreateDbContextForProfileAsync(sourceProfile))
        {
            sourceContext.Add(new CognitiveMemorySourceManifestRecord
            {
                Id = manifestId,
                ProjectId = projectId,
                SourceSystem = "ExternalFile",
                SourceScopeKey = "project:aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
                SourceSnapshotId = "snapshot:file-transfer-test",
                SnapshotHash = CognitiveMemoryHash.FromUtf8("source truth manifest").Value,
                ProviderVersion = "unit-test",
                ScanStatus = CognitiveMemoryRunStatus.Succeeded,
                ObservedAtUtc = now,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                ConcurrencyToken = Guid.NewGuid()
            });
            sourceContext.Add(new CognitiveMemorySourceItemRecord
            {
                Id = sourceItemId,
                SourceManifestId = manifestId,
                ProjectId = projectId,
                SourceSystem = "ExternalFile",
                SourceItemKey = "docs/validation-source.md",
                SourceItemType = "Markdown",
                Title = "Validation source",
                ContentText = "This source truth states that the deployment validation requires project structure transfer.",
                Locator = "docs/validation-source.md",
                ContentHash = CognitiveMemoryHash.FromUtf8("validation source item").Value,
                RedactionState = CognitiveMemoryRedactionState.Safe,
                AccessLevel = CognitiveMemoryAccessLevel.Project,
                AccessScope = projectId.ToString("D"),
                ProvenanceJson = "{\"origin\":\"integration-test\"}",
                ObservedAtUtc = now,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                ConcurrencyToken = Guid.NewGuid()
            });
            sourceContext.Add(new CognitiveMemoryEvidenceAnchorRecord
            {
                Id = evidenceAnchorId,
                ProjectId = projectId,
                AnchorKind = CognitiveMemoryEvidenceAnchorKind.TextSpan,
                SourceManifestId = manifestId,
                SourceItemId = sourceItemId,
                SourceSystem = "ExternalFile",
                Locator = "docs/validation-source.md#L1",
                StructuredPath = "$.lines[0]",
                TextStart = 0,
                TextEnd = 80,
                QuoteHash = CognitiveMemoryHash.FromUtf8("deployment validation requires project structure transfer").Value,
                TrustLevel = CognitiveMemorySourceTrustLevel.OfficialSource,
                RedactionState = CognitiveMemoryRedactionState.Safe,
                SourceHash = CognitiveMemoryHash.FromUtf8("validation source item").Value,
                ObservedAtUtc = now,
                CreatedAtUtc = now,
                ConcurrencyToken = Guid.NewGuid()
            });
            sourceContext.Add(new CognitiveMemoryExternalSourceIngestionRecord
            {
                Id = operationId,
                ProjectId = projectId,
                SourceKind = CognitiveMemoryExternalSourceKind.UploadedFile,
                Status = CognitiveMemoryExternalSourceIngestionStatus.Succeeded,
                Title = "Validation source",
                Locator = "docs/validation-source.md",
                ContentType = "text/markdown",
                ContentLength = 91,
                ProgressPercent = 100,
                StatusMessage = "Completed",
                SourceManifestId = manifestId,
                SourceItemId = sourceItemId,
                EvidenceAnchorId = evidenceAnchorId,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                CompletedAtUtc = now,
                ConcurrencyToken = Guid.NewGuid()
            });
            await sourceContext.SaveChangesAsync();
        }

        var targetTestProfile = testEnvironment.CreatePostgreSqlProfile("cognitive-memory-transfer-target");
        var targetSaveResult = await profileService.SaveAsync(TestDatabaseProfileEditorFactory.CreatePostgreSqlEditor(
            targetTestProfile,
            "Cognitive memory source truth transfer target"));
        Assert.True(targetSaveResult.IsSuccess, DescribeErrors(targetSaveResult.Errors));

        var targetProfile = runtimeAccessor.ResolveProfile(targetSaveResult.Value);
        await bootstrapper.EnsureProfileReadyAsync(targetProfile);

        DatabaseTransferResult transferResult;
        await using (var transferScope = provider.CreateAsyncScope())
        {
            var transferService = transferScope.ServiceProvider.GetRequiredService<IDatabaseTransferService>();
            var previews = await transferService.PreviewAsync(sourceProfile.Profile.Id, targetProfile.Profile.Id);
            var preview = Assert.Single(previews, item => item.Descriptor.Key == "cognitive-memory-source-truth");
            Assert.True(preview.IsAvailable);
            Assert.Equal(4, preview.SourceRecordCount);

            transferResult = await transferService.TransferAsync(new DatabaseTransferRequest
            {
                SourceProfileId = sourceProfile.Profile.Id,
                TargetProfileId = targetProfile.Profile.Id,
                ItemKeys = ["cognitive-memory-source-truth"]
            });
        }

        Assert.True(transferResult.IsSuccess, DescribeTransferResults(transferResult));
        var itemResult = Assert.Single(transferResult.Items);
        Assert.Equal("cognitive-memory-source-truth", itemResult.Key);
        Assert.Equal(4, itemResult.RecordsCopied);

        await using (var targetContext = await profileFactory.CreateDbContextForProfileAsync(targetProfile))
        {
            var item = await targetContext.Set<CognitiveMemorySourceItemRecord>().SingleAsync(row => row.Id == sourceItemId);
            Assert.Equal("This source truth states that the deployment validation requires project structure transfer.", item.ContentText);
            Assert.Equal(manifestId, item.SourceManifestId);

            var anchor = await targetContext.Set<CognitiveMemoryEvidenceAnchorRecord>().SingleAsync(row => row.Id == evidenceAnchorId);
            Assert.Equal(sourceItemId, anchor.SourceItemId);

            var operation = await targetContext.Set<CognitiveMemoryExternalSourceIngestionRecord>().SingleAsync(row => row.Id == operationId);
            Assert.Equal(evidenceAnchorId, operation.EvidenceAnchorId);
            Assert.Equal(CognitiveMemoryExternalSourceIngestionStatus.Succeeded, operation.Status);
        }
    }

    [Fact]
    public async Task Project_package_export_import_round_trips_project_records_and_media()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-project-package");
        var sourceTestProfile = testEnvironment.CreatePostgreSqlProfile("project-package-source");
        Guid sourceProfileId;
        await using (var setupProvider = DatabaseProfileControlPlaneIntegrationHost.BuildServiceProvider(testEnvironment))
        {
            var setupProfileService = setupProvider.GetRequiredService<IDatabaseProfileService>();
            var sourceSaveResult = await setupProfileService.SaveAsync(TestDatabaseProfileEditorFactory.CreatePostgreSqlEditor(
                sourceTestProfile,
                "PostgreSQL project package source"));
            Assert.True(sourceSaveResult.IsSuccess, DescribeErrors(sourceSaveResult.Errors));
            Assert.True((await setupProfileService.ActivateAsync(sourceSaveResult.Value)).IsSuccess);
            sourceProfileId = sourceSaveResult.Value;
        }

        await using var provider = DatabaseProfileControlPlaneIntegrationHost.BuildServiceProvider(testEnvironment);

        var runtimeAccessor = provider.GetRequiredService<IDatabaseProfileRuntimeAccessor>();
        var profileService = provider.GetRequiredService<IDatabaseProfileService>();
        var bootstrapper = provider.GetRequiredService<IAppDatabaseBootstrapper>();
        var profileFactory = provider.GetRequiredService<IProfileAppDbContextFactory>();

        var sourceProfile = runtimeAccessor.ResolveCurrentProfile();
        Assert.Equal(sourceProfileId, sourceProfile.Profile.Id);
        await bootstrapper.EnsureCurrentProfileReadyAsync();

        var sourceProjectName = $"Packaged Project {Guid.NewGuid():N}"[..29];
        var sourceNodeTitle = "Packaged note";
        Guid sourceProjectId;
        string sourceNodeKey;

        await using (var sourceScope = provider.CreateAsyncScope())
        {
            var saveResult = await sourceScope.ServiceProvider.GetRequiredService<ProjectsService>().SaveAsync(new ProjectEditorModel
            {
                Name = sourceProjectName,
                Description = "Package source description",
                Objective = "Package source objective",
                CurrentPhase = "Build",
                Phases =
                [
                    new ProjectPhaseEditorModel
                    {
                        Name = "Build",
                        Goal = "Create package",
                        Status = ProjectPhaseStatus.Active
                    }
                ]
            });
            Assert.True(saveResult.IsSuccess, DescribeErrors(saveResult.Errors));
            sourceProjectId = saveResult.Value;

            await sourceScope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>().SeedProjectObjectsAsync(
                sourceProjectId,
                [
                    new ProjectObjectSeedRequest(
                        ProjectObjectType.Note,
                        sourceNodeTitle,
                        "Package subtitle",
                        "Package notes")
                ]);
        }

        var mediaRelativePath = $"managed-files/project-packages/{sourceProjectId:N}/alpha.txt";
        var mediaContent = "project package media";
        await using (var sourceContext = await profileFactory.CreateDbContextForProfileAsync(sourceProfile))
        {
            var seededNode = await sourceContext.Set<ProjectObjectRecord>()
                .SingleAsync(item => item.ProjectId == sourceProjectId && item.Title == sourceNodeTitle);
            sourceNodeKey = seededNode.NodeKey;

            var binding = await sourceContext.Set<ProjectNodeBindingRecord>()
                .SingleAsync(item => item.ProjectObjectId == seededNode.Id);
            binding.MediaRelativePath = mediaRelativePath;
            binding.MediaContentType = "text/plain";
            binding.MediaOriginalFileName = "alpha.txt";
            await sourceContext.SaveChangesAsync();
        }

        var sourceMediaPath = Path.Combine(
            sourceProfile.Profile.Storage.WorkspaceRoot,
            mediaRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(sourceMediaPath)!);
        await File.WriteAllTextAsync(sourceMediaPath, mediaContent);

        var targetTestProfile = testEnvironment.CreatePostgreSqlProfile("project-package-target");
        var targetSaveResult = await profileService.SaveAsync(TestDatabaseProfileEditorFactory.CreatePostgreSqlEditor(
            targetTestProfile,
            "PostgreSQL project package target"));
        Assert.True(targetSaveResult.IsSuccess, DescribeErrors(targetSaveResult.Errors));

        var targetProfile = runtimeAccessor.ResolveProfile(targetSaveResult.Value);
        await bootstrapper.EnsureProfileReadyAsync(targetProfile);

        ProjectPackageExportResult exportResult;
        ProjectPackageImportResult importResult;
        await using (var packageScope = provider.CreateAsyncScope())
        {
            var packageService = packageScope.ServiceProvider.GetRequiredService<IProjectPackageService>();
            var export = await packageService.ExportAllAsync(new ProjectPackageExportRequest
            {
                SourceProfileId = sourceProfile.Profile.Id
            });
            Assert.True(export.IsSuccess, DescribeErrors(export.Errors));
            exportResult = export.Value!;

            Assert.True(File.Exists(exportResult.PackagePath));
            Assert.Equal(ProjectPackageManifest.CurrentFormat, exportResult.Manifest.Format);
            Assert.Equal(1, Assert.Single(exportResult.Manifest.Tables, item => item.Name == "Projects_Projects").RowCount);
            Assert.Equal(1, Assert.Single(exportResult.Manifest.Tables, item => item.Name == "Workbench_ProjectNodeBindings").RowCount);
            Assert.Single(exportResult.Manifest.StorageFiles, item => item.RelativePath == mediaRelativePath);

            using (var archive = ZipFile.OpenRead(exportResult.PackagePath))
            {
                Assert.Contains(archive.Entries, entry => entry.FullName == "manifest.json");
                Assert.Contains(archive.Entries, entry => entry.FullName == "tables/projects.json");
                Assert.Contains(archive.Entries, entry => entry.FullName == $"storage/{mediaRelativePath}");
            }

            var manifestResult = await packageService.ReadManifestAsync(exportResult.PackagePath);
            Assert.True(manifestResult.IsSuccess, DescribeErrors(manifestResult.Errors));
            Assert.Equal(exportResult.Manifest.PackageId, manifestResult.Value!.PackageId);

            var import = await packageService.ImportAllAsync(new ProjectPackageImportRequest
            {
                PackagePath = exportResult.PackagePath,
                TargetProfileId = targetProfile.Profile.Id
            });
            Assert.True(import.IsSuccess, DescribeErrors(import.Errors));
            importResult = import.Value!;
        }

        Assert.Equal(exportResult.Manifest.PackageId, importResult.Manifest.PackageId);
        Assert.Equal(exportResult.Manifest.TotalRecordCount, importResult.RecordsImported);
        Assert.Equal(1, importResult.StorageFilesImported);

        await using (var targetContext = await profileFactory.CreateDbContextForProfileAsync(targetProfile))
        {
            var transferredProject = await targetContext.Set<Project>()
                .SingleAsync(item => item.Id == sourceProjectId);
            Assert.Equal(sourceProjectName, transferredProject.Name);
            Assert.True(await targetContext.Set<ProjectPhase>().AnyAsync(item => item.ProjectId == sourceProjectId && item.Name == "Build"));

            var transferredNode = await targetContext.Set<ProjectObjectRecord>()
                .SingleAsync(item => item.ProjectId == sourceProjectId && item.NodeKey == sourceNodeKey);
            var transferredBinding = await targetContext.Set<ProjectNodeBindingRecord>()
                .SingleAsync(item => item.ProjectObjectId == transferredNode.Id);
            Assert.Equal(mediaRelativePath, transferredBinding.MediaRelativePath);
        }

        var targetMediaPath = Path.Combine(
            targetProfile.Profile.Storage.WorkspaceRoot,
            mediaRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(targetMediaPath));
        Assert.Equal(mediaContent, await File.ReadAllTextAsync(targetMediaPath));
    }

    private static string DescribeErrors(IReadOnlyList<CanDoItAll.SharedKernel.Error> errors)
    {
        return string.Join(" ", errors.Select(error => error.Message));
    }

    private static string DescribeTransferResults(DatabaseTransferResult result)
    {
        return string.Join(" ", result.Items.Select(item => $"{item.Label}: {item.Message}"));
    }
}
