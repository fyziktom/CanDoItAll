using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    internal interface IProcessArtifactContentReader
    {
        ProcessArtifactContentReadResult Read(string managedStoragePath);
    }

    internal sealed record ProcessArtifactContentReadResult(
        bool Succeeded,
        string ManagedStoragePath,
        string ResolvedPath,
        string ContentType,
        long ByteLength,
        byte[] ContentBytes,
        string? TextContent,
        string Diagnostic)
    {
        public static ProcessArtifactContentReadResult Failure(
            string managedStoragePath,
            string resolvedPath,
            string contentType,
            long byteLength,
            string diagnostic)
        {
            return new(
                false,
                managedStoragePath,
                resolvedPath,
                contentType,
                byteLength,
                [],
                null,
                diagnostic);
        }

        public static ProcessArtifactContentReadResult Success(
            string managedStoragePath,
            string resolvedPath,
            string contentType,
            byte[] contentBytes,
            string? textContent)
        {
            return new(
                true,
                managedStoragePath,
                resolvedPath,
                contentType,
                contentBytes.LongLength,
                contentBytes,
                textContent,
                string.Empty);
        }
    }

    internal sealed class WorkspaceProcessArtifactContentReader(IWorkspacePathResolver workspacePathResolver) : IProcessArtifactContentReader
    {
        public ProcessArtifactContentReadResult Read(string managedStoragePath)
        {
            if (string.IsNullOrWhiteSpace(managedStoragePath))
            {
                return ProcessArtifactContentReadResult.Failure(
                    managedStoragePath,
                    string.Empty,
                    "application/octet-stream",
                    0,
                    "Managed artifact storage path is empty.");
            }

            var workspaceRoot = Path.GetFullPath(workspacePathResolver.ResolveWorkspaceRoot());
            var candidateFullPath = Path.IsPathRooted(managedStoragePath)
                ? Path.GetFullPath(managedStoragePath)
                : Path.GetFullPath(Path.Combine(
                    workspaceRoot,
                    WorkspaceScopeDescriptor.NormalizeRelativePath(managedStoragePath).Replace('/', Path.DirectorySeparatorChar)));
            var contentType = GuessContentTypeFromPath(candidateFullPath);
            if (!IsWithinWorkspace(workspaceRoot, candidateFullPath))
            {
                return ProcessArtifactContentReadResult.Failure(
                    managedStoragePath,
                    candidateFullPath,
                    contentType,
                    0,
                    "Managed artifact storage path resolves outside the configured workspace root.");
            }

            if (!File.Exists(candidateFullPath))
            {
                return ProcessArtifactContentReadResult.Failure(
                    managedStoragePath,
                    candidateFullPath,
                    contentType,
                    0,
                    "Managed artifact content file was not found.");
            }

            var fileInfo = new FileInfo(candidateFullPath);
            if (fileInfo.Length > MaxProcessArtifactValidationContentBytes)
            {
                return ProcessArtifactContentReadResult.Failure(
                    managedStoragePath,
                    candidateFullPath,
                    contentType,
                    fileInfo.Length,
                    $"Managed artifact content is {fileInfo.Length} bytes, exceeding the validation limit of {MaxProcessArtifactValidationContentBytes} bytes.");
            }

            try
            {
                var contentBytes = File.ReadAllBytes(candidateFullPath);
                var textContent = TryDecodeManagedArtifactTextContent(contentType, candidateFullPath, contentBytes);
                return ProcessArtifactContentReadResult.Success(
                    managedStoragePath,
                    candidateFullPath,
                    contentType,
                    contentBytes,
                    textContent);
            }
            catch (IOException exception)
            {
                return ProcessArtifactContentReadResult.Failure(
                    managedStoragePath,
                    candidateFullPath,
                    contentType,
                    fileInfo.Length,
                    $"Managed artifact content could not be read: {exception.Message}");
            }
            catch (UnauthorizedAccessException exception)
            {
                return ProcessArtifactContentReadResult.Failure(
                    managedStoragePath,
                    candidateFullPath,
                    contentType,
                    fileInfo.Length,
                    $"Managed artifact content could not be read: {exception.Message}");
            }
        }
    }

    internal sealed class StorageBackedProcessArtifactContentReader(
        IWorkspacePathResolver workspacePathResolver,
        IStorageCatalogService storageCatalogService,
        IStorageDriverRegistry storageDriverRegistry) : IProcessArtifactContentReader
    {
        private readonly WorkspaceProcessArtifactContentReader workspaceReader = new(workspacePathResolver);

        public ProcessArtifactContentReadResult Read(string managedStoragePath)
        {
            if (!StorageJson.TryParseReference(managedStoragePath, out var reference) || reference is null)
            {
                return workspaceReader.Read(managedStoragePath);
            }

            if (!reference.StorageId.HasValue)
            {
                return workspaceReader.Read(reference.Locator);
            }

            try
            {
                return ReadStorageReferenceAsync(managedStoragePath, reference).GetAwaiter().GetResult();
            }
            catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException)
            {
                return ProcessArtifactContentReadResult.Failure(
                    managedStoragePath,
                    reference.Locator,
                    string.IsNullOrWhiteSpace(reference.ContentType) ? "application/octet-stream" : reference.ContentType,
                    reference.ContentLength ?? 0,
                    $"Managed storage object could not be read: {exception.Message}");
            }
        }

        private async Task<ProcessArtifactContentReadResult> ReadStorageReferenceAsync(
            string managedStoragePath,
            StorageObjectReference reference)
        {
            var storage = await storageCatalogService.GetAsync(reference.StorageId!.Value, CancellationToken.None);
            if (storage is null)
            {
                return ProcessArtifactContentReadResult.Failure(
                    managedStoragePath,
                    reference.Locator,
                    reference.ContentType,
                    reference.ContentLength ?? 0,
                    $"Storage catalog record '{reference.StorageId.Value:D}' was not found.");
            }

            var driver = storageDriverRegistry.Resolve(storage.ProviderKind);
            await using var stream = await driver.OpenReadAsync(storage, reference, CancellationToken.None);
            if (stream.CanSeek && stream.Length > MaxProcessArtifactValidationContentBytes)
            {
                return ProcessArtifactContentReadResult.Failure(
                    managedStoragePath,
                    reference.Locator,
                    reference.ContentType,
                    stream.Length,
                    $"Managed artifact content is {stream.Length} bytes, exceeding the validation limit of {MaxProcessArtifactValidationContentBytes} bytes.");
            }

            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory, CancellationToken.None);
            if (memory.Length > MaxProcessArtifactValidationContentBytes)
            {
                return ProcessArtifactContentReadResult.Failure(
                    managedStoragePath,
                    reference.Locator,
                    reference.ContentType,
                    memory.Length,
                    $"Managed artifact content is {memory.Length} bytes, exceeding the validation limit of {MaxProcessArtifactValidationContentBytes} bytes.");
            }

            var contentBytes = memory.ToArray();
            var contentType = string.IsNullOrWhiteSpace(reference.ContentType)
                ? GuessContentTypeFromPath(reference.Locator)
                : reference.ContentType;
            var textContent = TryDecodeManagedArtifactTextContent(contentType, reference.Locator, contentBytes);
            return ProcessArtifactContentReadResult.Success(
                managedStoragePath,
                reference.Locator,
                contentType,
                contentBytes,
                textContent);
        }
    }
}
