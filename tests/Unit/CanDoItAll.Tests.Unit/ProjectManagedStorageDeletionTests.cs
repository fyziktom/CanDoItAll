using System.Runtime.InteropServices;
using System.Text.Json;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit.Projects;

[Collection(AppDbContextModelRegistryTestCollectionNames.Name)]
public sealed class ProjectManagedStorageDeletionTests
{
    [Fact]
    public async Task Planner_preserves_shared_bytes_until_every_binding_is_deleted_then_returns_one_reference()
    {
        await using var dbContext = CreateDbContext(out var planner);
        var projectId = Guid.NewGuid();
        var source = CreateObject(projectId, "asset:source");
        var copy = CreateObject(projectId, "asset:copy");
        var duplicate = CreateObject(projectId, "asset:duplicate");
        var unrelated = CreateObject(projectId, "note:unrelated");
        var reference = CreateReference();
        dbContext.AddRange(source, copy, duplicate, unrelated);
        dbContext.AddRange(
            CreateBinding(source.Id, reference),
            CreateBinding(copy.Id, reference),
            CreateBinding(duplicate.Id, reference),
            new ProjectNodeBindingRecord
            {
                ProjectObjectId = unrelated.Id,
                StorageObjectReferenceJson = "not-storage-json"
            });
        await dbContext.SaveChangesAsync();

        var sharedPlan = await planner.PlanAsync(dbContext, [source.Id, duplicate.Id]);
        var finalPlan = await planner.PlanAsync(dbContext, [source.Id, copy.Id, duplicate.Id]);

        Assert.Empty(sharedPlan.References);
        var selected = Assert.Single(finalPlan.References);
        Assert.Equal(reference, selected);
    }

    [Fact]
    public async Task Planner_rejects_malformed_managed_candidate_and_managed_survivor()
    {
        await using var dbContext = CreateDbContext(out var planner);
        var projectId = Guid.NewGuid();
        var candidate = CreateObject(projectId, "asset:candidate");
        var survivor = CreateObject(projectId, "asset:survivor");
        dbContext.AddRange(candidate, survivor);
        dbContext.AddRange(
            new ProjectNodeBindingRecord
            {
                ProjectObjectId = candidate.Id,
                MediaRelativePath = "managed-files/project-media/files/candidate.txt",
                StorageObjectReferenceJson = "{broken"
            },
            CreateBinding(survivor.Id, CreateReference()));
        await dbContext.SaveChangesAsync();

        await Assert.ThrowsAsync<ProjectManagedStorageBindingException>(() =>
            planner.PlanAsync(dbContext, [candidate.Id]));

        var candidateBinding = await dbContext.Set<ProjectNodeBindingRecord>()
            .SingleAsync(binding => binding.ProjectObjectId == candidate.Id);
        candidateBinding.StorageObjectReferenceJson = StorageJson.SerializeReference(CreateReference("candidate.txt"));
        var survivorBinding = await dbContext.Set<ProjectNodeBindingRecord>()
            .SingleAsync(binding => binding.ProjectObjectId == survivor.Id);
        survivorBinding.StorageObjectReferenceJson = "{broken";
        await dbContext.SaveChangesAsync();

        await Assert.ThrowsAsync<ProjectManagedStorageBindingException>(() =>
            planner.PlanAsync(dbContext, [candidate.Id]));
    }

    [Fact]
    public async Task Deletion_service_records_ipfs_retention_without_calling_delete()
    {
        var storageId = Guid.NewGuid();
        var storage = CreateStorage(storageId, StorageProviderKind.Ipfs);
        var driver = new RecordingStorageDriver(StorageProviderKind.Ipfs, StorageCapability.Read);
        var service = CreateDeletionService(
            new StubStorageDriverRegistry(driver),
            CreateDbContextFactory(storage));
        var reference = new StorageObjectReference(
            storageId,
            StorageProviderKind.Ipfs,
            StorageLocatorKind.ContentAddress,
            "bafy-retained");

        var outcome = Assert.Single(await service.DeleteAsync([
            CreateDeletionCandidate(
                reference,
                storage,
                ProjectManagedStorageOwnershipBasis.ImmutableContentAddress)
        ]));

        Assert.Equal(ProjectManagedStorageDeletionOutcomeKind.RetainedByProvider, outcome.Kind);
        Assert.Equal(0, driver.DeleteCalls);
    }

    [Fact]
    public async Task Deletion_service_propagates_driver_failure_for_durable_retry()
    {
        var storageId = Guid.NewGuid();
        var storage = CreateStorage(storageId, StorageProviderKind.FileSystem);
        var driver = new RecordingStorageDriver(
            StorageProviderKind.FileSystem,
            StorageCapability.Delete,
            new IOException("delete failed"));
        var service = CreateDeletionService(
            new StubStorageDriverRegistry(driver),
            CreateDbContextFactory(storage));
        var reference = StampManagedReference(
            CreateReference(storageId: storageId),
            storage);

        await Assert.ThrowsAsync<IOException>(() => service.DeleteAsync([
            CreateDeletionCandidate(
                reference,
                storage,
                ProjectManagedStorageOwnershipBasis.CreationProvenanceV2)
        ]));
        Assert.Equal(1, driver.DeleteCalls);
    }

    [Fact]
    public async Task Planner_uses_stamped_provenance_for_remote_assets_without_media_relative_path()
    {
        await using var dbContext = CreateDbContext(out var planner);
        var projectId = Guid.NewGuid();
        var ftpObject = CreateObject(projectId, "asset:ftp");
        var ipfsObject = CreateObject(projectId, "asset:ipfs");
        var ftpStorageId = Guid.NewGuid();
        var ipfsStorageId = Guid.NewGuid();
        var physicalIdentityPolicy = CreatePhysicalIdentityPolicy();
        var ftpStorage = CreateStorage(ftpStorageId, StorageProviderKind.Ftp);
        var ipfsStorage = CreateStorage(ipfsStorageId, StorageProviderKind.Ipfs);
        var requestedFtpPath = "managed-files/project-media/files/remote.txt";
        var ftpReference = ProjectManagedStorageProvenancePolicy.Stamp(
            new StorageObjectReference(
                ftpStorageId,
                StorageProviderKind.Ftp,
                StorageLocatorKind.RemotePath,
                requestedFtpPath),
            requestedFtpPath,
            ftpStorage,
            physicalIdentityPolicy);
        var ipfsReference = ProjectManagedStorageProvenancePolicy.Stamp(
            new StorageObjectReference(
                ipfsStorageId,
                StorageProviderKind.Ipfs,
                StorageLocatorKind.ContentAddress,
                "bafy-owned"),
            "managed-files/project-media/files/immutable.txt",
            ipfsStorage,
            physicalIdentityPolicy);
        dbContext.AddRange(ftpStorage, ipfsStorage);
        dbContext.AddRange(ftpObject, ipfsObject);
        dbContext.AddRange(
            new ProjectNodeBindingRecord
            {
                ProjectObjectId = ftpObject.Id,
                StorageObjectReferenceJson = StorageJson.SerializeReference(ftpReference)
            },
            new ProjectNodeBindingRecord
            {
                ProjectObjectId = ipfsObject.Id,
                StorageObjectReferenceJson = StorageJson.SerializeReference(ipfsReference)
            });
        await dbContext.SaveChangesAsync();

        var plan = await planner.PlanAsync(
            dbContext,
            [ftpObject.Id, ipfsObject.Id]);

        Assert.Equal(2, plan.References.Count);
        Assert.Contains(plan.References, reference => reference.ProviderKind == StorageProviderKind.Ftp);
        Assert.Contains(plan.References, reference => reference.ProviderKind == StorageProviderKind.Ipfs);
    }

    [Fact]
    public async Task Planner_rejects_managed_path_bound_to_an_unrelated_or_noncanonical_locator()
    {
        await using var dbContext = CreateDbContext(out var planner);
        var projectId = Guid.NewGuid();
        var mismatched = CreateObject(projectId, "asset:mismatch");
        var dotSegment = CreateObject(projectId, "asset:dot-segment");
        var original = ProjectManagedStorageProvenancePolicy.Stamp(
            CreateReference(),
            "managed-files/project-media/files/shared.txt",
            CreateBootstrapStorage(),
            CreatePhysicalIdentityPolicy());
        dbContext.AddRange(mismatched, dotSegment);
        dbContext.AddRange(
            new ProjectNodeBindingRecord
            {
                ProjectObjectId = mismatched.Id,
                MediaRelativePath = "managed-files/project-media/files/shared.txt",
                StorageObjectReferenceJson = StorageJson.SerializeReference(original with
                {
                    Locator = "managed-files/project-media/files/unrelated.txt"
                })
            },
            new ProjectNodeBindingRecord
            {
                ProjectObjectId = dotSegment.Id,
                MediaRelativePath = "managed-files/project-media/files/a/../shared.txt"
            });
        await dbContext.SaveChangesAsync();

        await Assert.ThrowsAsync<ProjectManagedStorageBindingException>(() =>
            planner.PlanAsync(dbContext, [mismatched.Id]));
        await Assert.ThrowsAsync<ProjectManagedStorageBindingException>(() =>
            planner.PlanAsync(dbContext, [dotSegment.Id]));
    }

    [Fact]
    public async Task Planner_normalizes_legacy_windows_media_paths_but_preserves_mixed_bootstrap_references()
    {
        await using var dbContext = CreateDbContext(out var planner);
        var projectId = Guid.NewGuid();
        var legacy = CreateObject(projectId, "asset:legacy");
        var upgradedCopy = CreateObject(projectId, "asset:copy");
        var storageId = Guid.NewGuid();
        StorageCatalogRecord bootstrapStorage = CreateBootstrapStorage();
        bootstrapStorage.Id = storageId;
        dbContext.Add(bootstrapStorage);
        dbContext.AddRange(legacy, upgradedCopy);
        dbContext.AddRange(
            new ProjectNodeBindingRecord
            {
                ProjectObjectId = legacy.Id,
                MediaRelativePath = "managed-files\\project-media\\files\\shared.txt",
                MediaContentType = "text/plain"
            },
            CreateBinding(upgradedCopy.Id, CreateReference(storageId: storageId)));
        await dbContext.SaveChangesAsync();

        var plan = await planner.PlanAsync(
            dbContext,
            [legacy.Id]);

        Assert.Empty(plan.References);
    }

    [Fact]
    public async Task Planner_preserves_windows_file_when_survivor_differs_only_by_path_case()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        Directory.CreateDirectory(Path.Combine(
            StubWorkspacePathResolver.RootPath,
            "managed-files",
            "project-media",
            "files"));
        try
        {
            await using var dbContext = CreateDbContext(out var planner);
            var projectId = Guid.NewGuid();
            var candidate = CreateObject(projectId, "asset:candidate");
            var survivor = CreateObject(projectId, "asset:survivor");
            dbContext.AddRange(candidate, survivor);
            dbContext.AddRange(
                new ProjectNodeBindingRecord
                {
                    ProjectObjectId = candidate.Id,
                    MediaRelativePath = "managed-files/project-media/files/Shared.txt",
                    StorageObjectReferenceJson = StorageJson.SerializeReference(CreateReference("Shared.txt"))
                },
                CreateBinding(survivor.Id, CreateReference("shared.txt")));
            await dbContext.SaveChangesAsync();

            var plan = await planner.PlanAsync(
                dbContext,
                [candidate.Id]);

            Assert.Empty(plan.References);
        }
        finally
        {
            Directory.Delete(StubWorkspacePathResolver.RootPath, recursive: true);
        }
    }

    [Fact]
    public async Task Planner_does_not_conflate_case_distinct_files_on_a_sensitive_root()
    {
        Directory.CreateDirectory(Path.Combine(
            StubWorkspacePathResolver.RootPath,
            "managed-files",
            "project-media",
            "files"));
        if (TestWorkspaceServices.PhysicalPathPolicyFactory
                .Create(StubWorkspacePathResolver.RootPath)
                .CaseSensitivity != PhysicalFileSystemCaseSensitivity.Sensitive)
        {
            Directory.Delete(StubWorkspacePathResolver.RootPath, recursive: true);
            return;
        }

        try
        {
            await using var dbContext = CreateDbContext(out var planner);
            var projectId = Guid.NewGuid();
            var candidate = CreateObject(projectId, "asset:candidate-sensitive");
            var survivor = CreateObject(projectId, "asset:survivor-sensitive");
            dbContext.AddRange(candidate, survivor);
            dbContext.AddRange(
                new ProjectNodeBindingRecord
                {
                    ProjectObjectId = candidate.Id,
                    MediaRelativePath = "managed-files/project-media/files/Shared.txt",
                    StorageObjectReferenceJson = StorageJson.SerializeReference(CreateReference("Shared.txt"))
                },
                CreateBinding(survivor.Id, CreateReference("shared.txt")));
            await dbContext.SaveChangesAsync();

            ProjectManagedStorageDeletionPlan plan = await planner.PlanAsync(
                dbContext,
                [candidate.Id]);

            Assert.Equal(
                "managed-files/project-media/files/Shared.txt",
                Assert.Single(plan.References).Locator);
        }
        finally
        {
            Directory.Delete(StubWorkspacePathResolver.RootPath, recursive: true);
        }
    }

    [Fact]
    public async Task Deletion_service_treats_ipfs_as_terminal_even_when_catalog_entry_is_missing()
    {
        var storage = CreateStorage(Guid.NewGuid(), StorageProviderKind.Ipfs);
        var driver = new RecordingStorageDriver(StorageProviderKind.Ipfs, StorageCapability.Read);
        var service = CreateDeletionService(
            new StubStorageDriverRegistry(driver),
            CreateDbContextFactory());
        var reference = new StorageObjectReference(
            storage.Id,
            StorageProviderKind.Ipfs,
            StorageLocatorKind.ContentAddress,
            "bafy-missing-catalog");

        var outcome = Assert.Single(await service.DeleteAsync([
            CreateDeletionCandidate(
                reference,
                storage,
                ProjectManagedStorageOwnershipBasis.ImmutableContentAddress)
        ]));

        Assert.Equal(ProjectManagedStorageDeletionOutcomeKind.RetainedByProvider, outcome.Kind);
        Assert.Equal(0, driver.DeleteCalls);
    }

    [Fact]
    public async Task Deletion_service_fails_when_a_mutable_provider_unexpectedly_lacks_delete()
    {
        var storage = CreateStorage(Guid.NewGuid(), StorageProviderKind.Ftp);
        storage.EndpointOrRoot = "ftp://storage.example.test";
        var driver = new RecordingStorageDriver(StorageProviderKind.Ftp, StorageCapability.Read);
        var service = CreateDeletionService(
            new StubStorageDriverRegistry(driver),
            CreateDbContextFactory(storage));
        var reference = StampManagedReference(
            new StorageObjectReference(
                storage.Id,
                StorageProviderKind.Ftp,
                StorageLocatorKind.RemotePath,
                "managed-files/project-media/files/file.txt"),
            storage);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync([
            CreateDeletionCandidate(
                reference,
                storage,
                ProjectManagedStorageOwnershipBasis.CreationProvenanceV2)
        ]));
        Assert.Equal(0, driver.DeleteCalls);
    }

    [Fact]
    public async Task Deletion_service_retains_legacy_mutable_payload_reference_without_calling_driver()
    {
        var storage = CreateStorage(Guid.NewGuid(), StorageProviderKind.FileSystem);
        var driver = new RecordingStorageDriver(
            StorageProviderKind.FileSystem,
            StorageCapability.Delete);
        var service = CreateDeletionService(
            new StubStorageDriverRegistry(driver),
            CreateDbContextFactory(storage));
        var reference = CreateReference(storageId: storage.Id);

        var outcome = Assert.Single(await service.DeleteAsync([
            CreateDeletionCandidate(
                reference,
                storage,
                ProjectManagedStorageOwnershipBasis.UnverifiedLegacyPayload)
        ]));

        Assert.Equal(
            ProjectManagedStorageDeletionOutcomeKind.RetainedWithoutOwnershipProof,
            outcome.Kind);
        Assert.Equal(0, driver.DeleteCalls);
    }

    [Fact]
    public async Task Planner_refuses_retargeted_storage_before_unrelated_destination_bytes_can_be_deleted()
    {
        await using var dbContext = CreateDbContext(out var planner);
        var projectId = Guid.NewGuid();
        var asset = CreateObject(projectId, "asset:retargeted");
        var storage = CreateStorage(Guid.NewGuid(), StorageProviderKind.FileSystem);
        storage.Name = "Retargeted storage";
        storage.EndpointOrRoot = Path.Combine(Path.GetTempPath(), "storage-origin-a");
        var reference = new StorageObjectReference(
            storage.Id,
            StorageProviderKind.FileSystem,
            StorageLocatorKind.RelativePath,
            "managed-files/project-media/files/retargeted.txt");
        var stamped = ProjectManagedStorageProvenancePolicy.Stamp(
            reference,
            reference.Locator,
            storage,
            CreatePhysicalIdentityPolicy());
        dbContext.Add(asset);
        dbContext.Add(storage);
        dbContext.Add(new ProjectNodeBindingRecord
        {
            ProjectObjectId = asset.Id,
            MediaRelativePath = reference.Locator,
            StorageObjectReferenceJson = StorageJson.SerializeReference(stamped)
        });
        await dbContext.SaveChangesAsync();
        storage.EndpointOrRoot = Path.Combine(Path.GetTempPath(), "storage-retarget-b");
        await dbContext.SaveChangesAsync();

        await Assert.ThrowsAsync<ProjectManagedStorageBindingException>(() =>
            planner.PlanAsync(dbContext, [asset.Id]));
    }

    [Fact]
    public async Task Planner_uses_physical_root_not_catalog_id_for_survivor_liveness()
    {
        await using var dbContext = CreateDbContext(out var planner);
        var projectId = Guid.NewGuid();
        var candidate = CreateObject(projectId, "asset:catalog-a");
        var survivor = CreateObject(projectId, "asset:catalog-b");
        var sharedRoot = Path.Combine(Path.GetTempPath(), "storage-alias-root");
        var storageA = CreateStorage(Guid.NewGuid(), StorageProviderKind.FileSystem);
        storageA.Name = "Alias A";
        storageA.EndpointOrRoot = sharedRoot;
        var storageB = CreateStorage(Guid.NewGuid(), StorageProviderKind.FileSystem);
        storageB.Name = "Alias B";
        storageB.EndpointOrRoot = sharedRoot;
        var policy = CreatePhysicalIdentityPolicy();
        var referenceA = CreateReference(storageId: storageA.Id);
        var referenceB = CreateReference(storageId: storageB.Id);
        dbContext.AddRange(candidate, survivor, storageA, storageB);
        dbContext.AddRange(
            CreateBinding(candidate.Id, ProjectManagedStorageProvenancePolicy.Stamp(
                referenceA,
                referenceA.Locator,
                storageA,
                policy)),
            CreateBinding(survivor.Id, ProjectManagedStorageProvenancePolicy.Stamp(
                referenceB,
                referenceB.Locator,
                storageB,
                policy)));
        await dbContext.SaveChangesAsync();

        var plan = await planner.PlanAsync(dbContext, [candidate.Id]);

        Assert.Empty(plan.References);
    }

    [Fact]
    public async Task Planner_preserves_windows_file_when_survivor_uses_a_hard_link_alias()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var root = TestFileSystem.CreateTemporaryRoot("managed-storage-hard-link");
        try
        {
            const string candidateLocator = "managed-files/project-media/files/original.txt";
            const string survivorLocator = "managed-files/project-media/files/alias.txt";
            var originalPath = Path.Combine(root, candidateLocator.Replace('/', Path.DirectorySeparatorChar));
            var aliasPath = Path.Combine(root, survivorLocator.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(originalPath)!);
            await File.WriteAllTextAsync(originalPath, "shared bytes");
            Assert.True(
                CreateHardLink(aliasPath, originalPath, IntPtr.Zero),
                $"CreateHardLinkW failed with Win32 error {Marshal.GetLastWin32Error()}.");

            await using var dbContext = CreateDbContext(out var planner);
            var projectId = Guid.NewGuid();
            var candidate = CreateObject(projectId, "asset:hard-link-original");
            var survivor = CreateObject(projectId, "asset:hard-link-alias");
            var storageA = CreateStorage(Guid.NewGuid(), StorageProviderKind.FileSystem);
            storageA.EndpointOrRoot = root;
            var storageB = CreateStorage(Guid.NewGuid(), StorageProviderKind.FileSystem);
            storageB.EndpointOrRoot = root;
            var policy = CreatePhysicalIdentityPolicy();
            var referenceA = CreateReference("original.txt", storageA.Id);
            var referenceB = CreateReference("alias.txt", storageB.Id);
            var stampedA = ProjectManagedStorageProvenancePolicy.Stamp(
                referenceA,
                candidateLocator,
                storageA,
                policy);
            var stampedB = ProjectManagedStorageProvenancePolicy.Stamp(
                referenceB,
                survivorLocator,
                storageB,
                policy);
            dbContext.AddRange(candidate, survivor, storageA, storageB);
            dbContext.AddRange(
                new ProjectNodeBindingRecord
                {
                    ProjectObjectId = candidate.Id,
                    MediaRelativePath = candidateLocator,
                    StorageObjectReferenceJson = StorageJson.SerializeReference(stampedA)
                },
                new ProjectNodeBindingRecord
                {
                    ProjectObjectId = survivor.Id,
                    MediaRelativePath = survivorLocator,
                    StorageObjectReferenceJson = StorageJson.SerializeReference(stampedB)
                });
            await dbContext.SaveChangesAsync();

            var plan = await planner.PlanAsync(dbContext, [candidate.Id]);

            Assert.Empty(plan.References);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(root);
        }
    }

    [Theory]
    [InlineData(@"C:\managed\asset.txt", @"\\?\C:\managed\asset.txt")]
    [InlineData(@"\\server\share\asset.txt", @"\\?\UNC\server\share\asset.txt")]
    [InlineData(@"\\?\C:\managed\asset.txt", @"\\?\C:\managed\asset.txt")]
    [InlineData(@"\\.\PhysicalDrive0", @"\\.\PhysicalDrive0")]
    public void Windows_create_file_path_uses_extended_length_form_without_rewriting_device_paths(
        string path,
        string expected)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        Assert.Equal(expected, WindowsFileSystemObjectIdentity.ResolveCreateFilePath(path));
    }

    [Fact]
    public async Task Windows_identity_fingerprints_and_stamps_a_real_file_beyond_legacy_max_path()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var root = TestFileSystem.CreateTemporaryRoot("managed-storage-long-path");
        var segments = Enumerable.Range(0, 7)
            .Select(index => $"segment-{index:D2}-{new string('x', 32)}");
        var locator =
            $"managed-files/project-media/files/{string.Join('/', segments)}/evidence.bin";
        var fullPath = Path.Combine(
            root,
            locator.Replace('/', Path.DirectorySeparatorChar));
        try
        {
            Assert.True(
                fullPath.Length > 260,
                $"The test path must exceed the legacy Windows MAX_PATH boundary, but was {fullPath.Length} characters.");
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            await File.WriteAllBytesAsync(fullPath, [0x43, 0x44, 0x49, 0x41]);
            var storage = CreateStorage(Guid.NewGuid(), StorageProviderKind.FileSystem);
            storage.EndpointOrRoot = root;
            var reference = new StorageObjectReference(
                storage.Id,
                StorageProviderKind.FileSystem,
                StorageLocatorKind.RelativePath,
                locator);
            var policy = CreatePhysicalIdentityPolicy();

            var fingerprint = policy.ResolveObjectFingerprint(reference, storage);
            var livenessKey = policy.ResolveConservativeLivenessKey(reference, storage);
            var stamped = ProjectManagedStorageProvenancePolicy.Stamp(
                reference,
                locator,
                storage,
                policy);

            Assert.Equal(64, fingerprint.Length);
            Assert.Equal(64, livenessKey.Length);
            Assert.True(
                ProjectManagedStorageProvenancePolicy.TryValidate(
                    stamped,
                    locator,
                    out var validationError),
                validationError);
            using var metadata = JsonDocument.Parse(stamped.MetadataJson);
            Assert.Equal(
                fingerprint,
                metadata.RootElement
                    .GetProperty("physicalObjectFingerprint")
                    .GetString());
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(root);
        }
    }

    [Fact]
    public async Task Deletion_service_refuses_unix_symlink_liveness_alias_without_deleting_the_real_object()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var root = TestFileSystem.CreateTemporaryRoot("managed-storage-symbolic-link");
        var physicalRoot = Path.Combine(root, "physical");
        var symbolicRoot = Path.Combine(root, "symbolic");
        try
        {
            const string locator = "managed-files/project-media/files/shared.txt";
            var physicalPath = Path.Combine(
                physicalRoot,
                locator.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(physicalPath)!);
            await File.WriteAllTextAsync(physicalPath, "survivor bytes");

            var physicalStorage = CreateStorage(
                Guid.NewGuid(),
                StorageProviderKind.FileSystem);
            physicalStorage.EndpointOrRoot = physicalRoot;
            var symbolicStorage = CreateStorage(
                Guid.NewGuid(),
                StorageProviderKind.FileSystem);
            symbolicStorage.EndpointOrRoot = symbolicRoot;
            var reference = new StorageObjectReference(
                physicalStorage.Id,
                StorageProviderKind.FileSystem,
                StorageLocatorKind.RelativePath,
                locator);
            var stampedReference = StampManagedReference(reference, physicalStorage);

            Directory.CreateSymbolicLink(symbolicRoot, physicalRoot);
            var survivorReference = reference with
            {
                StorageId = symbolicStorage.Id
            };
            var dbContextFactory = CreateDbContextFactory(
                physicalStorage,
                symbolicStorage);
            await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
            {
                var survivor = CreateObject(Guid.NewGuid(), "asset:symlink-survivor");
                dbContext.Add(survivor);
                dbContext.Add(new ProjectNodeBindingRecord
                {
                    ProjectObjectId = survivor.Id,
                    MediaRelativePath = locator,
                    StorageObjectReferenceJson = StorageJson.SerializeReference(survivorReference)
                });
                await dbContext.SaveChangesAsync();
            }

            var driver = new RecordingStorageDriver(
                StorageProviderKind.FileSystem,
                StorageCapability.Delete);
            var service = CreateDeletionService(
                new StubStorageDriverRegistry(driver),
                dbContextFactory);

            var failure = await Assert.ThrowsAsync<StorageBrowseException>(() =>
                service.DeleteAsync([
                    CreateDeletionCandidate(
                        stampedReference,
                        physicalStorage,
                        ProjectManagedStorageOwnershipBasis.CreationProvenanceV2)
                ]));

            Assert.Equal(StorageBrowseErrorCode.AccessDenied, failure.Error.Code);
            Assert.Equal(0, driver.DeleteCalls);
            Assert.True(File.Exists(physicalPath));
            Assert.Equal("survivor bytes", await File.ReadAllTextAsync(physicalPath));
        }
        finally
        {
            if (Directory.Exists(symbolicRoot))
            {
                Directory.Delete(symbolicRoot);
            }

            TestFileSystem.DeleteDirectoryWithRetry(root);
        }
    }

    [Fact]
    public async Task Planner_does_not_conflate_same_locator_under_different_physical_roots()
    {
        await using var dbContext = CreateDbContext(out var planner);
        var projectId = Guid.NewGuid();
        var candidate = CreateObject(projectId, "asset:root-a");
        var survivor = CreateObject(projectId, "asset:root-b");
        var storageA = CreateStorage(Guid.NewGuid(), StorageProviderKind.FileSystem);
        storageA.Name = "Root A";
        storageA.EndpointOrRoot = Path.Combine(Path.GetTempPath(), "physical-root-a");
        var storageB = CreateStorage(Guid.NewGuid(), StorageProviderKind.FileSystem);
        storageB.Name = "Root B";
        storageB.EndpointOrRoot = Path.Combine(Path.GetTempPath(), "physical-root-b");
        var policy = CreatePhysicalIdentityPolicy();
        var referenceA = CreateReference(storageId: storageA.Id);
        var referenceB = CreateReference(storageId: storageB.Id);
        dbContext.AddRange(candidate, survivor, storageA, storageB);
        dbContext.AddRange(
            CreateBinding(candidate.Id, ProjectManagedStorageProvenancePolicy.Stamp(
                referenceA,
                referenceA.Locator,
                storageA,
                policy)),
            CreateBinding(survivor.Id, ProjectManagedStorageProvenancePolicy.Stamp(
                referenceB,
                referenceB.Locator,
                storageB,
                policy)));
        await dbContext.SaveChangesAsync();

        var plan = await planner.PlanAsync(dbContext, [candidate.Id]);

        Assert.Single(plan.References);
    }

    [Fact]
    public async Task Planner_conservatively_preserves_ftp_case_alias_but_detects_base_path_retargeting()
    {
        await using var dbContext = CreateDbContext(out var planner);
        var projectId = Guid.NewGuid();
        var candidate = CreateObject(projectId, "asset:ftp-a");
        var survivor = CreateObject(projectId, "asset:ftp-b");
        var storageA = CreateStorage(Guid.NewGuid(), StorageProviderKind.Ftp);
        storageA.Name = "FTP A";
        storageA.EndpointOrRoot = "ftp://files.example.test";
        storageA.ConfigJson = "{\"basePath\":\"origin\"}";
        var storageB = CreateStorage(Guid.NewGuid(), StorageProviderKind.Ftp);
        storageB.Name = "FTP B";
        storageB.EndpointOrRoot = "ftp://files.example.test";
        storageB.ConfigJson = "{\"basePath\":\"origin\"}";
        var policy = CreatePhysicalIdentityPolicy();
        var candidatePath = "managed-files/project-media/files/Foo.txt";
        var survivorPath = "managed-files/project-media/files/foo.txt";
        var referenceA = new StorageObjectReference(
            storageA.Id,
            StorageProviderKind.Ftp,
            StorageLocatorKind.RemotePath,
            candidatePath);
        var referenceB = new StorageObjectReference(
            storageB.Id,
            StorageProviderKind.Ftp,
            StorageLocatorKind.RemotePath,
            survivorPath);
        dbContext.AddRange(candidate, survivor, storageA, storageB);
        dbContext.AddRange(
            new ProjectNodeBindingRecord
            {
                ProjectObjectId = candidate.Id,
                StorageObjectReferenceJson = StorageJson.SerializeReference(
                    ProjectManagedStorageProvenancePolicy.Stamp(
                        referenceA,
                        candidatePath,
                        storageA,
                        policy))
            },
            new ProjectNodeBindingRecord
            {
                ProjectObjectId = survivor.Id,
                StorageObjectReferenceJson = StorageJson.SerializeReference(
                    ProjectManagedStorageProvenancePolicy.Stamp(
                        referenceB,
                        survivorPath,
                        storageB,
                        policy))
            });
        await dbContext.SaveChangesAsync();

        Assert.Empty((await planner.PlanAsync(dbContext, [candidate.Id])).References);

        storageA.ConfigJson = "{\"basePath\":\"Origin\"}";
        await dbContext.SaveChangesAsync();
        await Assert.ThrowsAsync<ProjectManagedStorageBindingException>(() =>
            planner.PlanAsync(dbContext, [candidate.Id]));
    }

    [Fact]
    public void Storage_facts_preserve_bootstrap_host_binding_and_filesystem_and_ftp_identity() {
        var bootstrap = CreateBootstrapStorage();
        var facts = StorageCatalogPlanningFact.FromCatalogRecord(bootstrap, includeFtpAddressing: false);
        var paths = new FileSystemStoragePathPolicy(new StubWorkspacePathResolver());
        var reference = CreateReference(storageId: bootstrap.Id);
        Assert.Equal(paths.ResolveRootPath(bootstrap), paths.ResolveRootPathFromFacts(facts));
        Assert.Equal(paths.ResolveFullPath(bootstrap, reference.Locator), paths.ResolveFullPathFromFacts(facts, reference.Locator));
        Assert.Equal(paths.ResolveFullPath(bootstrap, reference.Locator), paths.ResolveWorkspaceFullPath(reference.Locator));
        Assert.Equal(bootstrap.Id, StorageBootstrapCatalogPolicy.ResolveAuthoritativeFileSystemStorageFact(
            [facts], paths.ResolveWorkspaceRootPath())?.Id);
        var physical = CreatePhysicalIdentityPolicy();
        Assert.Equal(physical.ResolveObjectFingerprint(reference, bootstrap, bootstrap.Id),
            physical.ResolveObjectFingerprintFromFacts(reference, facts, bootstrap.Id));
        var ftp = CreateStorage(Guid.NewGuid(), StorageProviderKind.Ftp);
        ftp.EndpointOrRoot = "ftps://example.invalid/Root";
        ftp.ConfigJson = "{\"port\":2121,\"basePath\":\"MixedCase\"}";
        var ftpFacts = StorageCatalogPlanningFact.FromCatalogRecord(ftp, includeFtpAddressing: true);
        const string remotePath = "managed-files/project-media/files/Asset.txt";
        Assert.Equal(FtpStorageAddressPolicy.ResolveObjectUri(ftp, remotePath),
            FtpStorageAddressPolicy.ResolveObjectUriFromFacts(ftpFacts, remotePath));
        Assert.Equal("/Root/MixedCase/managed-files/project-media/files/Asset.txt",
            FtpStorageAddressPolicy.ResolveObjectUriFromFacts(ftpFacts, remotePath).AbsolutePath);
        Assert.Equal(2121, FtpStorageAddressPolicy.ResolveObjectUriFromFacts(ftpFacts, remotePath).Port);
    }

    [Fact]
    public async Task Planner_parses_malformed_ftp_configuration_only_when_a_reached_binding_references_it() {
        await using var context = CreateDbContext(out var planner);
        var bootstrap = CreateBootstrapStorage();
        var malformed = CreateStorage(Guid.NewGuid(), StorageProviderKind.Ftp);
        malformed.Name = "Unrelated malformed FTP";
        malformed.ConfigJson = "{";
        var item = CreateObject(Guid.NewGuid(), "asset:selective-configuration");
        var reference = StampManagedReference(CreateReference(storageId: bootstrap.Id), bootstrap);
        var binding = CreateBinding(item.Id, reference);
        context.AddRange(bootstrap, malformed, item, binding);
        await context.SaveChangesAsync();
        Assert.Single((await planner.PlanAsync(context, [item.Id])).References);
        binding.StorageObjectReferenceJson = StorageJson.SerializeReference(new StorageObjectReference(
            malformed.Id, StorageProviderKind.Ftp, StorageLocatorKind.RemotePath,
            reference.Locator, "shared.txt", "text/plain"));
        await context.SaveChangesAsync();
        await Assert.ThrowsAsync<JsonException>(() => planner.PlanAsync(context, [item.Id]));
        binding.StorageObjectReferenceJson = StorageJson.SerializeReference(CreateReference(storageId: malformed.Id));
        await context.SaveChangesAsync();
        var mismatch = await Assert.ThrowsAsync<ProjectManagedStorageBindingException>(() => planner.PlanAsync(context, [item.Id]));
        Assert.Contains("provider does not match", mismatch.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Planner_does_not_parse_surviving_ftp_configuration_when_deleted_objects_have_no_media() {
        await using var context = CreateDbContext(out var planner);
        var malformed = CreateStorage(Guid.NewGuid(), StorageProviderKind.Ftp);
        malformed.ConfigJson = "{";
        var item = CreateObject(Guid.NewGuid(), "note:no-media");
        var survivor = CreateObject(Guid.NewGuid(), "asset:surviving-malformed-config");
        var reference = new StorageObjectReference(malformed.Id, StorageProviderKind.Ftp,
            StorageLocatorKind.RemotePath, "managed-files/project-media/files/shared.txt");
        context.AddRange(malformed, item, survivor, CreateBinding(survivor.Id, reference));
        await context.SaveChangesAsync();
        Assert.Empty((await planner.PlanAsync(context, [item.Id])).References);
    }

    [Fact]
    public async Task Storage_owner_requires_callback_success_and_retains_the_catalog_record_used_for_validation() {
        var storage = CreateStorage(Guid.NewGuid(), StorageProviderKind.Ftp);
        storage.EndpointOrRoot = "ftp://example.invalid/original";
        storage.ConfigJson = "{\"basePath\":\"Original\"}";
        var factory = CreateDbContextFactory(storage);
        var driver = new RecordingStorageDriver(StorageProviderKind.Ftp, StorageCapability.Delete);
        var owner = new StorageObjectDeletionService(factory.StorageFactory, new StubStorageDriverRegistry(driver),
            new FileSystemStoragePathPolicy(new StubWorkspacePathResolver()));
        var reference = new StorageObjectReference(storage.Id, storage.ProviderKind, StorageLocatorKind.RemotePath, "asset.txt");
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.DeleteUnderCallerBindingGateAsync(reference,
            (_, _) => throw new InvalidOperationException("The caller refused the current binding state.")));
        Assert.Equal(0, driver.DeleteCalls);
        await owner.DeleteUnderCallerBindingGateAsync(reference, async (facts, cancellationToken) => {
            Assert.Equal(storage.Id, facts.Storage.Id);
            Assert.Equal("Original", facts.Storage.FtpAddressing?.BasePath);
            await using var edit = await factory.StorageFactory.CreateDbContextAsync(cancellationToken);
            var row = await edit.Set<StorageCatalogRecord>().SingleAsync(cancellationToken);
            row.EndpointOrRoot = "ftp://example.invalid/retargeted";
            row.ConfigJson = "{\"basePath\":\"Retargeted\"}";
            await edit.SaveChangesAsync(cancellationToken);
        });
        Assert.Equal(1, driver.DeleteCalls);
        var used = Assert.IsType<StorageCatalogRecord>(driver.LastDeletedStorage);
        Assert.Equal(storage.EndpointOrRoot, used.EndpointOrRoot);
        Assert.Equal(storage.ConfigJson, used.ConfigJson);
        await using var readback = await factory.StorageFactory.CreateDbContextAsync();
        Assert.Equal("ftp://example.invalid/retargeted", (await readback.Set<StorageCatalogRecord>().SingleAsync()).EndpointOrRoot);
    }

    private static AppDbContext CreateDbContext(out ProjectManagedStorageDeletionPlanner planner) {
        var factory = CreateDbContextFactory();
        planner = new(CreatePhysicalIdentityPolicy(), factory.Catalog, factory.Coordinator);
        return factory.CreateDbContext();
    }

    private static TestDbContextFactory CreateDbContextFactory(params StorageCatalogRecord[] storages) {
        AppDbContextModelRegistry.ConfigureAssemblies([typeof(WorkbenchModuleAssemblyMarker).Assembly]);
        var databaseName = $"project-storage-deletion-{Guid.NewGuid():N}";
        var root = new InMemoryDatabaseRoot();
        var profile = new ResolvedDatabaseProfile(new() { ProviderKind = DatabaseProviderKind.InMemory },
            DatabaseProfileResolutionSource.ExplicitOverride, databaseName);
        var options = new DbContextOptionsBuilder<AppDbContext>();
        AppDbContextOptionsConfigurator.Configure(options, profile);
        options.UseInMemoryDatabase(databaseName, root);
        var storageOptions = new DbContextOptionsBuilder<StorageDbContext>();
        AppDbContextOptionsConfigurator.Configure(storageOptions, profile);
        storageOptions.UseInMemoryDatabase(databaseName, root);
        var workbenchOptions = new DbContextOptionsBuilder<WorkbenchDbContext>();
        AppDbContextOptionsConfigurator.Configure(workbenchOptions, profile);
        workbenchOptions.UseInMemoryDatabase(databaseName, root);
        var factory = new TestDbContextFactory(options.Options, storageOptions.Options, workbenchOptions.Options, profile);
        using var dbContext = factory.CreateDbContext();
        dbContext.AddRange(storages);
        dbContext.SaveChanges();
        return factory;
    }

    private static ProjectManagedStorageDeletionService CreateDeletionService(IStorageDriverRegistry drivers, TestDbContextFactory factory) {
        return new(new StorageObjectDeletionService(factory.StorageFactory, drivers,
            new FileSystemStoragePathPolicy(new StubWorkspacePathResolver())), CreatePhysicalIdentityPolicy(), factory.WorkbenchFactory, factory.Catalog);
    }

    private static ProjectManagedStoragePhysicalIdentityPolicy CreatePhysicalIdentityPolicy()
        => new(
            new FileSystemStoragePathPolicy(new StubWorkspacePathResolver()),
            TestWorkspaceServices.PhysicalPathPolicyFactory);

    private static ProjectManagedStorageDeletionCandidate CreateDeletionCandidate(
        StorageObjectReference reference,
        StorageCatalogRecord storage,
        ProjectManagedStorageOwnershipBasis ownershipBasis)
    {
        var physicalIdentityPolicy = CreatePhysicalIdentityPolicy();
        return new ProjectManagedStorageDeletionCandidate(
            reference,
            ownershipBasis,
            physicalIdentityPolicy.ResolveObjectFingerprint(
                reference,
                storage,
                authoritativeBootstrapStorageId: Guid.Empty),
            reference.ProviderKind == StorageProviderKind.Ipfs
                ? string.Empty
                : reference.Locator);
    }

    private static StorageCatalogRecord CreateBootstrapStorage()
    {
        var storage = new StorageCatalogRecord
        {
            Id = Guid.NewGuid(),
            Name = "Bootstrap",
            ProviderKind = StorageProviderKind.FileSystem,
            IsSystemDefault = true,
            IsEnabled = true,
            EndpointOrRoot = StubWorkspacePathResolver.RootPath,
            CapabilityMask = StorageCapability.Read | StorageCapability.Delete
        };
        StorageCatalogHostBindingPolicy.BindCurrent(
            storage,
            storage.EndpointOrRoot,
            DateTimeOffset.UtcNow);
        return storage;
    }

    private static ProjectObjectRecord CreateObject(Guid projectId, string nodeKey)
        => new()
        {
            ProjectId = projectId,
            NodeKey = nodeKey,
            Title = nodeKey,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

    private static ProjectNodeBindingRecord CreateBinding(
        Guid projectObjectId,
        StorageObjectReference reference)
        => new()
        {
            ProjectObjectId = projectObjectId,
            MediaRelativePath = "managed-files/project-media/files/shared.txt",
            MediaContentType = "text/plain",
            MediaOriginalFileName = "shared.txt",
            StorageObjectReferenceJson = StorageJson.SerializeReference(reference)
        };

    private static StorageObjectReference CreateReference(
        string fileName = "shared.txt",
        Guid? storageId = null)
        => new(
            storageId,
            StorageProviderKind.FileSystem,
            StorageLocatorKind.RelativePath,
            $"managed-files/project-media/files/{fileName}",
            fileName,
            "text/plain");

    private static StorageObjectReference StampManagedReference(
        StorageObjectReference reference,
        StorageCatalogRecord storage)
        => ProjectManagedStorageProvenancePolicy.Stamp(
            reference,
            reference.Locator,
            storage,
            CreatePhysicalIdentityPolicy());

    private static StorageCatalogRecord CreateStorage(Guid id, StorageProviderKind providerKind)
    {
        var storage = new StorageCatalogRecord
        {
            Id = id,
            Name = "Deletion storage",
            ProviderKind = providerKind,
            IsEnabled = true,
            CapabilityMask = StorageCapability.Read | StorageCapability.Delete,
            EndpointOrRoot = providerKind == StorageProviderKind.FileSystem
                ? StubWorkspacePathResolver.RootPath
                : "unused"
        };
        if (providerKind == StorageProviderKind.FileSystem)
        {
            StorageCatalogHostBindingPolicy.BindCurrent(
                storage,
                storage.EndpointOrRoot,
                DateTimeOffset.UtcNow);
        }

        return storage;
    }

    private sealed class StubWorkspacePathResolver : IWorkspacePathResolver
    {
        private static readonly string Root = Path.Combine(
            Path.GetTempPath(),
            "candoitall-storage-deletion-tests");

        internal static string RootPath => Root;

        public string ResolveWorkspaceRoot()
        {
            Directory.CreateDirectory(Root);
            return Root;
        }

        public string ResolveManagedFilesRoot() => Path.Combine(Root, "managed-files");

        public string ResolveExportsRoot() => Path.Combine(Root, "exports");

        public string ResolveEvidenceRoot() => Path.Combine(Root, "evidence");

        public string ResolveManagerArtifactsRoot() => Path.Combine(Root, "manager-artifacts");
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(
        string fileName,
        string existingFileName,
        IntPtr securityAttributes);

    private sealed class TestDbContextFactory : IDbContextFactory<AppDbContext> {
        private readonly DbContextOptions<AppDbContext> options;

        public TestDbContextFactory(DbContextOptions<AppDbContext> options, DbContextOptions<StorageDbContext> storageOptions,
            DbContextOptions<WorkbenchDbContext> workbenchOptions, ResolvedDatabaseProfile profile) {
            this.options = options;
            Coordinator = CoordinatedDatabaseTransaction.ForProfile(profile);
            StorageFactory = new PooledDbContextFactory<StorageDbContext>(storageOptions);
            WorkbenchFactory = new PooledDbContextFactory<WorkbenchDbContext>(workbenchOptions);
            Catalog = new(StorageFactory, new StubWorkspacePathResolver(), new SystemClock(), storageOptions, Coordinator);
        }

        public CoordinatedDatabaseTransaction Coordinator { get; }
        public IDbContextFactory<StorageDbContext> StorageFactory { get; }
        public IDbContextFactory<WorkbenchDbContext> WorkbenchFactory { get; }
        public StorageCatalogService Catalog { get; }
        public AppDbContext CreateDbContext() => new(options);
        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
    }

    private sealed class StubStorageDriverRegistry(IStorageDriver driver) : IStorageDriverRegistry
    {
        public IReadOnlyCollection<StorageProviderKind> RegisteredKinds => [driver.ProviderKind];

        public bool TryResolve(StorageProviderKind providerKind, out IStorageDriver resolved)
        {
            resolved = driver;
            return providerKind == driver.ProviderKind;
        }

        public IStorageDriver Resolve(StorageProviderKind providerKind)
            => providerKind == driver.ProviderKind
                ? driver
                : throw new InvalidOperationException();
    }

    private sealed class RecordingStorageDriver(
        StorageProviderKind providerKind,
        StorageCapability supportedCapabilities,
        Exception? deletionFailure = null) : IStorageDriver
    {
        public StorageProviderKind ProviderKind => providerKind;

        public StorageCapability SupportedCapabilities => supportedCapabilities;

        public int DeleteCalls { get; private set; }

        public StorageCatalogRecord? LastDeletedStorage { get; private set; }

        public Task<StorageConnectionTestResult> TestConnectionAsync(
            StorageCatalogRecord storage,
            string? secretValue,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<StorageWriteResult> SaveAsync(
            StorageCatalogRecord storage,
            StorageWriteRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Stream> OpenReadAsync(
            StorageCatalogRecord storage,
            StorageObjectReference reference,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteAsync(
            StorageCatalogRecord storage,
            StorageObjectReference reference,
            CancellationToken cancellationToken = default)
        {
            LastDeletedStorage = storage;
            DeleteCalls++;
            return deletionFailure is null
                ? Task.CompletedTask
                : Task.FromException(deletionFailure);
        }
    }
}
