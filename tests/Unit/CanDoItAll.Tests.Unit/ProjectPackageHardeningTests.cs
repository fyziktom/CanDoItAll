using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Search;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.SchedulerPlanner;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectPackageHardeningTests
{
    [Fact]
    public void ValidateForImport_accepts_canonical_virtual_project_root_parent()
    {
        var projectId = Guid.NewGuid();
        var dataSet = CreateDataSet(new ProjectObjectRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            NodeKey = "custom:child",
            ParentNodeKey = ProjectWorkbenchGraphConventions.BuildProjectRootNodeKey(projectId),
            Title = "Child"
        });

        dataSet.ValidateForImport();
    }

    [Fact]
    public void ValidateForImport_rejects_mismatched_virtual_project_root_parent()
    {
        var projectId = Guid.NewGuid();
        var dataSet = CreateDataSet(new ProjectObjectRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            NodeKey = "custom:child",
            ParentNodeKey = ProjectWorkbenchGraphConventions.BuildProjectRootNodeKey(Guid.NewGuid()),
            Title = "Child"
        });

        var exception = Assert.Throws<InvalidDataException>(dataSet.ValidateForImport);

        Assert.Contains("project object parent", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateForImport_rejects_unknown_virtual_project_root_parent()
    {
        var projectId = Guid.NewGuid();
        var dataSet = CreateDataSet(new ProjectObjectRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            NodeKey = "custom:child",
            ParentNodeKey = "project:not-a-project-id",
            Title = "Child"
        });

        var exception = Assert.Throws<InvalidDataException>(dataSet.ValidateForImport);

        Assert.Contains("project object parent", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateForImport_rejects_case_ambiguous_node_keys()
    {
        var projectId = Guid.NewGuid();
        var dataSet = CreateDataSet(
            new ProjectObjectRecord
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                NodeKey = "custom:Asset",
                Title = "A"
            },
            new ProjectObjectRecord
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                NodeKey = "custom:asset",
                Title = "B"
            });

        var exception = Assert.Throws<InvalidDataException>(dataSet.ValidateForImport);

        Assert.Contains("duplicate node key", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateForImport_rejects_project_hierarchy_cycles()
    {
        var first = CreateProject("first");
        var second = CreateProject("second");
        var dataSet = new ProjectTransferDataSet
        {
            Projects = [first, second],
            HierarchyLinks =
            [
                new ProjectHierarchyLink
                {
                    Id = Guid.NewGuid(),
                    ParentProjectId = first.Id,
                    ChildProjectId = second.Id
                },
                new ProjectHierarchyLink
                {
                    Id = Guid.NewGuid(),
                    ParentProjectId = second.Id,
                    ChildProjectId = first.Id
                }
            ]
        };

        var exception = Assert.Throws<InvalidDataException>(dataSet.ValidateForImport);

        Assert.Contains("cycle", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateForImport_rejects_node_parent_cycles()
    {
        var projectId = Guid.NewGuid();
        var dataSet = CreateDataSet(
            new ProjectObjectRecord
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                NodeKey = "node:a",
                ParentNodeKey = "node:b",
                Title = "A"
            },
            new ProjectObjectRecord
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                NodeKey = "node:b",
                ParentNodeKey = "NODE:A",
                Title = "B"
            });

        var exception = Assert.Throws<InvalidDataException>(dataSet.ValidateForImport);

        Assert.Contains("cycle", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateForImport_handles_deep_acyclic_node_parent_chains_iteratively()
    {
        const int nodeCount = 10_000;
        var projectId = Guid.NewGuid();
        var objects = Enumerable.Range(0, nodeCount)
            .Select(index => new ProjectObjectRecord
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                NodeKey = $"node:{index:D5}",
                ParentNodeKey = index == 0 ? null : $"node:{index - 1:D5}",
                Title = $"Node {index}"
            })
            .ToArray();
        var dataSet = CreateDataSet(objects);

        dataSet.ValidateForImport();
    }

    [Fact]
    public void ValidateForImport_rejects_duplicate_project_hierarchy_edges()
    {
        var first = CreateProject("first");
        var second = CreateProject("second");
        var dataSet = new ProjectTransferDataSet
        {
            Projects = [first, second],
            HierarchyLinks =
            [
                new ProjectHierarchyLink
                {
                    Id = Guid.NewGuid(),
                    ParentProjectId = first.Id,
                    ChildProjectId = second.Id
                },
                new ProjectHierarchyLink
                {
                    Id = Guid.NewGuid(),
                    ParentProjectId = first.Id,
                    ChildProjectId = second.Id
                }
            ]
        };

        Assert.Throws<InvalidDataException>(dataSet.ValidateForImport);
    }

    [Fact]
    public void Package_export_rejects_nonterminal_cross_module_mutations()
    {
        var project = CreateProject("project");
        var dataSet = new ProjectTransferDataSet
        {
            Projects = [project],
            CrossModuleMutations =
            [
                new ProjectCrossModuleMutationRecord
                {
                    Id = Guid.NewGuid(),
                    ProjectId = project.Id,
                    ScopeNodeKey = "node:a",
                    Status = ProjectCrossModuleMutationStatus.Failed
                }
            ]
        };

        var exception = Assert.Throws<InvalidDataException>(
            dataSet.PrepareForPackageExport);

        Assert.Contains("recovery work", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Package_import_rejects_even_terminal_cross_module_mutations()
    {
        var project = CreateProject("project");
        var dataSet = new ProjectTransferDataSet
        {
            Projects = [project],
            CrossModuleMutations =
            [
                new ProjectCrossModuleMutationRecord
                {
                    Id = Guid.NewGuid(),
                    ProjectId = project.Id,
                    ScopeNodeKey = "node:a",
                    Status = ProjectCrossModuleMutationStatus.Completed
                }
            ]
        };

        var exception = Assert.Throws<InvalidDataException>(
            dataSet.ValidatePackageImportSafety);

        Assert.Contains("cannot import", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Archive_extraction_rejects_case_colliding_entries()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var packagePath = Path.Combine(root, "duplicate.zip");
            using (var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create))
            {
                archive.CreateEntry("manifest.json");
                archive.CreateEntry("MANIFEST.json");
            }

            var exception = await Assert.ThrowsAsync<InvalidDataException>(
                () => ProjectPackageArchive.ExtractPackageAsync(
                    packagePath,
                    TestWorkspaceServices.PhysicalPathPolicyFactory,
                    CancellationToken.None));

            Assert.Contains("duplicate", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Archive_extraction_rejects_parent_traversal()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var packagePath = Path.Combine(root, "traversal.zip");
            using (var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create))
            {
                archive.CreateEntry("../escape.json");
            }

            await Assert.ThrowsAsync<InvalidDataException>(
                () => ProjectPackageArchive.ExtractPackageAsync(
                    packagePath,
                    TestWorkspaceServices.PhysicalPathPolicyFactory,
                    CancellationToken.None));
            Assert.False(File.Exists(Path.Combine(root, "escape.json")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Archive_creation_rejects_more_than_the_entry_limit()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var stagingRoot = Path.Combine(root, "staging");
            Directory.CreateDirectory(stagingRoot);
            for (var index = 0; index <= ProjectPackageArchive.MaximumArchiveEntries; index++)
            {
                await File.WriteAllBytesAsync(
                    Path.Combine(stagingRoot, $"{index:D5}.json"),
                    []);
            }

            var packagePath = Path.Combine(root, "too-many.zip");
            var exception = await Assert.ThrowsAsync<InvalidDataException>(
                () => ProjectPackageArchive.CreatePackageArchiveAsync(
                    stagingRoot,
                    packagePath,
                    DateTimeOffset.UtcNow,
                    TestWorkspaceServices.PhysicalPathPolicyFactory,
                    CancellationToken.None));

            Assert.Contains("too many", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.False(File.Exists(packagePath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Archive_creation_never_overwrites_an_existing_output()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var stagingRoot = Path.Combine(root, "staging");
            Directory.CreateDirectory(stagingRoot);
            await File.WriteAllTextAsync(Path.Combine(stagingRoot, "manifest.json"), "new");
            var packagePath = Path.Combine(root, "existing.zip");
            var original = new byte[] { 1, 2, 3, 4 };
            await File.WriteAllBytesAsync(packagePath, original);

            await Assert.ThrowsAnyAsync<IOException>(
                () => ProjectPackageArchive.CreatePackageArchiveAsync(
                    stagingRoot,
                    packagePath,
                    DateTimeOffset.UtcNow,
                    TestWorkspaceServices.PhysicalPathPolicyFactory,
                    CancellationToken.None));

            Assert.Equal(original, await File.ReadAllBytesAsync(packagePath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Manifest_validation_rejects_v1_with_an_actionable_format_message()
    {
        var manifest = new ProjectPackageManifest
        {
            PackageId = Guid.NewGuid(),
            SourceProfileId = Guid.NewGuid(),
            Format = "candoitall.projects.v1"
        };

        var exception = Assert.Throws<InvalidDataException>(
            () => ProjectPackageService.ValidateManifest(manifest));

        Assert.Contains(ProjectPackageManifest.CurrentFormat, exception.Message, StringComparison.Ordinal);
        Assert.Contains("integrity", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Integrity_verification_rejects_hash_and_length_tampering()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var path = Path.Combine(root, "payload.bin");
            var original = "expected"u8.ToArray();
            await File.WriteAllBytesAsync(path, "tampered"u8.ToArray());
            var sha256 = Convert.ToHexStringLower(SHA256.HashData(original));

            await Assert.ThrowsAsync<InvalidDataException>(
                () => ProjectPackageArchive.VerifyFileIntegrityAsync(
                    path,
                    original.LongLength,
                    sha256,
                    1024,
                    CancellationToken.None));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Archive_extraction_rejects_symbolic_link_entries()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var packagePath = Path.Combine(root, "symlink.zip");
            using (var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry("link");
                entry.ExternalAttributes = 0xA000 << 16;
            }

            var exception = await Assert.ThrowsAsync<InvalidDataException>(
                () => ProjectPackageArchive.ExtractPackageAsync(
                    packagePath,
                    TestWorkspaceServices.PhysicalPathPolicyFactory,
                    CancellationToken.None));

            Assert.Contains("symbolic-link", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Archive_creation_rejects_oversized_sparse_entries()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var stagingRoot = Path.Combine(root, "staging");
            Directory.CreateDirectory(stagingRoot);
            var oversizedPath = Path.Combine(stagingRoot, "oversized.bin");
            await using (var stream = new FileStream(
                             oversizedPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None))
            {
                stream.SetLength(ProjectPackageArchive.MaximumArchiveEntryBytes + 1);
            }

            var exception = await Assert.ThrowsAsync<InvalidDataException>(
                () => ProjectPackageArchive.CreatePackageArchiveAsync(
                    stagingRoot,
                    Path.Combine(root, "oversized.zip"),
                    DateTimeOffset.UtcNow,
                    TestWorkspaceServices.PhysicalPathPolicyFactory,
                    CancellationToken.None));

            Assert.Contains("entry size", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Failed_placement_verification_is_registered_for_compensation()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var expectedContent = "expected"u8.ToArray();
            var driver = new TestStorageDriver(
                StorageProviderKind.FileSystem,
                StorageCapability.Read | StorageCapability.Write | StorageCapability.Delete,
                "different"u8.ToArray());
            var importer = CreateImporter(root, driver);
            var targetStorage = CreateStorage(
                StorageProviderKind.FileSystem,
                root,
                StorageCapability.Read |
                StorageCapability.Write |
                StorageCapability.Delete |
                StorageCapability.InlinePreview);
            var sourceReference = new StorageObjectReference(
                Guid.NewGuid(),
                StorageProviderKind.FileSystem,
                StorageLocatorKind.RelativePath,
                "managed-files/project-media/files/source.txt",
                "source.txt",
                "text/plain",
                expectedContent.LongLength);
            var projectId = Guid.NewGuid();
            var projectObject = new ProjectObjectRecord
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                NodeKey = "node:file",
                Title = "File"
            };
            var binding = new ProjectNodeBindingRecord
            {
                Id = Guid.NewGuid(),
                ProjectObjectId = projectObject.Id,
                MediaRelativePath = sourceReference.Locator,
                MediaContentType = "text/plain",
                MediaOriginalFileName = "source.txt",
                StorageObjectReferenceJson = StorageJson.SerializeReference(sourceReference)
            };
            var packagePath = Path.Combine(root, "payload.bin");
            await File.WriteAllBytesAsync(packagePath, expectedContent);
            var manifest = new ProjectPackageManifest
            {
                PackageId = Guid.NewGuid(),
                StorageFiles =
                [
                    new ProjectPackageStorageFileManifest
                    {
                        SourceStorageId = sourceReference.StorageId,
                        ProviderKind = sourceReference.ProviderKind,
                        LocatorKind = sourceReference.LocatorKind,
                        Locator = sourceReference.Locator,
                        RelativePath = sourceReference.Locator,
                        PackagePath = "payload.bin",
                        ContentType = "text/plain",
                        OriginalFileName = "source.txt",
                        Length = expectedContent.LongLength,
                        Sha256 = Convert.ToHexStringLower(SHA256.HashData(expectedContent))
                    }
                ]
            };
            var dataSet = new ProjectTransferDataSet
            {
                Projects = [CreateProjectWithId(projectId)],
                Objects = [projectObject],
                NodeBindings = [binding]
            };
            var preflight = new ProjectPackageStorageImportPreflight(
            [
                new PackageBinding(
                    binding,
                    sourceReference,
                    ProjectManagedStorageObjectKey.FromReference(sourceReference))
            ]);
            var storagePlan = new TargetStoragePlan(
                [targetStorage],
                [targetStorage],
                [],
                null,
                null,
                string.Empty);
            var stagedWrites = importer.CreateStagingJournal();

            await Assert.ThrowsAsync<InvalidDataException>(
                () => importer.RewriteStorageBindingsAsync(
                    root,
                    manifest,
                    dataSet,
                    preflight,
                    storagePlan,
                    stagedWrites,
                    CancellationToken.None));

            Assert.Single(stagedWrites);
            await importer.CleanupStagedWritesAsync(stagedWrites);
            Assert.Equal(1, driver.DeleteCount);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Target_storage_without_delete_cannot_stage_mutable_package_bytes()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var driver = new TestStorageDriver(
                StorageProviderKind.FileSystem,
                StorageCapability.Read | StorageCapability.Write,
                []);
            var importer = CreateImporter(root, driver);
            var storage = CreateStorage(
                StorageProviderKind.FileSystem,
                root,
                StorageCapability.Read | StorageCapability.Write);
            var options = AppDbContextTestOptionsBuilder.Create()
                .UseInMemoryDatabase($"package-no-delete-{Guid.NewGuid():N}")
                .Options;
            await using var dbContext = new AppDbContext(options);
            await dbContext.Database.EnsureCreatedAsync();
            dbContext.Set<StorageCatalogRecord>().Add(storage);
            await dbContext.SaveChangesAsync();
            var profile = TestProfile(root);

            var exception = await Assert.ThrowsAsync<InvalidDataException>(
                () => importer.BuildTargetStoragePlanAsync(
                    dbContext,
                    profile,
                    CancellationToken.None));

            Assert.Contains("write and verify", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Immutable_import_fails_when_target_ipfs_resolves_different_bytes()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var expectedContent = "immutable expected"u8.ToArray();
            var driver = new TestStorageDriver(
                StorageProviderKind.Ipfs,
                StorageCapability.Read,
                "different network"u8.ToArray());
            var importer = CreateImporter(root, driver);
            var targetStorage = CreateStorage(
                StorageProviderKind.Ipfs,
                "http://127.0.0.1:5001",
                StorageCapability.Read);
            var sourceReference = new StorageObjectReference(
                Guid.NewGuid(),
                StorageProviderKind.Ipfs,
                StorageLocatorKind.ContentAddress,
                "bafy-package-test",
                "immutable.txt",
                "text/plain",
                expectedContent.LongLength);
            var projectId = Guid.NewGuid();
            var projectObject = new ProjectObjectRecord
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                NodeKey = "node:immutable",
                Title = "Immutable"
            };
            var binding = new ProjectNodeBindingRecord
            {
                Id = Guid.NewGuid(),
                ProjectObjectId = projectObject.Id,
                MediaContentType = "text/plain",
                MediaOriginalFileName = "immutable.txt",
                StorageObjectReferenceJson = StorageJson.SerializeReference(sourceReference)
            };
            var manifest = new ProjectPackageManifest
            {
                PackageId = Guid.NewGuid(),
                ImmutableStorageReferences =
                [
                    new ProjectPackageImmutableStorageReferenceManifest
                    {
                        SourceStorageId = sourceReference.StorageId,
                        ProviderKind = StorageProviderKind.Ipfs,
                        LocatorKind = StorageLocatorKind.ContentAddress,
                        Locator = sourceReference.Locator,
                        ContentType = "text/plain",
                        OriginalFileName = "immutable.txt",
                        Length = expectedContent.LongLength,
                        Sha256 = Convert.ToHexStringLower(SHA256.HashData(expectedContent))
                    }
                ]
            };
            var dataSet = new ProjectTransferDataSet
            {
                Projects = [CreateProjectWithId(projectId)],
                Objects = [projectObject],
                NodeBindings = [binding]
            };
            var preflight = new ProjectPackageStorageImportPreflight(
            [
                new PackageBinding(
                    binding,
                    sourceReference,
                    ProjectManagedStorageObjectKey.FromReference(sourceReference))
            ]);
            var storagePlan = new TargetStoragePlan(
                [targetStorage],
                [],
                [],
                null,
                null,
                string.Empty);

            var exception = await Assert.ThrowsAsync<InvalidDataException>(
                () => importer.RewriteStorageBindingsAsync(
                    root,
                    manifest,
                    dataSet,
                    preflight,
                    storagePlan,
                    importer.CreateStagingJournal(),
                    CancellationToken.None));

            Assert.Contains("different bytes", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Failed_compensation_reports_incomplete_cleanup_after_bounded_retries()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var driver = new TestStorageDriver(
                StorageProviderKind.FileSystem,
                StorageCapability.Read | StorageCapability.Write | StorageCapability.Delete,
                [],
                failDelete: true);
            var importer = CreateImporter(root, driver);
            var storage = CreateStorage(
                StorageProviderKind.FileSystem,
                root,
                StorageCapability.Read | StorageCapability.Write | StorageCapability.Delete);
            var reference = new StorageObjectReference(
                storage.Id,
                StorageProviderKind.FileSystem,
                StorageLocatorKind.RelativePath,
                "managed-files/project-media/imports/orphan.bin");

            var exception = await Assert.ThrowsAsync<ProjectPackageCompensationException>(
                () => importer.CleanupStagedWritesAsync(
                [
                    new StagedStorageWrite(storage, reference)
                ]));

            Assert.Equal(3, driver.DeleteCount);
            Assert.Contains(storage.Id.ToString("D"), exception.Message, StringComparison.Ordinal);
            Assert.Contains("cleanup is incomplete", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(reference.LocatorKind.ToString(), exception.Message, StringComparison.Ordinal);
            Assert.Contains(
                ProjectPackageCompensationFailure.CreateLocatorFingerprint(reference),
                exception.Message,
                StringComparison.Ordinal);
            Assert.DoesNotContain(reference.Locator, exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Failed_compensation_distinguishes_two_orphans_on_one_storage()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var driver = new TestStorageDriver(
                StorageProviderKind.FileSystem,
                StorageCapability.Read | StorageCapability.Write | StorageCapability.Delete,
                [],
                failDelete: true);
            var importer = CreateImporter(root, driver);
            var storage = CreateStorage(
                StorageProviderKind.FileSystem,
                root,
                StorageCapability.Read | StorageCapability.Write | StorageCapability.Delete);
            var firstReference = new StorageObjectReference(
                storage.Id,
                StorageProviderKind.FileSystem,
                StorageLocatorKind.RelativePath,
                "managed-files/project-media/imports/first-orphan.bin");
            var secondReference = firstReference with
            {
                Locator = "managed-files/project-media/imports/second-orphan.bin"
            };

            var exception = await Assert.ThrowsAsync<ProjectPackageCompensationException>(
                () => importer.CleanupStagedWritesAsync(
                [
                    new StagedStorageWrite(storage, firstReference),
                    new StagedStorageWrite(storage, secondReference)
                ]));

            var firstFingerprint = ProjectPackageCompensationFailure
                .CreateLocatorFingerprint(firstReference);
            var secondFingerprint = ProjectPackageCompensationFailure
                .CreateLocatorFingerprint(secondReference);
            Assert.Equal(6, driver.DeleteCount);
            Assert.Equal(2, exception.Failures.Count);
            Assert.NotEqual(firstFingerprint, secondFingerprint);
            Assert.Contains(firstFingerprint, exception.Message, StringComparison.Ordinal);
            Assert.Contains(secondFingerprint, exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(firstReference.Locator, exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(secondReference.Locator, exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Target_state_guard_detects_project_party_assignment_residue()
    {
        await using var dbContext = CreateTargetStateContext();
        dbContext.Set<ProjectPartyAssignment>().Add(new ProjectPartyAssignment
        {
            ProjectId = Guid.NewGuid(),
            PartyId = Guid.NewGuid(),
            Source = "test"
        });
        await dbContext.SaveChangesAsync();

        var participant = new CrmHrProjectTransferTargetStateParticipant();
        var residues = await participant.FindResiduesAsync(
            dbContext,
            CancellationToken.None);

        Assert.Contains(
            residues,
            residue => residue.Description == "CRM project party assignments");
    }

    [Fact]
    public async Task Target_state_guard_detects_project_search_document_residue()
    {
        await using var dbContext = CreateTargetStateContext();
        dbContext.Set<SearchDocument>().Add(new SearchDocument
        {
            SourceType = SearchDocument.ProjectSourceType,
            SourceKey = Guid.NewGuid().ToString("D"),
            Category = "project",
            Title = "Stale project search entry",
            Route = "/projects/stale",
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var participant = new InfrastructureProjectTransferTargetStateParticipant();
        var residues = await participant.FindResiduesAsync(
            dbContext,
            CancellationToken.None);

        Assert.Contains(
            residues,
            residue => residue.Description == "project search documents");
    }

    [Theory]
    [InlineData(StorageRoutingScopeKind.Project)]
    [InlineData(StorageRoutingScopeKind.Node)]
    public async Task Target_state_guard_detects_typed_project_storage_routing_scope(
        StorageRoutingScopeKind scopeKind)
    {
        await using var dbContext = CreateTargetStateContext();
        dbContext.Set<StorageRoutingRule>().Add(new StorageRoutingRule
        {
            Name = "Stale project storage route",
            ScopeKind = scopeKind,
            ProjectId = null,
            PreferredStorageId = Guid.NewGuid()
        });
        await dbContext.SaveChangesAsync();

        var participant = new InfrastructureProjectTransferTargetStateParticipant();
        var residues = await participant.FindResiduesAsync(
            dbContext,
            CancellationToken.None);

        Assert.Contains(
            residues,
            residue => residue.Description == "project storage routing rules");
    }

    [Fact]
    public async Task Target_state_guard_ignores_unattributed_workspace_storage_route()
    {
        await using var dbContext = CreateTargetStateContext();
        dbContext.Set<StorageRoutingRule>().Add(new StorageRoutingRule
        {
            Name = "Workspace storage route",
            ScopeKind = StorageRoutingScopeKind.Workspace,
            ProjectId = null,
            PreferredStorageId = Guid.NewGuid()
        });
        await dbContext.SaveChangesAsync();
        var participant = new InfrastructureProjectTransferTargetStateParticipant();

        var residues = await participant.FindResiduesAsync(
            dbContext,
            CancellationToken.None);

        Assert.DoesNotContain(
            residues,
            residue => residue.Description == "project storage routing rules");
    }

    [Fact]
    public async Task Exclusive_import_locks_cover_project_related_state_tables()
    {
        await using var dbContext = CreateTargetStateContext();
        var guard = CreateTargetStateGuard(
            new InfrastructureProjectTransferTargetStateParticipant(),
            new CrmHrProjectTransferTargetStateParticipant());
        var tableNames = guard.ResolveExclusiveImportTableNames(dbContext);

        Assert.Contains(
            "\"CrmHr_ProjectPartyAssignments\"",
            tableNames);
        Assert.Contains(
            "\"Infrastructure_SearchDocuments\"",
            tableNames);
        Assert.Contains(
            "\"Storage_RoutingRules\"",
            tableNames);
    }

    [Fact]
    public async Task Exclusive_import_lock_fails_fast_for_non_postgresql_provider()
    {
        await using var dbContext = CreateTargetStateContext();
        var guard = CreateTargetStateGuard();

        var exception = await Assert.ThrowsAsync<NotSupportedException>(
            () => guard.AcquireExclusiveImportLocksAsync(
                dbContext,
                CancellationToken.None));

        Assert.Contains("require PostgreSQL", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Target_state_guard_detects_project_origin_workflow_run_without_project_projection()
    {
        await using var dbContext = CreateTargetStateContext();
        dbContext.Set<WorkflowRunRecordEntity>().Add(new WorkflowRunRecordEntity
        {
            RunId = Guid.NewGuid(),
            WorkflowId = Guid.NewGuid(),
            VersionId = Guid.NewGuid(),
            OriginKind = WorkflowLaunchOriginKind.ProjectStructureNode,
            OriginProjectId = null,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();
        var participant = new AgentFrameworkProjectTransferTargetStateParticipant();

        var residues = await participant.FindResiduesAsync(
            dbContext,
            CancellationToken.None);

        Assert.Contains(
            residues,
            residue => residue.Description ==
                "agent workflow runs linked to projects");
    }

    [Fact]
    public async Task Target_state_guard_ignores_unattributed_api_workflow_run()
    {
        await using var dbContext = CreateTargetStateContext();
        dbContext.Set<WorkflowRunRecordEntity>().Add(new WorkflowRunRecordEntity
        {
            RunId = Guid.NewGuid(),
            WorkflowId = Guid.NewGuid(),
            VersionId = Guid.NewGuid(),
            OriginKind = WorkflowLaunchOriginKind.Api,
            OriginProjectId = null,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();
        var participant = new AgentFrameworkProjectTransferTargetStateParticipant();

        var residues = await participant.FindResiduesAsync(
            dbContext,
            CancellationToken.None);

        Assert.DoesNotContain(
            residues,
            residue => residue.Description ==
                "agent workflow runs linked to projects");
    }

    [Fact]
    public async Task Target_state_guard_detects_and_locks_project_workflow_launch_claims()
    {
        await using var dbContext = CreateTargetStateContext();
        dbContext.Set<WorkflowLaunchIdempotencyRecordEntity>().Add(
            new WorkflowLaunchIdempotencyRecordEntity
            {
                Id = Guid.NewGuid(),
                WorkflowId = Guid.NewGuid(),
                OriginKind = WorkflowLaunchOriginKind.ProjectStructureNode,
                ClaimToken = Guid.NewGuid(),
                ReservedRunId = Guid.NewGuid(),
                ClaimedAtUtc = DateTimeOffset.UtcNow,
                LeaseExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(1)
            });
        await dbContext.SaveChangesAsync();
        var participant = new AgentFrameworkProjectTransferTargetStateParticipant();

        var residues = await participant.FindResiduesAsync(
            dbContext,
            CancellationToken.None);
        var guard = CreateTargetStateGuard(participant);
        var tableNames = guard.ResolveExclusiveImportTableNames(dbContext);

        Assert.Contains(
            residues,
            residue => residue.Description ==
                "project structure workflow launch claims");
        Assert.Contains(
            "\"AgentFramework_WorkflowLaunchIdempotency\"",
            tableNames);
    }

    [Fact]
    public async Task Target_state_guard_detects_and_locks_project_workflow_usage()
    {
        await using var dbContext = CreateTargetStateContext();
        dbContext.Set<WorkflowUsageObservationRecordEntity>().Add(
            new WorkflowUsageObservationRecordEntity
            {
                Id = Guid.NewGuid(),
                RunId = Guid.NewGuid(),
                WorkflowId = Guid.NewGuid(),
                VersionId = Guid.NewGuid(),
                NodeId = "node",
                InvocationId = Guid.NewGuid(),
                OriginKind = WorkflowLaunchOriginKind.ProjectStructureNode,
                RecordedAtUtc = DateTimeOffset.UtcNow
            });
        await dbContext.SaveChangesAsync();
        var participant = new AgentFrameworkProjectTransferTargetStateParticipant();

        var residues = await participant.FindResiduesAsync(
            dbContext,
            CancellationToken.None);
        var guard = CreateTargetStateGuard(participant);
        var tableNames = guard.ResolveExclusiveImportTableNames(dbContext);

        Assert.Contains(
            residues,
            residue => residue.Description ==
                "project structure workflow usage observations");
        Assert.Contains(
            "\"AgentFramework_WorkflowUsageObservations\"",
            tableNames);
    }

    [Fact]
    public async Task Target_state_guard_ignores_nonproject_workflow_claim_and_usage()
    {
        await using var dbContext = CreateTargetStateContext();
        dbContext.Set<WorkflowLaunchIdempotencyRecordEntity>().Add(
            new WorkflowLaunchIdempotencyRecordEntity
            {
                Id = Guid.NewGuid(),
                WorkflowId = Guid.NewGuid(),
                OriginKind = WorkflowLaunchOriginKind.Api,
                ClaimToken = Guid.NewGuid(),
                ReservedRunId = Guid.NewGuid(),
                ClaimedAtUtc = DateTimeOffset.UtcNow,
                LeaseExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(1)
            });
        dbContext.Set<WorkflowUsageObservationRecordEntity>().Add(
            new WorkflowUsageObservationRecordEntity
            {
                Id = Guid.NewGuid(),
                RunId = Guid.NewGuid(),
                WorkflowId = Guid.NewGuid(),
                VersionId = Guid.NewGuid(),
                NodeId = "node",
                InvocationId = Guid.NewGuid(),
                OriginKind = WorkflowLaunchOriginKind.Api,
                RecordedAtUtc = DateTimeOffset.UtcNow
            });
        await dbContext.SaveChangesAsync();
        var participant = new AgentFrameworkProjectTransferTargetStateParticipant();

        var residues = await participant.FindResiduesAsync(
            dbContext,
            CancellationToken.None);

        Assert.DoesNotContain(
            residues,
            residue => residue.Description.Contains(
                "project structure workflow",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task Target_state_guard_detects_and_locks_project_structure_leases()
    {
        await using var dbContext = CreateTargetStateContext();
        dbContext.Set<ProjectStructureLeaseRecord>().Add(new ProjectStructureLeaseRecord
        {
            ScopeKind = ProjectStructureLeaseScopeKind.Project,
            ScopeKey = Guid.NewGuid().ToString("D"),
            LeaseToken = Guid.NewGuid().ToString("N"),
            AgentId = "test-agent",
            AgentName = "Test agent",
            MachineName = "test-machine",
            RepositoryRoot = "C:\\test",
            BranchName = "test",
            Reason = "target guard test",
            AcquiredAtUtc = DateTimeOffset.UtcNow,
            RenewedAtUtc = DateTimeOffset.UtcNow,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(5)
        });
        await dbContext.SaveChangesAsync();
        var participant = new WorkbenchProjectTransferTargetStateParticipant();

        var residues = await participant.FindResiduesAsync(
            dbContext,
            CancellationToken.None);
        var guard = CreateTargetStateGuard(participant);
        var tableNames = guard.ResolveExclusiveImportTableNames(dbContext);

        Assert.Contains(
            residues,
            residue => residue.Description == "project structure leases");
        Assert.Contains("\"Workbench_ProjectStructureLeases\"", tableNames);
    }

    [Fact]
    public async Task Target_state_guard_ignores_repo_branch_leases_but_still_locks_table()
    {
        await using var dbContext = CreateTargetStateContext();
        dbContext.Set<ProjectStructureLeaseRecord>().Add(new ProjectStructureLeaseRecord
        {
            ScopeKind = ProjectStructureLeaseScopeKind.RepoBranch,
            ScopeKey = "C:\\test::main",
            LeaseToken = Guid.NewGuid().ToString("N"),
            AgentId = "test-agent",
            AgentName = "Test agent",
            MachineName = "test-machine",
            RepositoryRoot = "C:\\test",
            BranchName = "main",
            Reason = "target guard negative test",
            AcquiredAtUtc = DateTimeOffset.UtcNow,
            RenewedAtUtc = DateTimeOffset.UtcNow,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(5)
        });
        await dbContext.SaveChangesAsync();
        var participant = new WorkbenchProjectTransferTargetStateParticipant();

        var residues = await participant.FindResiduesAsync(
            dbContext,
            CancellationToken.None);
        var guard = CreateTargetStateGuard(participant);
        var tableNames = guard.ResolveExclusiveImportTableNames(dbContext);

        Assert.DoesNotContain(
            residues,
            residue => residue.Description == "project structure leases");
        Assert.Contains("\"Workbench_ProjectStructureLeases\"", tableNames);
    }

    [Fact]
    public async Task Target_state_guard_ignores_repo_branch_analytics_but_still_locks_table()
    {
        await using var dbContext = CreateTargetStateContext();
        dbContext.Set<ProjectStructureOperationAnalyticsRecord>().Add(
            new ProjectStructureOperationAnalyticsRecord
            {
                OperationName = "inspect_repo_branch",
                ProjectId = null,
                ScopeKind = ProjectStructureLeaseScopeKind.RepoBranch,
                ScopeKey = "C:\\test::main",
                AgentId = "test-agent",
                AgentName = "Test agent",
                MachineName = "test-machine",
                RepositoryRoot = "C:\\test",
                BranchName = "main",
                Succeeded = true,
                OccurredAtUtc = DateTimeOffset.UtcNow
            });
        await dbContext.SaveChangesAsync();
        var participant = new WorkbenchProjectTransferTargetStateParticipant();

        var residues = await participant.FindResiduesAsync(
            dbContext,
            CancellationToken.None);
        var guard = CreateTargetStateGuard(participant);
        var tableNames = guard.ResolveExclusiveImportTableNames(dbContext);

        Assert.DoesNotContain(
            residues,
            residue => residue.Description ==
                "project structure operation analytics");
        Assert.Contains(
            "\"Workbench_ProjectStructureOperationAnalytics\"",
            tableNames);
    }

    [Fact]
    public async Task Target_state_guard_fails_closed_and_locks_scheduler_state()
    {
        await using var dbContext = CreateTargetStateContext();
        var projectId = Guid.NewGuid();
        dbContext.Set<SchedulerPlan>().Add(new SchedulerPlan
        {
            Name = "Project automation",
            TargetKind = SchedulerPlanTargetKind.Workflow,
            TargetId = Guid.NewGuid(),
            TargetNameSnapshot = "Project workflow",
            CronExpression = "0 * * * * ?",
            CronDescription = "Every minute",
            InputJson = $"{{\"projectId\":\"{projectId:D}\"}}",
            SchedulerTriggerId = Guid.NewGuid(),
            SchedulerTriggerKey = Guid.NewGuid().ToString("N"),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();
        var participant = new SchedulerPlannerProjectTransferTargetStateParticipant();

        var residues = await participant.FindResiduesAsync(
            dbContext,
            CancellationToken.None);
        var guard = CreateTargetStateGuard(participant);
        var tableNames = guard.ResolveExclusiveImportTableNames(dbContext);

        Assert.Contains(
            residues,
            residue => residue.Description.Contains(
                "unclassifiable project input",
                StringComparison.Ordinal));
        Assert.Contains("\"SchedulerPlanner_Plans\"", tableNames);
        Assert.Contains("\"SchedulerPlanner_Runs\"", tableNames);
    }

    [Fact]
    public async Task Target_state_guard_detects_and_locks_process_assignment_project_state()
    {
        await using var dbContext = CreateTargetStateContext();
        dbContext.Set<ProcessRuntimeStepAssignmentEntity>().Add(
            CreateProcessAssignment(
                JsonSerializer.Serialize(new Dictionary<string, string>
                {
                    [ProcessRuntimeLaunchVariables.ProjectId] =
                        Guid.NewGuid().ToString("D")
                })));
        await dbContext.SaveChangesAsync();
        var participant = new ProcessesProjectTransferTargetStateParticipant();

        var residues = await participant.FindResiduesAsync(
            dbContext,
            CancellationToken.None);
        var guard = CreateTargetStateGuard(participant);
        var tableNames = guard.ResolveExclusiveImportTableNames(dbContext);

        Assert.Contains(
            residues,
            residue => residue.Description ==
                "process step assignments linked to projects");
        Assert.Contains("\"process_runtime_step_assignments\"", tableNames);
    }

    [Fact]
    public async Task Target_state_guard_detects_project_node_only_process_assignment()
    {
        await using var dbContext = CreateTargetStateContext();
        dbContext.Set<ProcessRuntimeStepAssignmentEntity>().Add(
            CreateProcessAssignment(
                JsonSerializer.Serialize(new Dictionary<string, string>
                {
                    [ProcessRuntimeLaunchVariables.ProjectNodeId] =
                        "custom:planning-note"
                })));
        await dbContext.SaveChangesAsync();
        var participant = new ProcessesProjectTransferTargetStateParticipant();

        var residues = await participant.FindResiduesAsync(
            dbContext,
            CancellationToken.None);

        Assert.Contains(
            residues,
            residue => residue.Description ==
                "process step assignments linked to projects");
    }

    [Theory]
    [InlineData("{\"ProjectId\":")]
    [InlineData("{\"ProjectId\":\"\"}")]
    [InlineData("{\"ProjectId\":\"not-a-guid\"}")]
    [InlineData("{\"ProjectNodeId\":\"\"}")]
    public async Task Target_state_guard_fails_closed_on_malformed_process_project_state(
        string launchVariablesJson)
    {
        await using var dbContext = CreateTargetStateContext();
        dbContext.Set<ProcessRuntimeStepAssignmentEntity>().Add(
            CreateProcessAssignment(launchVariablesJson));
        await dbContext.SaveChangesAsync();
        var participant = new ProcessesProjectTransferTargetStateParticipant();

        var residues = await participant.FindResiduesAsync(
            dbContext,
            CancellationToken.None);

        Assert.Contains(
            residues,
            residue => residue.Description.Contains(
                "malformed project launch state",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task Target_state_guard_ignores_project_token_in_process_value()
    {
        await using var dbContext = CreateTargetStateContext();
        dbContext.Set<ProcessRuntimeStepAssignmentEntity>().Add(
            CreateProcessAssignment(
                JsonSerializer.Serialize(new Dictionary<string, string>
                {
                    ["note"] =
                        $"{ProcessRuntimeLaunchVariables.ProjectId} " +
                        ProcessRuntimeLaunchVariables.ProjectNodeId
                })));
        await dbContext.SaveChangesAsync();
        var participant = new ProcessesProjectTransferTargetStateParticipant();

        var residues = await participant.FindResiduesAsync(
            dbContext,
            CancellationToken.None);

        Assert.DoesNotContain(
            residues,
            residue => residue.Description.Contains(
                "process step assignments",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task Target_state_guard_detects_nonterminal_process_state_and_locks_sequence_tables()
    {
        await using var dbContext = CreateTargetStateContext();
        var planId = Guid.NewGuid();
        dbContext.Set<ProcessInstancePlanEntity>().Add(new ProcessInstancePlanEntity
        {
            PlanId = planId,
            RootPlanId = planId,
            DefinitionId = Guid.NewGuid(),
            DefinitionVersionId = Guid.NewGuid(),
            PlanHash = "hash",
            PlanSchemaVersion = "v1",
            DefinitionContentHash = "definition-hash",
            PayloadJson = "{}",
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        dbContext.Set<ProcessRuntimeStateEntity>().Add(new ProcessRuntimeStateEntity
        {
            RunId = Guid.NewGuid(),
            RootRunId = Guid.NewGuid(),
            PlanId = planId,
            PlanHash = "hash",
            Status = ProcessRuntimeStatus.Active,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            ConcurrencyToken = Guid.NewGuid()
        });
        await dbContext.SaveChangesAsync();
        var participant = new ProcessesProjectTransferTargetStateParticipant();

        var residues = await participant.FindResiduesAsync(
            dbContext,
            CancellationToken.None);
        var guard = CreateTargetStateGuard(participant);
        var tableNames = guard.ResolveExclusiveImportTableNames(dbContext);

        Assert.Contains(
            residues,
            residue => residue.Description == "nonterminal process runtime state");
        Assert.Contains("\"process_instance_plans\"", tableNames);
        Assert.Contains("\"process_runtime_states\"", tableNames);
    }

    [Theory]
    [InlineData(ProcessRuntimeStatus.Completed)]
    [InlineData(ProcessRuntimeStatus.Failed)]
    [InlineData(ProcessRuntimeStatus.Cancelled)]
    public async Task Target_state_guard_allows_unattributed_terminal_process_history(
        ProcessRuntimeStatus status)
    {
        await using var dbContext = CreateTargetStateContext();
        var planId = Guid.NewGuid();
        dbContext.Set<ProcessInstancePlanEntity>().Add(new ProcessInstancePlanEntity
        {
            PlanId = planId,
            RootPlanId = planId,
            DefinitionId = Guid.NewGuid(),
            DefinitionVersionId = Guid.NewGuid(),
            PlanHash = "hash",
            PlanSchemaVersion = "v1",
            DefinitionContentHash = "definition-hash",
            PayloadJson = "{}",
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        dbContext.Set<ProcessRuntimeStateEntity>().Add(new ProcessRuntimeStateEntity
        {
            RunId = Guid.NewGuid(),
            RootRunId = Guid.NewGuid(),
            PlanId = planId,
            PlanHash = "hash",
            Status = status,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            ConcurrencyToken = Guid.NewGuid()
        });
        await dbContext.SaveChangesAsync();
        var participant = new ProcessesProjectTransferTargetStateParticipant();

        var residues = await participant.FindResiduesAsync(
            dbContext,
            CancellationToken.None);

        Assert.DoesNotContain(
            residues,
            residue => residue.Description == "nonterminal process runtime state");
    }

    [Fact]
    public void Target_state_guard_rejects_missing_participants()
    {
        var participants = Enum.GetValues<ProjectTransferTargetStateArea>()
            .Where(area => area != ProjectTransferTargetStateArea.Workspace)
            .Select(area => new EmptyTargetStateParticipant(area));

        var exception = Assert.Throws<InvalidOperationException>(
            () => new ProjectTransferTargetStateGuard(participants));

        Assert.Contains("Workspace", exception.Message, StringComparison.Ordinal);
        Assert.Contains("missing", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Target_state_guard_rejects_duplicate_participants()
    {
        var participants = Enum.GetValues<ProjectTransferTargetStateArea>()
            .Select(area =>
                (IProjectTransferTargetStateParticipant)new EmptyTargetStateParticipant(area))
            .Append(new EmptyTargetStateParticipant(
                ProjectTransferTargetStateArea.Infrastructure));

        var exception = Assert.Throws<InvalidOperationException>(
            () => new ProjectTransferTargetStateGuard(participants));

        Assert.Contains("Infrastructure", exception.Message, StringComparison.Ordinal);
        Assert.Contains("duplicated", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static ProjectTransferDataSet CreateDataSet(
        params ProjectObjectRecord[] objects)
    {
        var projectId = Assert.Single(objects.Select(item => item.ProjectId).Distinct());
        return new ProjectTransferDataSet
        {
            Projects =
            [
                new Project
                {
                    Id = projectId,
                    Name = "Project",
                    Slug = "project"
                }
            ],
            Objects = [.. objects]
        };
    }

    private static AppDbContext CreateTargetStateContext()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(
            TestApplicationBootstrap.ModuleAssemblies);
        var optionsBuilder = AppDbContextTestOptionsBuilder.Create()
            .UseInMemoryDatabase($"package-target-state-{Guid.NewGuid():N}");
        var options = optionsBuilder.Options;
        return new AppDbContext(options);
    }

    private static ProcessRuntimeStepAssignmentEntity CreateProcessAssignment(
        string launchVariablesJson)
        => new()
        {
            RunId = Guid.NewGuid(),
            StepInstanceId = Guid.NewGuid(),
            PlanId = Guid.NewGuid(),
            StepKey = "test-step",
            RoleKey = "test-role",
            ExecutorKind = "test",
            ExecutorId = "test-executor",
            LaunchVariablesJson = launchVariablesJson,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

    private static ProjectTransferTargetStateGuard CreateTargetStateGuard(
        params IProjectTransferTargetStateParticipant[] participants)
    {
        var suppliedByArea = participants.ToDictionary(
            participant => participant.Area);
        var complete = Enum.GetValues<ProjectTransferTargetStateArea>()
            .Select(area => suppliedByArea.TryGetValue(area, out var participant)
                ? participant
                : new EmptyTargetStateParticipant(area));
        return new ProjectTransferTargetStateGuard(complete);
    }

    private static Project CreateProject(string name)
        => new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Slug = name
        };

    private static Project CreateProjectWithId(Guid id)
        => new()
        {
            Id = id,
            Name = "Project",
            Slug = "project"
        };

    private static ProjectPackageStorageImporter CreateImporter(
        string workspaceRoot,
        IStorageDriver driver)
    {
        var registry = new StorageDriverRegistry([driver]);
        var physicalIdentityPolicy = new ProjectManagedStoragePhysicalIdentityPolicy(
            new FileSystemStoragePathPolicy(
                new TestWorkspacePathResolver(workspaceRoot)),
            TestWorkspaceServices.PhysicalPathPolicyFactory);
        return new ProjectPackageStorageImporter(
            registry,
            physicalIdentityPolicy,
            TestWorkspaceServices.PhysicalPathPolicyFactory,
            new FixedClock(),
            NullLogger<StoragePlacementService>.Instance,
            NullLogger<ProjectPackageService>.Instance);
    }

    private static StorageCatalogRecord CreateStorage(
        StorageProviderKind providerKind,
        string endpointOrRoot,
        StorageCapability capabilities)
    {
        var storage = new StorageCatalogRecord
        {
            Id = Guid.NewGuid(),
            Name = $"Test {providerKind}",
            ProviderKind = providerKind,
            IsEnabled = true,
            EndpointOrRoot = endpointOrRoot,
            CapabilityMask = capabilities,
            HealthStatus = StorageHealthStatus.Healthy,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        if (providerKind == StorageProviderKind.FileSystem)
        {
            StorageCatalogHostBindingPolicy.BindCurrent(storage, endpointOrRoot, DateTimeOffset.UtcNow);
        }

        return storage;
    }

    private static ResolvedDatabaseProfile TestProfile(string workspaceRoot)
        => new(
            new DatabaseProfileRecord
            {
                Id = Guid.NewGuid(),
                DisplayName = "Target",
                ProviderKind = DatabaseProviderKind.InMemory,
                SourceKind = DatabaseProfileSourceKind.InMemory,
                InMemory = new InMemoryDatabaseProfileConnection
                {
                    DatabaseName = $"target-{Guid.NewGuid():N}"
                },
                Storage = new DatabaseProfileStorageDescriptor
                {
                    WorkspaceRoot = workspaceRoot
                }
            },
            DatabaseProfileResolutionSource.PersistedCatalogFallback,
            $"target-{Guid.NewGuid():N}");

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"candoitall-project-package-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset GetUtcNow() => DateTimeOffset.Parse("2026-08-04T00:00:00Z");
    }

    private sealed class EmptyTargetStateParticipant(
        ProjectTransferTargetStateArea area)
        : IProjectTransferTargetStateParticipant
    {
        public ProjectTransferTargetStateArea Area => area;

        public IReadOnlyCollection<Type> EntityTypesToLock { get; } = [];

        public Task<IReadOnlyList<ProjectTransferTargetStateResidue>>
            FindResiduesAsync(
                AppDbContext dbContext,
                CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<ProjectTransferTargetStateResidue>>([]);
    }

    private sealed class TestWorkspacePathResolver(string workspaceRoot) : IWorkspacePathResolver
    {
        public string ResolveWorkspaceRoot() => workspaceRoot;

        public string ResolveManagedFilesRoot() => Path.Combine(workspaceRoot, "managed-files");

        public string ResolveExportsRoot() => Path.Combine(workspaceRoot, "exports");

        public string ResolveEvidenceRoot() => Path.Combine(workspaceRoot, "evidence");

        public string ResolveManagerArtifactsRoot() => Path.Combine(workspaceRoot, "manager-artifacts");
    }

    private sealed class TestStorageDriver(
        StorageProviderKind providerKind,
        StorageCapability supportedCapabilities,
        byte[] readContent,
        bool failDelete = false) : IStorageDriver
    {
        public int DeleteCount { get; private set; }

        public StorageProviderKind ProviderKind => providerKind;

        public StorageCapability SupportedCapabilities => supportedCapabilities;

        public Task<StorageConnectionTestResult> TestConnectionAsync(
            StorageCatalogRecord storage,
            string? secretValue,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new StorageConnectionTestResult(
                true,
                "ok",
                StorageHealthStatus.Healthy,
                supportedCapabilities,
                DateTimeOffset.UtcNow));

        public Task<StorageWriteResult> SaveAsync(
            StorageCatalogRecord storage,
            StorageWriteRequest request,
            CancellationToken cancellationToken = default)
        {
            var locatorKind = providerKind switch
            {
                StorageProviderKind.FileSystem => StorageLocatorKind.RelativePath,
                StorageProviderKind.Ftp => StorageLocatorKind.RemotePath,
                _ => StorageLocatorKind.ContentAddress
            };
            var locator = providerKind == StorageProviderKind.Ipfs
                ? "bafy-test-write"
                : request.RelativePathHint ?? request.FileName;
            var reference = new StorageObjectReference(
                storage.Id,
                providerKind,
                locatorKind,
                locator,
                request.FileName,
                request.ContentType,
                request.Content.LongLength);
            var access = new StorageAccessDescriptor(
                "/preview",
                "/download",
                null,
                true,
                true,
                providerKind == StorageProviderKind.FileSystem,
                request.FileName,
                request.ContentType,
                request.Content.LongLength,
                string.Empty);
            return Task.FromResult(new StorageWriteResult(reference, access));
        }

        public Task<Stream> OpenReadAsync(
            StorageCatalogRecord storage,
            StorageObjectReference reference,
            CancellationToken cancellationToken = default)
            => Task.FromResult<Stream>(new MemoryStream(readContent, writable: false));

        public Task DeleteAsync(
            StorageCatalogRecord storage,
            StorageObjectReference reference,
            CancellationToken cancellationToken = default)
        {
            DeleteCount++;
            if (failDelete)
            {
                throw new IOException("simulated delete failure");
            }

            return Task.CompletedTask;
        }
    }
}
