using System.IO.Compression;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkspaceFileServiceTests
{
    [Fact]
    public async Task AgentChatAttachmentStagingService_stage_image_writes_scoped_artifact_and_returns_logical_path()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.ChatAttachmentTests.{Guid.NewGuid():N}");
        var scope = WorkspaceScopeDescriptor.Organization("test-org");
        Directory.CreateDirectory(workspaceRoot);

        try
        {
            var pathResolution = new WorkspacePathResolutionService(workspaceRoot, scope);
            var service = new AgentChatAttachmentStagingService(pathResolution);
            var bytes = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };

            var result = await service.StageImageAsync(
                "qa screenshot.PNG",
                "image/png",
                bytes.Length,
                new MemoryStream(bytes));

            Assert.StartsWith("artifacts/chat-attachments/", result.RelativePath, StringComparison.Ordinal);
            Assert.EndsWith(".png", result.RelativePath, StringComparison.Ordinal);
            Assert.Equal("image/png", result.ContentType);

            var resolved = pathResolution.ResolveFilePath(result.RelativePath, allowMissing: false);
            Assert.True(File.Exists(resolved.FullPath));
            Assert.Contains(
                Path.Combine("artifacts", "scopes", "organization", "test-org", "chat-attachments"),
                resolved.FullPath,
                StringComparison.OrdinalIgnoreCase);
            Assert.Equal(bytes, await File.ReadAllBytesAsync(resolved.FullPath));
        }
        finally
        {
            try
            {
                Directory.Delete(workspaceRoot, recursive: true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public async Task AgentChatAttachmentStagingService_stage_image_rejects_non_image_extension()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.ChatAttachmentTests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspaceRoot);

        try
        {
            var service = new AgentChatAttachmentStagingService(new WorkspacePathResolutionService(workspaceRoot));

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.StageImageAsync(
                "notes.txt",
                "text/plain",
                4,
                new MemoryStream([1, 2, 3, 4])));

            Assert.Contains("Only PNG, JPEG, GIF, and WebP", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            try
            {
                Directory.Delete(workspaceRoot, recursive: true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public async Task AgentChatAttachmentStagingService_rejects_stream_larger_than_declared_limit_and_removes_partial_file()
    {
        var workspaceRoot = Path.Combine(
            Path.GetTempPath(),
            $"CanDoItAll.ChatAttachmentTests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspaceRoot);

        try
        {
            var service = new AgentChatAttachmentStagingService(
                new WorkspacePathResolutionService(workspaceRoot));
            var oversizedContent = new byte[
                checked((int)AgentChatAttachmentStagingService.MaxImageAttachmentBytes + 1)];

            var exception =
                await Assert.ThrowsAsync<
                    AgentRuntimeInputAttachmentSizeException>(
                    () => service.StageImageAsync(
                        "deceptive.png",
                        "image/png",
                        AgentChatAttachmentStagingService
                            .MaxImageAttachmentBytes,
                        new MemoryStream(oversizedContent)));

            Assert.Equal(
                AgentChatAttachmentStagingService.MaxImageAttachmentBytes,
                exception.MaximumBytes);
            Assert.Empty(
                Directory.GetFiles(
                    workspaceRoot,
                    "*",
                    SearchOption.AllDirectories));
        }
        finally
        {
            try
            {
                Directory.Delete(workspaceRoot, recursive: true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public async Task AgentRuntimeInputAttachmentPolicy_accepts_exact_limit_and_rejects_limit_plus_one()
    {
        var exactBytes = new byte[
            checked((int)AgentRuntimeInputAttachmentPolicy.MaximumImageBytes)];
        await using var exactOutput = new MemoryStream();

        var copied = await AgentRuntimeInputAttachmentPolicy.CopyBoundedAsync(
            new MemoryStream(exactBytes),
            exactOutput,
            "exact.png",
            AgentRuntimeInputAttachmentPolicy.MaximumImageBytes,
            CancellationToken.None);

        Assert.Equal(exactBytes.LongLength, copied);
        Assert.Equal(exactBytes.LongLength, exactOutput.Length);

        var oversizedBytes = new byte[
            checked((int)AgentRuntimeInputAttachmentPolicy.MaximumImageBytes + 1)];
        await using var oversizedOutput = new MemoryStream();
        var exception =
            await Assert.ThrowsAsync<
                AgentRuntimeInputAttachmentSizeException>(
                () => AgentRuntimeInputAttachmentPolicy.CopyBoundedAsync(
                    new MemoryStream(oversizedBytes),
                    oversizedOutput,
                    "oversized.png",
                    AgentRuntimeInputAttachmentPolicy.MaximumImageBytes,
                    CancellationToken.None));

        Assert.Equal("oversized.png", exception.SourcePath);
        Assert.Equal(
            AgentRuntimeInputAttachmentPolicy.MaximumImageBytes + 1,
            exception.ObservedBytes);
        Assert.True(
            oversizedOutput.Length <=
            AgentRuntimeInputAttachmentPolicy.MaximumImageBytes);
    }

    [Fact]
    public void WriteTextFile_registers_showcase_deliverable_as_execution_artifact()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.WorkspaceFileServiceTests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspaceRoot);

        try
        {
            var service = new WorkspaceFileService(workspaceRoot);

            var result = service.WriteTextFile(
                "showcases/blazor-ssr-workflow/app/SimpleWorkflowApp/Program.cs",
                "var builder = WebApplication.CreateBuilder(args);");

            Assert.True(result.Succeeded);
            Assert.Contains(
                result.Receipt.ArtifactReferences,
                item =>
                    string.Equals(item.Zone, "generated-output", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(
                        item.RelativePath,
                        "showcases/blazor-ssr-workflow/app/SimpleWorkflowApp/Program.cs",
                        StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            try
            {
                Directory.Delete(workspaceRoot, recursive: true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public void WriteTextFile_tags_audit_receipt_with_runtime_tool_provider_ownership()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.WorkspaceFileServiceTests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspaceRoot);
        var run = CreateRun();

        try
        {
            var service = new WorkspaceFileService(workspaceRoot);
            using (WorkspaceExecutionAuditContext.BeginScope(run))
            using (AgentRuntimeToolOwnershipContext.BeginScope(new AgentRuntimeToolOwnership(
                       "processes.runtime-tools",
                       "Processes runtime tools",
                       "workspace_write_file")))
            {
                var result = service.WriteTextFile("artifacts/process-runs/run-001/provider-proof.md", "proof");

                Assert.True(result.Succeeded);
            }

            var receiptPath = Assert.Single(Directory.GetFiles(
                Path.Combine(workspaceRoot, "data", "execution", "runs", run.Id.ToString("N"), "audit", "receipts"),
                "*.json"));
            var receipt = JsonSerializer.Deserialize<ToolExecutionReceiptRecord>(
                              File.ReadAllText(receiptPath),
                              new JsonSerializerOptions(JsonSerializerDefaults.Web))
                          ?? throw new InvalidOperationException("Receipt JSON did not deserialize.");

            Assert.Equal("workspace_write_file", receipt.ToolName);
            Assert.Equal("processes.runtime-tools", receipt.RuntimeToolProviderKey);
            Assert.Equal("Processes runtime tools", receipt.RuntimeToolProviderName);
        }
        finally
        {
            try
            {
                Directory.Delete(workspaceRoot, recursive: true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public void ToolExecutionReceiptRecord_deserializes_legacy_receipts_with_empty_runtime_provider_ownership()
    {
        var receiptId = Guid.NewGuid();
        var executionRunId = Guid.NewGuid();
        var timestamp = DateTimeOffset.UtcNow;
        var json = $$"""
            {
              "id": "{{receiptId}}",
              "executionRunId": "{{executionRunId}}",
              "toolFamily": "workspace-file",
              "toolName": "workspace_read_file",
              "riskClass": "ReadOnlyWorkspace",
              "approvalMode": "NotRequired",
              "isolationGuarantee": "Workspace file service.",
              "requestSummary": "README.md",
              "workingDirectory": ".",
              "exitSummary": "Succeeded",
              "startedAtUtc": "{{timestamp:O}}",
              "completedAtUtc": "{{timestamp:O}}"
            }
            """;

        var receipt = JsonSerializer.Deserialize<ToolExecutionReceiptRecord>(
                          json,
                          new JsonSerializerOptions(JsonSerializerDefaults.Web))
                      ?? throw new InvalidOperationException("Receipt JSON did not deserialize.");

        Assert.Equal(receiptId, receipt.Id);
        Assert.Equal(executionRunId, receipt.ExecutionRunId);
        Assert.Equal(string.Empty, receipt.RuntimeToolProviderKey);
        Assert.Equal(string.Empty, receipt.RuntimeToolProviderName);
    }

    [Fact]
    public void ZipPath_does_not_delete_existing_destination_when_source_validation_fails()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.WorkspaceFileServiceTests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(workspaceRoot, "archives"));
        var existingArchivePath = Path.Combine(workspaceRoot, "archives", "docs.zip");
        File.WriteAllText(existingArchivePath, "existing archive");

        try
        {
            var service = new WorkspaceFileService(workspaceRoot);

            var result = service.ZipPath("missing", "archives/docs.zip", overwrite: true);

            Assert.False(result.Succeeded);
            Assert.Equal("existing archive", File.ReadAllText(existingArchivePath));
        }
        finally
        {
            try
            {
                Directory.Delete(workspaceRoot, recursive: true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public void UnzipArchive_does_not_extract_partial_files_when_overwrite_conflict_exists()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.WorkspaceFileServiceTests.{Guid.NewGuid():N}");
        var sourceDirectory = Path.Combine(workspaceRoot, "source");
        var destinationDirectory = Path.Combine(workspaceRoot, "expanded");
        Directory.CreateDirectory(sourceDirectory);
        Directory.CreateDirectory(destinationDirectory);
        File.WriteAllText(Path.Combine(sourceDirectory, "a.txt"), "new a");
        File.WriteAllText(Path.Combine(sourceDirectory, "b.txt"), "new b");
        File.WriteAllText(Path.Combine(destinationDirectory, "b.txt"), "existing b");
        ZipFile.CreateFromDirectory(sourceDirectory, Path.Combine(workspaceRoot, "archive.zip"));

        try
        {
            var service = new WorkspaceFileService(workspaceRoot);

            var result = service.UnzipArchive("archive.zip", "expanded", overwrite: false);

            Assert.False(result.Succeeded);
            Assert.False(File.Exists(Path.Combine(destinationDirectory, "a.txt")));
            Assert.Equal("existing b", File.ReadAllText(Path.Combine(destinationDirectory, "b.txt")));
        }
        finally
        {
            try
            {
                Directory.Delete(workspaceRoot, recursive: true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public void UnzipArchive_rejects_existing_reparse_point_in_destination_tree()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.WorkspaceFileServiceTests.{Guid.NewGuid():N}");
        var sourceDirectory = Path.Combine(workspaceRoot, "source", "linked");
        var destinationDirectory = Path.Combine(workspaceRoot, "expanded");
        var outsideDirectory = Path.Combine(Path.GetTempPath(), $"CanDoItAll.WorkspaceFileServiceOutside.{Guid.NewGuid():N}");
        Directory.CreateDirectory(sourceDirectory);
        Directory.CreateDirectory(destinationDirectory);
        Directory.CreateDirectory(outsideDirectory);
        File.WriteAllText(Path.Combine(sourceDirectory, "escaped.txt"), "must stay contained");
        ZipFile.CreateFromDirectory(Path.Combine(workspaceRoot, "source"), Path.Combine(workspaceRoot, "archive.zip"));
        Directory.CreateSymbolicLink(Path.Combine(destinationDirectory, "linked"), outsideDirectory);

        try
        {
            var service = new WorkspaceFileService(workspaceRoot);

            var result = service.UnzipArchive("archive.zip", "expanded", overwrite: true);

            Assert.False(result.Succeeded);
            Assert.Contains("reparse-point traversal", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.False(File.Exists(Path.Combine(outsideDirectory, "escaped.txt")));
        }
        finally
        {
            try
            {
                Directory.Delete(Path.Combine(destinationDirectory, "linked"));
                Directory.Delete(workspaceRoot, recursive: true);
            }
            catch
            {
            }

            try
            {
                Directory.Delete(outsideDirectory, recursive: true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public void WriteTextFile_scans_managed_project_authority_once_without_climbing_parent_directories()
    {
        var parentRoot = Path.Combine(
            Path.GetTempPath(),
            $"CanDoItAll.ProjectPlacementBoundary.{Guid.NewGuid():N}");
        var workspaceRoot = Path.Combine(parentRoot, "workspace");
        var sourceRoot = Path.Combine(workspaceRoot, "src");
        Directory.CreateDirectory(sourceRoot);
        var scannedRoots = new List<string>();

        try
        {
            var pathPolicy = new WorkspacePathPolicy(workspaceRoot);
            var service = new WorkspaceFileMutationService(
                pathPolicy,
                new WorkspaceFileReceiptWriter(workspaceRoot),
                root =>
                {
                    scannedRoots.Add(Path.GetFullPath(root));
                    return [];
                });

            var result = service.WriteTextFile("src/Feature.cs", "internal sealed class Feature {}", overwrite: true);

            Assert.True(result.Succeeded, result.Message);
            Assert.Equal([Path.GetFullPath(workspaceRoot)], scannedRoots);
            Assert.DoesNotContain(
                scannedRoots,
                root => string.Equals(root, Path.GetFullPath(parentRoot), StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            try
            {
                Directory.Delete(parentRoot, recursive: true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public void WriteTextFile_project_file_guard_does_not_inspect_parent_of_managed_authority()
    {
        var parentRoot = Path.Combine(
            Path.GetTempPath(),
            $"CanDoItAll.ProjectFilePlacementBoundary.{Guid.NewGuid():N}");
        var workspaceRoot = Path.Combine(parentRoot, "workspace");
        Directory.CreateDirectory(Path.Combine(workspaceRoot, "nested"));
        File.WriteAllText(Path.Combine(parentRoot, "Parent.csproj"), "<Project />");
        var scannedRoots = new List<string>();

        try
        {
            var pathPolicy = new WorkspacePathPolicy(workspaceRoot);
            var service = new WorkspaceFileMutationService(
                pathPolicy,
                new WorkspaceFileReceiptWriter(workspaceRoot),
                root =>
                {
                    scannedRoots.Add(Path.GetFullPath(root));
                    return [];
                });

            var result = service.WriteTextFile(
                "nested/NewProject.csproj",
                "<Project Sdk=\"Microsoft.NET.Sdk\" />",
                overwrite: true);

            Assert.True(result.Succeeded, result.Message);
            Assert.Equal([Path.GetFullPath(workspaceRoot)], scannedRoots);
        }
        finally
        {
            try
            {
                Directory.Delete(parentRoot, recursive: true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public void WriteTextFile_scans_explicit_external_authority_once_without_climbing_its_parent()
    {
        var parentRoot = Path.Combine(
            Path.GetTempPath(),
            $"CanDoItAll.ExternalProjectPlacementBoundary.{Guid.NewGuid():N}");
        var workspaceRoot = Path.Combine(parentRoot, "workspace");
        var authorityRoot = Path.Combine(parentRoot, "calculator");
        var sourceRoot = Path.Combine(authorityRoot, "src");
        Directory.CreateDirectory(workspaceRoot);
        Directory.CreateDirectory(sourceRoot);
        var targetPath = Path.Combine(sourceRoot, "Feature.cs");
        var targetAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(targetPath);
        var authorityAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(authorityRoot);
        var scannedRoots = new List<string>();

        try
        {
            Assert.NotNull(targetAlias);
            Assert.NotNull(authorityAlias);
            var pathPolicy = new WorkspacePathPolicy(workspaceRoot);
            var service = new WorkspaceFileMutationService(
                pathPolicy,
                new WorkspaceFileReceiptWriter(workspaceRoot),
                root =>
                {
                    scannedRoots.Add(Path.GetFullPath(root));
                    return [];
                });

            var result = service.WriteTextFile(
                targetAlias!,
                "internal sealed class Feature {}",
                overwrite: true,
                authorityRootPath: authorityAlias);

            Assert.True(result.Succeeded, result.Message);
            Assert.Equal([Path.GetFullPath(authorityRoot)], scannedRoots);
            Assert.DoesNotContain(
                scannedRoots,
                root => string.Equals(root, Path.GetFullPath(parentRoot), StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            try
            {
                Directory.Delete(parentRoot, recursive: true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public void WriteTextFile_rejects_nested_project_file_without_creating_it()
    {
        using var workspace = new TemporaryWorkspace();
        File.WriteAllText(Path.Combine(workspace.RootPath, "Host.csproj"), "<Project />");

        var result = workspace.Files.WriteTextFile(
            "nested/Child.csproj",
            "<Project />");

        Assert.False(result.Succeeded);
        Assert.Contains("nested project", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Path.Combine(workspace.RootPath, "nested", "Child.csproj")));
    }

    [Fact]
    public void AppendTextFile_rejects_nested_project_creation_and_forbidden_shim_content()
    {
        using var workspace = new TemporaryWorkspace();
        File.WriteAllText(Path.Combine(workspace.RootPath, "Host.csproj"), "<Project />");
        var safeFilePath = Path.Combine(workspace.RootPath, "Existing.cs");
        const string originalContent = "namespace Product;\n";
        File.WriteAllText(safeFilePath, originalContent);

        var nestedProject = workspace.Files.AppendTextFile(
            "nested/Child.csproj",
            "<Project />");
        var forbiddenShim = workspace.Files.AppendTextFile(
            "Existing.cs",
            "namespace Xunit; public sealed class FactAttribute : Attribute;");

        Assert.False(nestedProject.Succeeded);
        Assert.False(forbiddenShim.Succeeded);
        Assert.False(File.Exists(Path.Combine(workspace.RootPath, "nested", "Child.csproj")));
        Assert.Equal(originalContent, File.ReadAllText(safeFilePath));
    }

    [Fact]
    public void WriteTextFile_commit_failure_restores_existing_file_and_cleans_transaction_artifacts()
    {
        using var workspace = new TemporaryWorkspace();
        var targetPath = Path.Combine(workspace.RootPath, "target.txt");
        File.WriteAllText(targetPath, "old content");
        var mutationService = CreateMutationServiceWithFailingFileCommit(workspace.RootPath);

        Assert.Throws<IOException>(() =>
            mutationService.WriteTextFile("target.txt", "new content", overwrite: true));

        Assert.Equal("old content", File.ReadAllText(targetPath));
        Assert.Empty(FindTransactionArtifacts(workspace.RootPath));
    }

    [Fact]
    public void AppendTextFile_commit_failure_restores_existing_file_and_cleans_transaction_artifacts()
    {
        using var workspace = new TemporaryWorkspace();
        var targetPath = Path.Combine(workspace.RootPath, "target.txt");
        File.WriteAllText(targetPath, "old content");
        var mutationService = CreateMutationServiceWithFailingFileCommit(workspace.RootPath);

        Assert.Throws<IOException>(() =>
            mutationService.AppendTextFile("target.txt", " appended content"));

        Assert.Equal("old content", File.ReadAllText(targetPath));
        Assert.Empty(FindTransactionArtifacts(workspace.RootPath));
    }

    [Fact]
    public void WriteTextFile_new_target_commit_failure_does_not_delete_concurrent_writer_file()
    {
        using var workspace = new TemporaryWorkspace();
        var targetPath = Path.Combine(workspace.RootPath, "target.txt");
        var mutationService = new WorkspaceFileMutationService(
            new WorkspacePathPolicy(workspace.RootPath),
            new WorkspaceFileReceiptWriter(workspace.RootPath),
            _ => [],
            WorkspaceFileMutationService.DeleteDirectoryTree,
            commitStagedFile: request =>
            {
                File.WriteAllText(request.DestinationPath, "concurrent content");
                return WorkspaceStagedFileCommitAttempt.NotCommitted(
                    new IOException("Injected concurrent destination race."));
            });

        Assert.Throws<IOException>(() =>
            mutationService.WriteTextFile("target.txt", "new content", overwrite: true));

        Assert.Equal("concurrent content", File.ReadAllText(targetPath));
        Assert.Empty(FindTransactionArtifacts(workspace.RootPath));
    }

    [Fact]
    public void AppendTextFile_rejects_result_over_byte_budget_without_changing_large_file()
    {
        using var workspace = new TemporaryWorkspace();
        var targetPath = Path.Combine(workspace.RootPath, "large.txt");
        var originalContent = new string('a', WorkspaceFileLimits.MaxTextMutationBytes);
        File.WriteAllText(targetPath, originalContent);

        var result = workspace.Files.AppendTextFile("large.txt", "x");

        Assert.False(result.Succeeded);
        Assert.Equal("Failed", result.Receipt.Outcome);
        Assert.Contains("text-mutation limit", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(originalContent, File.ReadAllText(targetPath));
        Assert.Empty(FindTransactionArtifacts(workspace.RootPath));
    }

    [Fact]
    public void AppendTextFile_rejects_concurrent_change_before_replace_without_lost_update()
    {
        using var workspace = new TemporaryWorkspace();
        var targetPath = Path.Combine(workspace.RootPath, "Target.cs");
        File.WriteAllText(targetPath, "internal sealed class Original {}\n");
        var mutationService = new WorkspaceFileMutationService(
            new WorkspacePathPolicy(workspace.RootPath),
            new WorkspaceFileReceiptWriter(workspace.RootPath),
            _ =>
            {
                File.WriteAllText(targetPath, "internal sealed class Concurrent {}\n");
                return [];
            });

        var result = mutationService.AppendTextFile(
            "Target.cs",
            "internal sealed class Appended {}\n");

        Assert.False(result.Succeeded);
        Assert.Equal("Failed", result.Receipt.Outcome);
        Assert.Contains("could not be verified unchanged", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("internal sealed class Concurrent {}\n", File.ReadAllText(targetPath));
        Assert.Empty(FindTransactionArtifacts(workspace.RootPath));
    }

    [Fact]
    public void CopyPath_rejects_nested_project_in_directory_tree_without_destination_residue()
    {
        using var workspace = new TemporaryWorkspace();
        var sourceRoot = Directory.CreateDirectory(Path.Combine(workspace.RootPath, "seed")).FullName;
        File.WriteAllText(Path.Combine(sourceRoot, "Host.csproj"), "<Project />");
        Directory.CreateDirectory(Path.Combine(sourceRoot, "nested"));
        File.WriteAllText(Path.Combine(sourceRoot, "nested", "Child.csproj"), "<Project />");

        var result = workspace.Files.CopyPath("seed", "copied");

        Assert.False(result.Succeeded);
        Assert.True(File.Exists(Path.Combine(sourceRoot, "nested", "Child.csproj")));
        Assert.False(Directory.Exists(Path.Combine(workspace.RootPath, "copied")));
        Assert.Empty(FindTransactionArtifacts(workspace.RootPath));
    }

    [Fact]
    public void CopyPath_project_layout_verification_denial_preserves_source_and_existing_destination()
    {
        using var workspace = new TemporaryWorkspace();
        var sourceRoot = Directory.CreateDirectory(Path.Combine(workspace.RootPath, "source")).FullName;
        var sourcePath = Path.Combine(sourceRoot, "Feature.cs");
        File.WriteAllText(sourcePath, "internal sealed class NewFeature {}");
        var destinationRoot = Directory.CreateDirectory(
            Path.Combine(workspace.RootPath, "destination")).FullName;
        var destinationPath = Path.Combine(destinationRoot, "Feature.cs");
        File.WriteAllText(destinationPath, "internal sealed class ExistingFeature {}");
        var mutationService = new WorkspaceFileMutationService(
            new WorkspacePathPolicy(workspace.RootPath),
            new WorkspaceFileReceiptWriter(workspace.RootPath),
            _ => throw new UnauthorizedAccessException("native access detail"));

        var result = mutationService.CopyPath(
            "source/Feature.cs",
            "destination/Feature.cs",
            overwrite: true);

        Assert.False(result.Succeeded);
        Assert.Contains("Cannot verify the destination project layout", result.Message, StringComparison.Ordinal);
        Assert.Contains("no content changed", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("native access detail", result.Message, StringComparison.Ordinal);
        Assert.Equal("internal sealed class NewFeature {}", File.ReadAllText(sourcePath));
        Assert.Equal("internal sealed class ExistingFeature {}", File.ReadAllText(destinationPath));
        Assert.Empty(FindTransactionArtifacts(workspace.RootPath));
    }

    [Fact]
    public void MovePath_rejects_forbidden_shim_in_directory_tree_and_preserves_source()
    {
        using var workspace = new TemporaryWorkspace();
        var sourceRoot = Directory.CreateDirectory(Path.Combine(workspace.RootPath, "seed")).FullName;
        var shimPath = Path.Combine(sourceRoot, "TestingFallback.cs");
        File.WriteAllText(
            shimPath,
            "namespace NUnit.Framework; public sealed class TestAttribute : Attribute;");

        var result = workspace.Files.MovePath("seed", "moved");

        Assert.False(result.Succeeded);
        Assert.True(File.Exists(shimPath));
        Assert.False(Directory.Exists(Path.Combine(workspace.RootPath, "moved")));
        Assert.Empty(FindTransactionArtifacts(workspace.RootPath));
    }

    [Fact]
    public void UnzipArchive_rejects_nested_project_entries_without_partial_extraction()
    {
        using var workspace = new TemporaryWorkspace();
        var archivePath = Path.Combine(workspace.RootPath, "nested-project.zip");
        using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            WriteArchiveEntry(archive, "Host.csproj", "<Project />");
            WriteArchiveEntry(archive, "nested/Child.csproj", "<Project />");
        }

        var result = workspace.Files.UnzipArchive("nested-project.zip", "expanded");

        Assert.False(result.Succeeded);
        Assert.Contains("nested project", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(Path.Combine(workspace.RootPath, "expanded")));
        Assert.Empty(FindTransactionArtifacts(workspace.RootPath));
    }

    [Fact]
    public void CopyPath_late_source_read_failure_preserves_existing_destination_and_cleans_staging()
    {
        using var workspace = new TemporaryWorkspace();
        var sourceRoot = Directory.CreateDirectory(Path.Combine(workspace.RootPath, "source")).FullName;
        var lockedSourcePath = Path.Combine(sourceRoot, "locked.txt");
        File.WriteAllText(lockedSourcePath, "new content");
        var destinationRoot = Directory.CreateDirectory(Path.Combine(workspace.RootPath, "destination")).FullName;
        var existingPath = Path.Combine(destinationRoot, "existing.txt");
        File.WriteAllText(existingPath, "old content");
        using var lockedSource = new FileStream(
            lockedSourcePath,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None);

        Assert.ThrowsAny<IOException>(() =>
            workspace.Files.CopyPath("source", "destination", overwrite: true));

        Assert.Equal("old content", File.ReadAllText(existingPath));
        Assert.Empty(FindTransactionArtifacts(workspace.RootPath));
    }

    [Fact]
    public void ZipPath_late_source_read_failure_preserves_existing_archive_and_cleans_staging()
    {
        using var workspace = new TemporaryWorkspace();
        var sourceRoot = Directory.CreateDirectory(Path.Combine(workspace.RootPath, "source")).FullName;
        var lockedSourcePath = Path.Combine(sourceRoot, "locked.txt");
        File.WriteAllText(lockedSourcePath, "new content");
        var archivePath = Path.Combine(workspace.RootPath, "archive.zip");
        var oldArchive = new byte[] { 1, 2, 3, 4 };
        File.WriteAllBytes(archivePath, oldArchive);
        using var lockedSource = new FileStream(
            lockedSourcePath,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None);

        Assert.ThrowsAny<IOException>(() =>
            workspace.Files.ZipPath("source", "archive.zip", overwrite: true));

        Assert.Equal(oldArchive, File.ReadAllBytes(archivePath));
        Assert.Empty(FindTransactionArtifacts(workspace.RootPath));
    }

    [Fact]
    public void UnzipArchive_late_destination_read_failure_preserves_existing_tree_and_cleans_staging()
    {
        using var workspace = new TemporaryWorkspace();
        var archivePath = Path.Combine(workspace.RootPath, "payload.zip");
        using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            WriteArchiveEntry(archive, "payload.txt", "new content");
        }

        var destinationRoot = Directory.CreateDirectory(Path.Combine(workspace.RootPath, "destination")).FullName;
        var existingPath = Path.Combine(destinationRoot, "existing.txt");
        File.WriteAllText(existingPath, "old content");
        using var lockedDestination = new FileStream(
            existingPath,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None);

        Assert.ThrowsAny<IOException>(() =>
            workspace.Files.UnzipArchive("payload.zip", "destination", overwrite: true));

        lockedDestination.Dispose();
        Assert.Equal("old content", File.ReadAllText(existingPath));
        Assert.False(File.Exists(Path.Combine(destinationRoot, "payload.txt")));
        Assert.Empty(FindTransactionArtifacts(workspace.RootPath));
    }

    [Fact]
    public void CommitStagedDirectory_partial_backup_cleanup_failure_keeps_committed_destination()
    {
        using var workspace = new TemporaryWorkspace();
        var destinationRoot = Directory.CreateDirectory(
            Path.Combine(workspace.RootPath, "destination")).FullName;
        File.WriteAllText(Path.Combine(destinationRoot, "old-a.txt"), "old a");
        File.WriteAllText(Path.Combine(destinationRoot, "old-b.txt"), "old b");
        var stagingRoot = Directory.CreateDirectory(
            Path.Combine(workspace.RootPath, ".destination.candoitall-stage-test")).FullName;
        File.WriteAllText(Path.Combine(stagingRoot, "new.txt"), "new content");

        var result = WorkspaceFileMutationService.CommitStagedDirectory(
            stagingRoot,
            destinationRoot,
            overwrite: true,
            PartiallyDeleteThenFail);

        Assert.True(result.HasCleanupWarning);
        Assert.Contains(".candoitall-backup-", result.RetainedCleanupArtifact, StringComparison.Ordinal);
        Assert.Equal("new content", File.ReadAllText(Path.Combine(destinationRoot, "new.txt")));
        Assert.False(File.Exists(Path.Combine(destinationRoot, "old-a.txt")));
        Assert.Single(
            FindTransactionArtifacts(workspace.RootPath),
            path => Path.GetFileName(path).Contains(".candoitall-backup-", StringComparison.Ordinal));

        static void PartiallyDeleteThenFail(string backupPath)
        {
            File.Delete(Directory.EnumerateFiles(backupPath).First());
            throw new IOException("Injected backup cleanup failure.");
        }
    }

    [Fact]
    public void MovePath_partial_backup_cleanup_failure_keeps_moved_source_committed()
    {
        using var workspace = new TemporaryWorkspace();
        var sourceRoot = Directory.CreateDirectory(Path.Combine(workspace.RootPath, "source")).FullName;
        File.WriteAllText(Path.Combine(sourceRoot, "new.txt"), "new content");
        var destinationRoot = Directory.CreateDirectory(
            Path.Combine(workspace.RootPath, "destination")).FullName;
        File.WriteAllText(Path.Combine(destinationRoot, "old-a.txt"), "old a");
        File.WriteAllText(Path.Combine(destinationRoot, "old-b.txt"), "old b");
        var mutationService = new WorkspaceFileMutationService(
            new WorkspacePathPolicy(workspace.RootPath),
            new WorkspaceFileReceiptWriter(workspace.RootPath),
            _ => [],
            PartiallyDeleteThenFail);

        var result = mutationService.MovePath("source", "destination", overwrite: true);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal("Succeeded", result.Receipt.Outcome);
        Assert.Contains("committed successfully", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("retained cleanup artifact", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(sourceRoot));
        Assert.Equal("new content", File.ReadAllText(Path.Combine(destinationRoot, "new.txt")));
        Assert.False(File.Exists(Path.Combine(destinationRoot, "old-a.txt")));
        Assert.Single(
            FindTransactionArtifacts(workspace.RootPath),
            path => Path.GetFileName(path).Contains(".candoitall-backup-", StringComparison.Ordinal));

        static void PartiallyDeleteThenFail(string backupPath)
        {
            File.Delete(Directory.EnumerateFiles(backupPath).First());
            throw new IOException("Injected backup cleanup failure.");
        }
    }

    [Fact]
    public void RecursiveDelete_partial_tombstone_cleanup_failure_remains_a_successful_logical_delete()
    {
        using var workspace = new TemporaryWorkspace();
        var targetRoot = Directory.CreateDirectory(
            Path.Combine(workspace.RootPath, "obsolete")).FullName;
        File.WriteAllText(Path.Combine(targetRoot, "old-a.txt"), "old a");
        File.WriteAllText(Path.Combine(targetRoot, "old-b.txt"), "old b");
        var mutationService = new WorkspaceFileMutationService(
            new WorkspacePathPolicy(workspace.RootPath),
            new WorkspaceFileReceiptWriter(workspace.RootPath),
            _ => [],
            PartiallyDeleteThenFail);

        var result = mutationService.DeletePath("obsolete", recursive: true);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal("Succeeded", result.Receipt.Outcome);
        Assert.Contains("committed successfully", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("retained cleanup artifact", result.Receipt.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(targetRoot));
        Assert.Single(
            FindTransactionArtifacts(workspace.RootPath),
            path => Path.GetFileName(path).Contains(".candoitall-tombstone-", StringComparison.Ordinal));

        static void PartiallyDeleteThenFail(string tombstonePath)
        {
            File.Delete(Directory.EnumerateFiles(tombstonePath).First());
            throw new IOException("Injected tombstone cleanup failure.");
        }
    }

    [Fact]
    public void RecursiveDelete_rename_failure_rolls_back_directory_and_cleans_tombstone()
    {
        using var workspace = new TemporaryWorkspace();
        var targetRoot = Directory.CreateDirectory(
            Path.Combine(workspace.RootPath, "obsolete")).FullName;
        var targetFile = Path.Combine(targetRoot, "keep.txt");
        File.WriteAllText(targetFile, "keep content");

        Assert.Throws<IOException>(() =>
            WorkspaceFileMutationService.CommitRecursiveDirectoryDelete(
                targetRoot,
                (sourcePath, tombstonePath) =>
                {
                    Directory.Move(sourcePath, tombstonePath);
                    throw new IOException("Injected rename acknowledgement failure.");
                }));

        Assert.Equal("keep content", File.ReadAllText(targetFile));
        Assert.Empty(FindTransactionArtifacts(workspace.RootPath));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MovePath_cross_volume_preflight_rejects_file_and_directory_without_mutation(
        bool moveDirectory)
    {
        using var workspace = new TemporaryWorkspace();
        var sourcePath = Path.Combine(workspace.RootPath, "source");
        var destinationPath = Path.Combine(workspace.RootPath, "destination");
        if (moveDirectory)
        {
            Directory.CreateDirectory(sourcePath);
            Directory.CreateDirectory(destinationPath);
            File.WriteAllText(Path.Combine(sourcePath, "content.txt"), "source content");
            File.WriteAllText(Path.Combine(destinationPath, "content.txt"), "destination content");
        }
        else
        {
            File.WriteAllText(sourcePath, "source content");
            File.WriteAllText(destinationPath, "destination content");
        }

        var mutationService = new WorkspaceFileMutationService(
            new WorkspacePathPolicy(workspace.RootPath),
            new WorkspaceFileReceiptWriter(workspace.RootPath),
            _ => [],
            WorkspaceFileMutationService.DeleteDirectoryTree,
            resolveVolumeRoot: path => path.Contains("source", StringComparison.OrdinalIgnoreCase)
                ? "source-volume"
                : "destination-volume");

        var result = mutationService.MovePath("source", "destination", overwrite: true);

        Assert.False(result.Succeeded);
        Assert.Equal("Failed", result.Receipt.Outcome);
        Assert.Contains("across filesystem volumes", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("workspace_copy_path", result.Message, StringComparison.Ordinal);
        Assert.Contains("verify", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("workspace_delete_path", result.Message, StringComparison.Ordinal);
        Assert.True(moveDirectory ? Directory.Exists(sourcePath) : File.Exists(sourcePath));
        Assert.Equal(
            "source content",
            File.ReadAllText(moveDirectory ? Path.Combine(sourcePath, "content.txt") : sourcePath));
        Assert.Equal(
            "destination content",
            File.ReadAllText(moveDirectory ? Path.Combine(destinationPath, "content.txt") : destinationPath));
        Assert.Empty(FindTransactionArtifacts(workspace.RootPath));
    }

    private static void WriteArchiveEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(content);
    }

    private static WorkspaceFileMutationService CreateMutationServiceWithFailingFileCommit(
        string workspaceRoot)
        => new(
            new WorkspacePathPolicy(workspaceRoot),
            new WorkspaceFileReceiptWriter(workspaceRoot),
            _ => [],
            WorkspaceFileMutationService.DeleteDirectoryTree,
            commitStagedFile: _ => WorkspaceStagedFileCommitAttempt.NotCommitted(
                new IOException("Injected staged-file commit failure.")));

    private static string[] FindTransactionArtifacts(string rootPath)
        => Directory.EnumerateFileSystemEntries(rootPath, "*", SearchOption.TopDirectoryOnly)
            .Where(path =>
                Path.GetFileName(path).Contains(".candoitall-stage-", StringComparison.Ordinal) ||
                Path.GetFileName(path).Contains(".candoitall-backup-", StringComparison.Ordinal) ||
                Path.GetFileName(path).Contains(".candoitall-tombstone-", StringComparison.Ordinal))
            .ToArray();

    private sealed class TemporaryWorkspace : IDisposable
    {
        public TemporaryWorkspace()
        {
            RootPath = Path.Combine(
                Path.GetTempPath(),
                $"CanDoItAll.WorkspacePlacementTests.{Guid.NewGuid():N}");
            Directory.CreateDirectory(RootPath);
            Files = new WorkspaceFileService(RootPath);
        }

        public string RootPath { get; }

        public WorkspaceFileService Files { get; }

        public void Dispose()
        {
            if (!Directory.Exists(RootPath))
            {
                return;
            }

            WorkspaceFileMutationService.DeleteDirectoryTree(RootPath);
        }
    }

    private static ExecutionRunRecord CreateRun()
    {
        var now = DateTimeOffset.UtcNow;
        return new ExecutionRunRecord(
            Id: Guid.NewGuid(),
            AgentId: Guid.NewGuid(),
            ChatSessionId: null,
            Title: "Receipt provider ownership",
            SourceKind: "manual",
            SourceId: "receipt-provider-ownership",
            CorrelationId: "corr-001",
            CausationId: string.Empty,
            RequestedBy: "unit-test",
            RequestedByKind: "system",
            MetadataJson: "{}",
            InputSummary: "Input",
            ResultSummary: string.Empty,
            ProviderName: "Provider",
            Model: "model",
            State: ExecutionState.Running,
            Outcome: null,
            CreatedAtUtc: now,
            UpdatedAtUtc: now,
            StartedAtUtc: now,
            CompletedAtUtc: null,
            RuntimeSessionKey: string.Empty,
            SerializedSessionStateJson: null,
            PendingApprovals: []);
    }
}
