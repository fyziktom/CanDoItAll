using System.Runtime.InteropServices;
using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Unit;

[Collection(AppDbContextModelRegistryTestCollectionNames.Name)]
public sealed class ProjectManagedStorageDeletionTests
{
    [Fact]
    public async Task Planner_preserves_shared_bytes_until_every_binding_is_deleted_then_returns_one_reference()
    {
        await using var dbContext = CreateDbContext();
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
        var planner = CreatePlanner();

        var sharedPlan = await planner.PlanAsync(dbContext, [source.Id, duplicate.Id]);
        var finalPlan = await planner.PlanAsync(dbContext, [source.Id, copy.Id, duplicate.Id]);

        Assert.Empty(sharedPlan.References);
        var selected = Assert.Single(finalPlan.References);
        Assert.Equal(reference, selected);
    }

    [Fact]
    public async Task Planner_rejects_malformed_managed_candidate_and_managed_survivor()
    {
        await using var dbContext = CreateDbContext();
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
        var planner = CreatePlanner();

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
        var service = new ProjectManagedStorageDeletionService(
            new StubStorageDriverRegistry(driver),
            CreatePhysicalIdentityPolicy(),
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
        var service = new ProjectManagedStorageDeletionService(
            new StubStorageDriverRegistry(driver),
            CreatePhysicalIdentityPolicy(),
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
        await using var dbContext = CreateDbContext();
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

        var plan = await CreatePlanner().PlanAsync(
            dbContext,
            [ftpObject.Id, ipfsObject.Id]);

        Assert.Equal(2, plan.References.Count);
        Assert.Contains(plan.References, reference => reference.ProviderKind == StorageProviderKind.Ftp);
        Assert.Contains(plan.References, reference => reference.ProviderKind == StorageProviderKind.Ipfs);
    }

    [Fact]
    public async Task Planner_rejects_managed_path_bound_to_an_unrelated_or_noncanonical_locator()
    {
        await using var dbContext = CreateDbContext();
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
        var planner = CreatePlanner();

        await Assert.ThrowsAsync<ProjectManagedStorageBindingException>(() =>
            planner.PlanAsync(dbContext, [mismatched.Id]));
        await Assert.ThrowsAsync<ProjectManagedStorageBindingException>(() =>
            planner.PlanAsync(dbContext, [dotSegment.Id]));
    }

    [Fact]
    public async Task Planner_normalizes_legacy_windows_media_paths_but_preserves_mixed_bootstrap_references()
    {
        await using var dbContext = CreateDbContext();
        var projectId = Guid.NewGuid();
        var legacy = CreateObject(projectId, "asset:legacy");
        var upgradedCopy = CreateObject(projectId, "asset:copy");
        var storageId = Guid.NewGuid();
        dbContext.Add(new StorageCatalogRecord
        {
            Id = storageId,
            Name = "Bootstrap",
            ProviderKind = StorageProviderKind.FileSystem,
            IsSystemDefault = true
        });
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

        var plan = await CreatePlanner().PlanAsync(
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

        await using var dbContext = CreateDbContext();
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

        var plan = await CreatePlanner().PlanAsync(
            dbContext,
            [candidate.Id]);

        Assert.Empty(plan.References);
    }

    [Fact]
    public async Task Deletion_service_treats_ipfs_as_terminal_even_when_catalog_entry_is_missing()
    {
        var storage = CreateStorage(Guid.NewGuid(), StorageProviderKind.Ipfs);
        var driver = new RecordingStorageDriver(StorageProviderKind.Ipfs, StorageCapability.Read);
        var service = new ProjectManagedStorageDeletionService(
            new StubStorageDriverRegistry(driver),
            CreatePhysicalIdentityPolicy(),
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
        var service = new ProjectManagedStorageDeletionService(
            new StubStorageDriverRegistry(driver),
            CreatePhysicalIdentityPolicy(),
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
        var service = new ProjectManagedStorageDeletionService(
            new StubStorageDriverRegistry(driver),
            CreatePhysicalIdentityPolicy(),
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
        await using var dbContext = CreateDbContext();
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
            CreatePlanner().PlanAsync(dbContext, [asset.Id]));
    }

    [Fact]
    public async Task Planner_uses_physical_root_not_catalog_id_for_survivor_liveness()
    {
        await using var dbContext = CreateDbContext();
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

        var plan = await CreatePlanner().PlanAsync(dbContext, [candidate.Id]);

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

            await using var dbContext = CreateDbContext();
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

            var plan = await CreatePlanner().PlanAsync(dbContext, [candidate.Id]);

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
            var service = new ProjectManagedStorageDeletionService(
                new StubStorageDriverRegistry(driver),
                CreatePhysicalIdentityPolicy(),
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
        await using var dbContext = CreateDbContext();
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

        var plan = await CreatePlanner().PlanAsync(dbContext, [candidate.Id]);

        Assert.Single(plan.References);
    }

    [Fact]
    public async Task Planner_conservatively_preserves_ftp_case_alias_but_detects_base_path_retargeting()
    {
        await using var dbContext = CreateDbContext();
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

        Assert.Empty((await CreatePlanner().PlanAsync(dbContext, [candidate.Id])).References);

        storageA.ConfigJson = "{\"basePath\":\"Origin\"}";
        await dbContext.SaveChangesAsync();
        await Assert.ThrowsAsync<ProjectManagedStorageBindingException>(() =>
            CreatePlanner().PlanAsync(dbContext, [candidate.Id]));
    }

    private static AppDbContext CreateDbContext()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(
        [
            typeof(WorkbenchModuleAssemblyMarker).Assembly
        ]);
        var options = AppDbContextTestOptionsBuilder.Create()
            .UseInMemoryDatabase($"project-storage-deletion-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private static IDbContextFactory<AppDbContext> CreateDbContextFactory(
        params StorageCatalogRecord[] storages)
    {
        AppDbContextModelRegistry.ConfigureAssemblies(
        [
            typeof(WorkbenchModuleAssemblyMarker).Assembly
        ]);
        var options = AppDbContextTestOptionsBuilder.Create()
            .UseInMemoryDatabase($"project-storage-service-{Guid.NewGuid():N}")
            .Options;
        using (var dbContext = new AppDbContext(options))
        {
            dbContext.AddRange(storages);
            dbContext.SaveChanges();
        }

        return new TestDbContextFactory(options);
    }

    private static ProjectManagedStorageDeletionPlanner CreatePlanner()
        => new(CreatePhysicalIdentityPolicy());

    private static ProjectManagedStoragePhysicalIdentityPolicy CreatePhysicalIdentityPolicy()
        => new(new FileSystemStoragePathPolicy(new StubWorkspacePathResolver()));

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
        => new()
        {
            Id = Guid.NewGuid(),
            Name = "Bootstrap",
            ProviderKind = StorageProviderKind.FileSystem,
            IsSystemDefault = true,
            IsEnabled = true,
            EndpointOrRoot = StubWorkspacePathResolver.RootPath,
            CapabilityMask = StorageCapability.Read | StorageCapability.Delete
        };

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
        => new()
        {
            Id = id,
            Name = "Deletion storage",
            ProviderKind = providerKind,
            IsEnabled = true,
            CapabilityMask = StorageCapability.Read | StorageCapability.Delete,
            EndpointOrRoot = "unused"
        };

    private sealed class StubStorageCatalogService(
        StorageCatalogRecord storage,
        bool returnMissing = false) : IStorageCatalogService
    {
        public Task<IReadOnlyList<StorageCatalogRecord>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StorageCatalogRecord>>([storage]);

        public Task<StorageCatalogRecord?> GetAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<StorageCatalogRecord?>(
                !returnMissing && id == storage.Id ? storage : null);

        public Task<StorageCatalogRecord> EnsureBootstrapFileSystemStorageAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(storage);

        public Task<StorageCatalogRecord> SaveAsync(StorageCatalogRecord record, CancellationToken cancellationToken = default)
            => Task.FromResult(record);

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<StorageRoutingRule>> ListRulesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StorageRoutingRule>>([]);

        public Task<StorageRoutingRule> SaveRuleAsync(StorageRoutingRule rule, CancellationToken cancellationToken = default)
            => Task.FromResult(rule);
    }

    private sealed class StubWorkspacePathResolver : IWorkspacePathResolver
    {
        private static readonly string Root = Path.Combine(
            Path.GetTempPath(),
            "candoitall-storage-deletion-tests");

        internal static string RootPath => Root;

        public string ResolveWorkspaceRoot() => Root;

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

    private sealed class TestDbContextFactory(
        DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext()
            => new(options);
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
            DeleteCalls++;
            return deletionFailure is null
                ? Task.CompletedTask
                : Task.FromException(deletionFailure);
        }
    }
}
