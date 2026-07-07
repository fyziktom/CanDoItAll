using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class WorkspaceRuntimePlugin(
    IWorkspaceCommandExecutionService commandExecutionService,
    IWorkspaceArtifactToolService artifactToolService,
    string workspaceRoot,
    WorkspaceScopeDescriptor workspaceScope,
    AgentWorkspaceToolAccessSettings accessSettings,
    ProviderProfile provider,
    string runtimeModel,
    IMafProviderRuntimeGateway providerRuntimeGateway)
{
    private static readonly ProviderProfileService ProviderFeatureService = new();

        private readonly IWorkspaceCommandExecutionService commandExecutionService = commandExecutionService;
        private readonly IWorkspaceArtifactToolService artifactToolService = artifactToolService;
        private readonly string workspaceRoot = Path.GetFullPath(workspaceRoot);
        private readonly string workspaceRootWithSeparator = EnsureTrailingSeparator(Path.GetFullPath(workspaceRoot));
        private readonly WorkspaceScopeDescriptor workspaceScope = workspaceScope;
        private readonly AgentWorkspaceToolAccessSettings accessSettings = AgentWorkspaceToolAccessMetadata.Normalize(accessSettings);
        private readonly ProviderProfile provider = provider;
        private readonly string runtimeModel = runtimeModel;
        private readonly IMafProviderRuntimeGateway providerRuntimeGateway = providerRuntimeGateway;
        private const string ImageAnalysisModelParameterConfigurationJson = """{"modelParameters":{"numPredict":512,"think":false}}""";
        private static readonly JsonSerializerOptions ScriptManifestJsonOptions = CreateScriptManifestJsonOptions();

        public WorkspaceCommandExecutionResult GetWorkspaceExecutionBoundary()
            => commandExecutionService.GetExecutionBoundary();

        public Task<WorkspaceCommandExecutionResult> GitWorkspaceStatus(bool includeBranch = true, string? workingDirectory = null, int timeoutSeconds = 30)
        {
            var allowedWorkingDirectory = PrepareFileReadPath(workingDirectory);
            return commandExecutionService.GitStatus(includeBranch, allowedWorkingDirectory, timeoutSeconds);
        }

        public Task<WorkspaceCommandExecutionResult> GitWorkspaceDiff(string? path = null, bool nameOnly = false, string? workingDirectory = null, int timeoutSeconds = 30)
        {
            var allowedPath = PrepareFileReadPath(path);
            var allowedWorkingDirectory = PrepareFileReadPath(workingDirectory);
            return commandExecutionService.GitDiff(allowedPath, nameOnly, allowedWorkingDirectory, timeoutSeconds);
        }

        public Task<WorkspaceCommandExecutionResult> GitWorkspaceLog(int count = 10, string? workingDirectory = null, int timeoutSeconds = 30)
        {
            var allowedWorkingDirectory = PrepareFileReadPath(workingDirectory);
            return commandExecutionService.GitLog(count, allowedWorkingDirectory, timeoutSeconds);
        }

        public Task<WorkspaceCommandExecutionResult> GitWorkspaceShow(string revision, string? workingDirectory = null, int timeoutSeconds = 30)
        {
            var allowedWorkingDirectory = PrepareFileReadPath(workingDirectory);
            return commandExecutionService.GitShow(revision, allowedWorkingDirectory, timeoutSeconds);
        }

        public Task<WorkspaceCommandExecutionResult> GitWorkspaceAdd(string[]? paths, string? workingDirectory = null, int timeoutSeconds = 30)
        {
            var allowedPaths = PrepareGitMutationPaths(paths);
            var allowedWorkingDirectory = PrepareGitMutationWorkingDirectory(workingDirectory);
            return commandExecutionService.GitAdd(allowedPaths, allowedWorkingDirectory, timeoutSeconds);
        }

        public Task<WorkspaceCommandExecutionResult> GitWorkspaceUnstage(string[]? paths, string? workingDirectory = null, int timeoutSeconds = 30)
        {
            var allowedPaths = PrepareGitMutationPaths(paths);
            var allowedWorkingDirectory = PrepareGitMutationWorkingDirectory(workingDirectory);
            return commandExecutionService.GitUnstage(allowedPaths, allowedWorkingDirectory, timeoutSeconds);
        }

        public Task<WorkspaceCommandExecutionResult> GitWorkspaceCommit(string message, string? workingDirectory = null, int timeoutSeconds = 30)
        {
            var allowedWorkingDirectory = PrepareGitMutationWorkingDirectory(workingDirectory);
            return commandExecutionService.GitCommit(message, allowedWorkingDirectory, timeoutSeconds);
        }

        public Task<WorkspaceCommandExecutionResult> GitWorkspaceBranchCreate(string branchName, string? workingDirectory = null, int timeoutSeconds = 30)
        {
            var allowedWorkingDirectory = PrepareGitMutationWorkingDirectory(workingDirectory);
            return commandExecutionService.GitBranchCreate(branchName, allowedWorkingDirectory, timeoutSeconds);
        }

        public Task<WorkspaceCommandExecutionResult> GitWorkspaceSwitch(string branchName, string? workingDirectory = null, int timeoutSeconds = 30)
        {
            var allowedWorkingDirectory = PrepareGitMutationWorkingDirectory(workingDirectory);
            return commandExecutionService.GitSwitch(branchName, allowedWorkingDirectory, timeoutSeconds);
        }

        public Task<WorkspaceCommandExecutionResult> DotnetWorkspaceRestore(string? targetPath = null, string? workingDirectory = null, int timeoutSeconds = 600)
        {
            var allowedTargetPath = PrepareValidationCommandPath(targetPath);
            var allowedWorkingDirectory = PrepareValidationCommandPath(workingDirectory);
            return commandExecutionService.DotnetRestore(allowedTargetPath, allowedWorkingDirectory, timeoutSeconds);
        }

        public Task<WorkspaceCommandExecutionResult> DotnetWorkspaceBuild(string? targetPath = null, string configuration = "Debug", bool noRestore = false, string? workingDirectory = null, int timeoutSeconds = 600)
        {
            var allowedTargetPath = PrepareValidationCommandPath(targetPath);
            var allowedWorkingDirectory = PrepareValidationCommandPath(workingDirectory);
            return commandExecutionService.DotnetBuild(allowedTargetPath, configuration, noRestore, allowedWorkingDirectory, timeoutSeconds);
        }

        public Task<WorkspaceCommandExecutionResult> DotnetWorkspaceTest(string? targetPath = null, string configuration = "Debug", string? filter = null, bool noBuild = false, bool noRestore = false, string? workingDirectory = null, int timeoutSeconds = 300)
        {
            var allowedTargetPath = PrepareValidationCommandPath(targetPath);
            var allowedWorkingDirectory = PrepareValidationCommandPath(workingDirectory);
            return commandExecutionService.DotnetTest(allowedTargetPath, configuration, filter, noBuild, noRestore, allowedWorkingDirectory, timeoutSeconds);
        }

        public Task<WorkspaceCommandExecutionResult> DotnetWorkspaceRun(string targetPath, string? url = null, string configuration = "Debug", bool noBuild = true, bool waitForHttp = true, string? workingDirectory = null, int startupTimeoutSeconds = 45, int timeoutSeconds = 120, bool keepAlive = false, WorkspaceProcessLifetimeScope lifetimeScope = WorkspaceProcessLifetimeScope.ExecutionRun)
        {
            var allowedTargetPath = PrepareValidationCommandPath(targetPath) ?? targetPath;
            var allowedWorkingDirectory = PrepareValidationCommandPath(workingDirectory);
            return commandExecutionService.DotnetRun(allowedTargetPath, url, configuration, noBuild, waitForHttp, allowedWorkingDirectory, startupTimeoutSeconds, timeoutSeconds, keepAlive, lifetimeScope);
        }

        public Task<WorkspaceCommandExecutionResult> DotnetWorkspaceStop(string startupReceiptPath, int timeoutSeconds = 30)
        {
            var allowedStartupReceiptPath = PrepareValidationCommandPath(startupReceiptPath) ?? startupReceiptPath;
            return commandExecutionService.DotnetStop(allowedStartupReceiptPath, timeoutSeconds);
        }

        public Task<WorkspaceCommandExecutionResult> DotnetWorkspaceNew(string template, string name, string? parentDirectory = null, bool force = false, int timeoutSeconds = 300)
        {
            var allowedParentDirectory = PrepareScaffoldPath(parentDirectory, name);
            return commandExecutionService.DotnetNew(template, name, allowedParentDirectory, force, timeoutSeconds);
        }

        public Task<WorkspaceCommandExecutionResult> RunWorkspacePythonFile(string path, string[]? arguments = null, string? workingDirectory = null, int timeoutSeconds = 300, object? sideEffectManifest = null)
        {
            var allowedPath = PrepareLocalScriptReadPath(path) ?? path;
            var allowedWorkingDirectory = PrepareLocalScriptReadPath(workingDirectory);
            return commandExecutionService.PythonRunFile(allowedPath, arguments, allowedWorkingDirectory, timeoutSeconds, NormalizeScriptSideEffectManifest(sideEffectManifest));
        }

        public Task<WorkspaceCommandExecutionResult> RunWorkspacePowerShellScript(string path, string[]? arguments = null, string[]? outputPaths = null, string? workingDirectory = null, int timeoutSeconds = 300, object? sideEffectManifest = null)
        {
            var allowedPath = PrepareLocalScriptReadPath(path) ?? path;
            var allowedWorkingDirectory = PrepareLocalScriptReadPath(workingDirectory);
            var allowedOutputPaths = (outputPaths ?? [])
                .Select(outputPath => PrepareFileWritePath(outputPath) ?? outputPath)
                .ToArray();

            return commandExecutionService.PowerShellRunScript(allowedPath, arguments, allowedOutputPaths, allowedWorkingDirectory, timeoutSeconds, NormalizeScriptSideEffectManifest(sideEffectManifest));
        }

        public Task<WorkspaceDocumentConversionResult> ConvertDocumentToMarkdown(string path, string? outputPath = null, int previewCharacters = 4000)
        {
            var allowedPath = PrepareArtifactTransformationReadPath(path) ?? path;
            var allowedOutputPath = PrepareArtifactTransformationWritePath(outputPath);
            return artifactToolService.ConvertDocumentToMarkdown(allowedPath, allowedOutputPath, previewCharacters);
        }

        public Task<WorkspaceSpreadsheetInspectionResult> InspectSpreadsheetFile(string path, int maxRows = 8, int maxColumns = 8, int previewCharacters = 4000)
        {
            var allowedPath = PrepareFileReadPath(path) ?? path;
            return artifactToolService.InspectSpreadsheetFile(allowedPath, maxRows, maxColumns, previewCharacters);
        }

        public Task<WorkspaceImageInspectionResult> InspectImageFile(string path)
        {
            var allowedPath = PrepareFileReadPath(path) ?? path;
            return artifactToolService.InspectImageFile(allowedPath);
        }

        public async Task<WorkspaceImageAnalysisResult> AnalyzeImageFile(string path, string prompt)
        {
            var allowedPath = PrepareArtifactTransformationReadPath(path) ?? path;
            var image = await artifactToolService.ReadImageFile(allowedPath).ConfigureAwait(false);
            if (!image.Succeeded)
            {
                return CreateImageAnalysisResult(
                    succeeded: false,
                    image,
                    NormalizeImageAnalysisPrompt(prompt),
                    analysis: string.Empty,
                    inputTokens: 0,
                    outputTokens: 0,
                    diagnostics: image.Diagnostics);
            }

            var selectedModel = ResolveImageAnalysisModel();
            var featureMatrix = ProviderFeatureService.ResolveFeatureMatrixForModel(provider, selectedModel);
            if (!featureMatrix.SupportsVision)
            {
                return CreateImageAnalysisResult(
                    succeeded: false,
                    image,
                    NormalizeImageAnalysisPrompt(prompt),
                    analysis: string.Empty,
                    inputTokens: 0,
                    outputTokens: 0,
                    diagnostics: $"Provider '{provider.Name}' model '{selectedModel}' does not support vision/image input.");
            }

            var analysisPrompt = NormalizeImageAnalysisPrompt(prompt);
            try
            {
                var result = await providerRuntimeGateway.RunProviderImageChatAsync(
                        provider,
                        new ProviderTestChatRequest(
                            selectedModel,
                            string.Empty,
                            [],
                            analysisPrompt),
                        selectedModel,
                        [new ProviderChatAttachment(
                            ResolveImageAttachmentName(image.Path),
                            image.ContentType,
                            image.Bytes)],
                        ImageAnalysisModelParameterConfigurationJson)
                    .ConfigureAwait(false);

                return CreateImageAnalysisResult(
                    succeeded: true,
                    image,
                    analysisPrompt,
                    result.ResponseText,
                    result.InputTokens,
                    result.OutputTokens,
                    diagnostics: string.Empty);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                return CreateImageAnalysisResult(
                    succeeded: false,
                    image,
                    analysisPrompt,
                    analysis: string.Empty,
                    inputTokens: 0,
                    outputTokens: 0,
                    diagnostics: exception.Message);
            }
        }

        public async Task<WorkspaceImagesAnalysisResult> AnalyzeImageFiles(string[] paths, string prompt)
        {
            var normalizedPaths = (paths ?? [])
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => path.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (normalizedPaths.Length == 0)
            {
                return CreateImagesAnalysisResult(
                    succeeded: false,
                    [],
                    NormalizeImageSetAnalysisPrompt(prompt, normalizedPaths.Length, string.Empty),
                    analysis: string.Empty,
                    inputTokens: 0,
                    outputTokens: 0,
                    diagnostics: "At least one image path is required.");
            }

            if (normalizedPaths.Length > 8)
            {
                return CreateImagesAnalysisResult(
                    succeeded: false,
                    [],
                    NormalizeImageSetAnalysisPrompt(prompt, normalizedPaths.Length, string.Empty),
                    analysis: string.Empty,
                    inputTokens: 0,
                    outputTokens: 0,
                    diagnostics: "Image set analysis accepts at most 8 image paths per call.");
            }

            var images = new List<WorkspaceImageContentResult>(normalizedPaths.Length);
            foreach (var path in normalizedPaths)
            {
                var allowedPath = PrepareArtifactTransformationReadPath(path) ?? path;
                var image = await artifactToolService
                    .ReadImageFile(allowedPath, operationName: "workspace_analyze_images")
                    .ConfigureAwait(false);
                images.Add(image);
                if (!image.Succeeded)
                {
                    return CreateImagesAnalysisResult(
                        succeeded: false,
                        images,
                        NormalizeImageSetAnalysisPrompt(prompt, normalizedPaths.Length, string.Empty),
                        analysis: string.Empty,
                        inputTokens: 0,
                        outputTokens: 0,
                        diagnostics: image.Diagnostics);
                }
            }

            var selectedModel = ResolveImageAnalysisModel();
            var featureMatrix = ProviderFeatureService.ResolveFeatureMatrixForModel(provider, selectedModel);
            if (!featureMatrix.SupportsVision)
            {
                return CreateImagesAnalysisResult(
                    succeeded: false,
                    images,
                    NormalizeImageSetAnalysisPrompt(prompt, normalizedPaths.Length, string.Empty),
                    analysis: string.Empty,
                    inputTokens: 0,
                    outputTokens: 0,
                    diagnostics: $"Provider '{provider.Name}' model '{selectedModel}' does not support vision/image input.");
            }

            var deterministicEvidence = WorkspaceImageSetEvidenceBuilder.Build(images);
            var analysisPrompt = NormalizeImageSetAnalysisPrompt(prompt, images.Count, deterministicEvidence);
            try
            {
                var attachments = images
                    .Select((image, index) => new ProviderChatAttachment(
                        $"{index + 1:D2}-{ResolveImageAttachmentName(image.Path)}",
                        image.ContentType,
                        image.Bytes))
                    .ToList();
                var result = await providerRuntimeGateway.RunProviderImageChatAsync(
                        provider,
                        new ProviderTestChatRequest(
                            selectedModel,
                            string.Empty,
                            [],
                            analysisPrompt),
                        selectedModel,
                        attachments,
                        ImageAnalysisModelParameterConfigurationJson)
                    .ConfigureAwait(false);

                return CreateImagesAnalysisResult(
                    succeeded: true,
                    images,
                    analysisPrompt,
                    result.ResponseText,
                    result.InputTokens,
                    result.OutputTokens,
                    diagnostics: string.Empty);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                return CreateImagesAnalysisResult(
                    succeeded: false,
                    images,
                    analysisPrompt,
                    analysis: string.Empty,
                    inputTokens: 0,
                    outputTokens: 0,
                    diagnostics: exception.Message);
            }
        }

        private string? PrepareFileReadPath(string? path)
        {
            EnsureFileReadAllowed(path);
            return NormalizeRecoverableCurrentRunArtifactPath(NormalizeAllowedExternalPathForWorkspaceTools(path));
        }

        private string? PrepareFileWritePath(string? path)
        {
            EnsureFileWriteAllowed(path);
            return NormalizeAllowedExternalPathForWorkspaceTools(path);
        }

        private string? PrepareGitMutationWorkingDirectory(string? workingDirectory)
        {
            EnsureGitMutationAllowed(workingDirectory);
            return NormalizeAllowedExternalPathForWorkspaceTools(workingDirectory);
        }

        private string[] PrepareGitMutationPaths(string[]? paths)
        {
            EnsureGitMutationAllowed(null);
            return (paths ?? [])
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => PrepareFileWritePath(path) ?? path)
                .ToArray();
        }

        private string? PrepareValidationCommandPath(string? path)
        {
            EnsureValidationCommandAllowed(path);
            return NormalizeAllowedExternalPathForWorkspaceTools(path);
        }

        private string? PrepareScaffoldPath(string? path, string? scaffoldName)
        {
            EnsureScaffoldAllowed(path, scaffoldName);
            return NormalizeAllowedExternalPathForWorkspaceTools(path);
        }

        private string? PrepareLocalScriptReadPath(string? path)
        {
            EnsureLocalScriptAllowed(path);
            return NormalizeAllowedExternalPathForWorkspaceTools(path);
        }

        private string? PrepareArtifactTransformationReadPath(string? path)
        {
            EnsureArtifactTransformationAllowed(path);
            return NormalizeAllowedExternalPathForWorkspaceTools(path);
        }

        private string? PrepareArtifactTransformationWritePath(string? path)
        {
            EnsureArtifactTransformationAllowed(path);
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            return PrepareFileWritePath(path);
        }

        private void EnsureFileReadAllowed(string? path)
        {
            if (!accessSettings.CanReadFiles && !accessSettings.CanWriteFiles)
            {
                throw new InvalidOperationException("This agent is not allowed to read workspace files.");
            }

            EnsureExternalAliasAllowed(path, requireWrite: false);
        }

        private void EnsureFileWriteAllowed(string? path)
        {
            if (!accessSettings.CanWriteFiles)
            {
                throw new InvalidOperationException("This agent is not allowed to write workspace files.");
            }

            EnsureExternalAliasAllowed(path, requireWrite: true);
        }

        private void EnsureGitMutationAllowed(string? path)
        {
            if (!accessSettings.CanManageWorkspacePaths)
            {
                throw new InvalidOperationException($"This agent is not allowed to mutate git state. Effective workspace tool profile '{FormatEffectiveWorkspaceProfile()}' does not grant workspace path management.");
            }

            EnsureFileWriteAllowed(path);
        }

        private void EnsureValidationCommandAllowed(string? path)
        {
            if (!accessSettings.CanRunValidationCommands)
            {
                throw new InvalidOperationException($"This agent is not allowed to run workspace validation commands. Effective workspace tool profile '{FormatEffectiveWorkspaceProfile()}' does not grant validation commands; repair the agent or governed process workspace-tool profile instead of retrying the command.");
            }

            EnsureFileReadAllowed(path);
        }

        private void EnsureScaffoldAllowed(string? path, string? scaffoldName)
        {
            if (!accessSettings.CanScaffoldProjects)
            {
                throw new InvalidOperationException($"This agent is not allowed to scaffold workspace projects. Effective workspace tool profile '{FormatEffectiveWorkspaceProfile()}' does not grant project scaffolding; implementation process steps must use a software-development workspace-tool profile.");
            }

            if (IsAllowedScaffoldParentAlias(path, scaffoldName))
            {
                return;
            }

            EnsureFileWriteAllowed(path);
        }

        private void EnsureLocalScriptAllowed(string? path)
        {
            if (!accessSettings.CanRunLocalScripts)
            {
                throw new InvalidOperationException("This agent is not allowed to run local workspace scripts.");
            }

            EnsureFileReadAllowed(path);
        }

        private void EnsureArtifactTransformationAllowed(string? path)
        {
            if (!accessSettings.CanTransformArtifacts)
            {
                throw new InvalidOperationException("This agent is not allowed to transform workspace artifacts.");
            }

            EnsureFileReadAllowed(path);
        }

        private WorkspaceImageAnalysisResult CreateImageAnalysisResult(
            bool succeeded,
            WorkspaceImageContentResult image,
            string prompt,
            string analysis,
            int inputTokens,
            int outputTokens,
            string diagnostics)
        {
            var message = succeeded
                ? $"Analyzed image '{image.Path}' with provider '{provider.Name}' model '{ResolveImageAnalysisModel()}'."
                : string.IsNullOrWhiteSpace(diagnostics)
                    ? $"Image analysis failed for '{image.Path}'."
                    : diagnostics;

            return new WorkspaceImageAnalysisResult(
                Succeeded: succeeded,
                Message: message,
                Receipt: image.Receipt,
                Path: image.Path,
                Prompt: prompt,
                ProviderName: provider.Name,
                Model: ResolveImageAnalysisModel(),
                Analysis: analysis,
                InputTokens: inputTokens,
                OutputTokens: outputTokens,
                Diagnostics: diagnostics);
        }

        private WorkspaceImagesAnalysisResult CreateImagesAnalysisResult(
            bool succeeded,
            IReadOnlyList<WorkspaceImageContentResult> images,
            string prompt,
            string analysis,
            int inputTokens,
            int outputTokens,
            string diagnostics)
        {
            var paths = images
                .Select(image => image.Path)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .ToList();
            var message = succeeded
                ? $"Analyzed {images.Count:N0} image(s) with provider '{provider.Name}' model '{ResolveImageAnalysisModel()}'."
                : string.IsNullOrWhiteSpace(diagnostics)
                    ? "Image set analysis failed."
                    : diagnostics;

            return new WorkspaceImagesAnalysisResult(
                Succeeded: succeeded,
                Message: message,
                Images: images.Select(CreateAnalyzedImageRecord).ToList(),
                Paths: paths,
                Prompt: prompt,
                ProviderName: provider.Name,
                Model: ResolveImageAnalysisModel(),
                Analysis: analysis,
                InputTokens: inputTokens,
                OutputTokens: outputTokens,
                Diagnostics: diagnostics);
        }

        private static WorkspaceAnalyzedImageRecord CreateAnalyzedImageRecord(
            WorkspaceImageContentResult image)
        {
            return new WorkspaceAnalyzedImageRecord(
                image.Path,
                image.Format,
                image.ContentType,
                image.SizeBytes,
                image.Width,
                image.Height,
                image.Message,
                image.Diagnostics);
        }

        private string ResolveImageAnalysisModel()
            => WorkspaceImageAnalysisModelResolver.ResolveProviderImageAnalysisModel(provider, runtimeModel);

        private static string NormalizeImageAnalysisPrompt(string prompt)
            => WorkspaceImageAnalysisPromptNormalizer.NormalizeSingleImagePrompt(prompt);

        private static string NormalizeImageSetAnalysisPrompt(
            string prompt,
            int imageCount,
            string deterministicEvidence)
            => WorkspaceImageAnalysisPromptNormalizer.NormalizeImageSetPrompt(prompt, imageCount, deterministicEvidence);

        private static string ResolveImageAttachmentName(string path)
        {
            var fileName = Path.GetFileName(path);
            return string.IsNullOrWhiteSpace(fileName) ? "image" : fileName;
        }

        private void EnsureExternalAliasAllowed(string? path, bool requireWrite)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var normalizedAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(path);
            if (string.IsNullOrWhiteSpace(normalizedAlias))
            {
                return;
            }

            if (IsManagedWorkspaceAbsolutePath(path))
            {
                return;
            }

            var readOnlyAliases = ResolveReadOnlyExternalTargetAliases();
            if (requireWrite &&
                IsExternalTargetAliasAllowed(normalizedAlias, readOnlyAliases))
            {
                throw new InvalidOperationException(
                    $"External workspace path '{normalizedAlias}' is read-only for this run.");
            }

            var allowedAliases = ResolveAllowedExternalTargetAliases();
            var isAllowedForRead = IsExternalTargetAliasAllowed(normalizedAlias, allowedAliases) ||
                                   IsExternalTargetAliasAllowed(normalizedAlias, readOnlyAliases);
            if (!isAllowedForRead)
            {
                throw new InvalidOperationException(
                    $"External workspace path '{normalizedAlias}' is not in this agent's allowed external workspace roots.");
            }

            if (requireWrite &&
                !IsExternalTargetAliasAllowed(normalizedAlias, allowedAliases))
            {
                throw new InvalidOperationException(
                    $"External workspace path '{normalizedAlias}' is read-only for this run.");
            }
        }

        private string? NormalizeAllowedExternalPathForWorkspaceTools(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) ||
                IsManagedWorkspaceAbsolutePath(path))
            {
                return path;
            }

            var normalizedAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(path);
            return string.IsNullOrWhiteSpace(normalizedAlias)
                ? path
                : normalizedAlias;
        }

        private string? NormalizeRecoverableCurrentRunArtifactPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return path;
            }

            var auditScope = WorkspaceExecutionAuditContext.Current;
            var currentRunId = auditScope?.ProcessRunId;
            var currentWorkspaceScope = auditScope?.ContextWorkspaceScope ?? workspaceScope;
            return WorkspaceProcessRunArtifactPath.TryBuildRecoverableCurrentRunPath(
                path,
                currentRunId,
                currentWorkspaceScope,
                out var currentRunPath)
                ? currentRunPath
                : path;
        }

        private IReadOnlyList<string> ResolveAllowedExternalTargetAliases()
        {
            var auditScope = WorkspaceExecutionAuditContext.Current;
            if (auditScope is not null &&
                (auditScope.AllowedExternalTargetAliases.Count > 0 ||
                 auditScope.ReadOnlyExternalTargetAliases.Count > 0))
            {
                return auditScope.AllowedExternalTargetAliases
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }

            return accessSettings.AllowedExternalTargetAliases
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static IReadOnlyList<string> ResolveReadOnlyExternalTargetAliases()
        {
            return WorkspaceExecutionAuditContext.Current?.ReadOnlyExternalTargetAliases
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray() ?? [];
        }

        private static bool IsExternalTargetAliasAllowed(
            string normalizedAlias,
            IReadOnlyList<string> allowedAliases)
        {
            return AgentWorkspaceToolAccessMetadata.IsExternalTargetAliasAllowed(
                normalizedAlias,
                allowedAliases);
        }

        private bool IsAllowedScaffoldParentAlias(string? parentDirectory, string? scaffoldName)
        {
            var normalizedParentDirectory = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(parentDirectory);
            if (string.IsNullOrWhiteSpace(normalizedParentDirectory))
            {
                return false;
            }

            var normalizedScaffoldName = NormalizeExternalTargetChildName(scaffoldName);
            if (string.IsNullOrWhiteSpace(normalizedScaffoldName))
            {
                return false;
            }

            var requestedScaffoldRoot = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(
                $"{normalizedParentDirectory}/{normalizedScaffoldName}");
            if (string.IsNullOrWhiteSpace(requestedScaffoldRoot))
            {
                return false;
            }

            return ResolveAllowedExternalTargetAliases()
                .Select(AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias)
                .Where(alias => !string.IsNullOrWhiteSpace(alias))
                .Any(alias => string.Equals(requestedScaffoldRoot, alias, StringComparison.OrdinalIgnoreCase));
        }

        private static string NormalizeExternalTargetChildName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            var normalizedName = name
                .Replace('\\', '/')
                .Trim()
                .Trim('`', '"', '\'')
                .Trim('/');
            var segments = normalizedName.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return segments.Any(segment => string.Equals(segment, ".", StringComparison.Ordinal) || string.Equals(segment, "..", StringComparison.Ordinal))
                ? string.Empty
                : normalizedName;
        }

        private static string? NormalizeScriptSideEffectManifest(object? sideEffectManifest)
        {
            return sideEffectManifest switch
            {
                null => null,
                string value => value,
                JsonElement { ValueKind: JsonValueKind.Null } => null,
                JsonElement { ValueKind: JsonValueKind.Undefined } => null,
                JsonElement { ValueKind: JsonValueKind.String } value => value.GetString(),
                JsonElement value => value.GetRawText(),
                _ => JsonSerializer.Serialize(sideEffectManifest, sideEffectManifest.GetType(), ScriptManifestJsonOptions)
            };
        }

        private static JsonSerializerOptions CreateScriptManifestJsonOptions()
        {
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            options.Converters.Add(new JsonStringEnumConverter());
            return options;
        }

        private bool IsManagedWorkspaceAbsolutePath(string path)
        {
            try
            {
                if (!Path.IsPathRooted(path))
                {
                    return false;
                }

                var fullPath = Path.GetFullPath(path);
                return string.Equals(fullPath, workspaceRoot, StringComparison.OrdinalIgnoreCase) ||
                       fullPath.StartsWith(workspaceRootWithSeparator, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string EnsureTrailingSeparator(string path)
        {
            return path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
                ? path
                : path + Path.DirectorySeparatorChar;
        }

        private string FormatEffectiveWorkspaceProfile()
            => AgentWorkspaceToolAccessProfiles.GetProfileKey(accessSettings.Profile);
}
