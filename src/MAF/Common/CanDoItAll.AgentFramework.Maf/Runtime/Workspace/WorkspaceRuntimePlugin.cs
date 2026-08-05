using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
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
    IAgentImageAnalysisService imageAnalysisService)
{
        private readonly IWorkspaceCommandExecutionService commandExecutionService = commandExecutionService;
        private readonly IWorkspaceArtifactToolService artifactToolService = artifactToolService;
        private readonly AgentWorkspaceToolAccessSettings accessSettings = AgentWorkspaceToolAccessMetadata.Normalize(accessSettings);
        private readonly WorkspaceRuntimeFileAccessGuard fileAccess = new(workspaceRoot, workspaceScope, accessSettings);
        private readonly ProviderProfile provider = provider;
        private readonly string runtimeModel = runtimeModel;
        private readonly IAgentImageAnalysisService imageAnalysisService = imageAnalysisService;
        private const string ImageAnalysisModelParameterConfigurationJson = """{"modelParameters":{"numPredict":512}}""";
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

        public Task<WorkspaceCommandExecutionResult> DotnetWorkspaceNew(
            string template,
            string name,
            string? parentDirectory = null,
            bool force = false,
            int timeoutSeconds = 300,
            string? targetFramework = null)
        {
            var allowedParentDirectory = PrepareScaffoldPath(parentDirectory);
            return commandExecutionService.DotnetNew(
                template,
                name,
                allowedParentDirectory,
                force,
                timeoutSeconds,
                targetFramework);
        }

        public Task<WorkspaceCommandExecutionResult> RunWorkspacePythonFile(string path, string[]? arguments = null, string? workingDirectory = null, int timeoutSeconds = 300, object? sideEffectManifest = null)
        {
            var allowedPath = PrepareLocalScriptReadPath(path) ?? path;
            var allowedWorkingDirectory = PrepareLocalScriptReadPath(workingDirectory);
            var allowedArguments = PrepareLocalScriptArguments(arguments);
            return commandExecutionService.PythonRunFile(allowedPath, allowedArguments, allowedWorkingDirectory, timeoutSeconds, NormalizeScriptSideEffectManifest(sideEffectManifest));
        }

        public Task<WorkspaceCommandExecutionResult> RunWorkspacePowerShellScript(string path, string[]? arguments = null, string[]? outputPaths = null, string? workingDirectory = null, int timeoutSeconds = 300, object? sideEffectManifest = null)
        {
            var allowedPath = PrepareLocalScriptReadPath(path) ?? path;
            var allowedWorkingDirectory = PrepareLocalScriptReadPath(workingDirectory);
            var allowedOutputPaths = (outputPaths ?? [])
                .Select(outputPath => PrepareFileWritePath(outputPath) ?? outputPath)
                .ToArray();
            var allowedArguments = PrepareLocalScriptArguments(arguments);

            return commandExecutionService.PowerShellRunScript(allowedPath, allowedArguments, allowedOutputPaths, allowedWorkingDirectory, timeoutSeconds, NormalizeScriptSideEffectManifest(sideEffectManifest));
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
            var analysisPrompt = NormalizeImageAnalysisPrompt(prompt);
            var result = await imageAnalysisService.AnalyzeAsync(
                    new AgentImageAnalysisRequest(
                        provider,
                        selectedModel,
                        analysisPrompt,
                        [new AgentImageAnalysisSource(
                            ResolveImageAttachmentName(image.Path),
                            image.ContentType,
                            image.Bytes)],
                        ImageAnalysisModelParameterConfigurationJson))
                .ConfigureAwait(false);

            return CreateImageAnalysisResult(
                succeeded: true,
                image,
                analysisPrompt,
                result.Analysis,
                result.InputTokens,
                result.OutputTokens,
                diagnostics: string.Empty);
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
            var deterministicEvidence = WorkspaceImageSetEvidenceBuilder.Build(images);
            var analysisPrompt = NormalizeImageSetAnalysisPrompt(prompt, images.Count, deterministicEvidence);
            var sources = images
                .Select((image, index) => new AgentImageAnalysisSource(
                    $"{index + 1:D2}-{ResolveImageAttachmentName(image.Path)}",
                    image.ContentType,
                    image.Bytes))
                .ToList();
            var result = await imageAnalysisService.AnalyzeAsync(
                    new AgentImageAnalysisRequest(
                        provider,
                        selectedModel,
                        analysisPrompt,
                        sources,
                        ImageAnalysisModelParameterConfigurationJson))
                .ConfigureAwait(false);

            return CreateImagesAnalysisResult(
                succeeded: true,
                images,
                analysisPrompt,
                result.Analysis,
                result.InputTokens,
                result.OutputTokens,
                diagnostics: string.Empty);
        }

        private string? PrepareFileReadPath(string? path)
            => fileAccess.PrepareFileReadPath(path);

        private string? PrepareFileWritePath(string? path)
            => fileAccess.PrepareFileWritePath(path);

        private string? PrepareGitMutationWorkingDirectory(string? workingDirectory)
        {
            EnsureGitMutationAllowed(workingDirectory);
            return fileAccess.NormalizeAllowedExternalPath(workingDirectory);
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
            return fileAccess.NormalizeAllowedExternalPath(path);
        }

        private string? PrepareScaffoldPath(string? path)
        {
            EnsureScaffoldAllowed(path);
            return fileAccess.NormalizeAllowedExternalPath(path);
        }

        private string? PrepareLocalScriptReadPath(string? path)
        {
            EnsureLocalScriptAllowed(path);
            return fileAccess.NormalizeAllowedExternalPath(path);
        }

        private string[]? PrepareLocalScriptArguments(string[]? arguments)
        {
            return arguments?
                .Select(PrepareLocalScriptArgument)
                .ToArray();
        }

        private string PrepareLocalScriptArgument(string argument)
        {
            if (!WorkspaceScriptArgumentPathParser.TryParse(argument, out var candidate))
            {
                return argument;
            }

            if (WorkspaceScriptArgumentPathParser.ContainsParentTraversal(candidate.Path))
            {
                throw new InvalidOperationException(
                    "Script argument paths cannot contain parent traversal segments ('..'). Use a canonical workspace or external-target path.");
            }

            var normalizedAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(candidate.Path);
            if (WorkspaceScriptArgumentPathParser.IsExternalTargetAliasPath(candidate.Path) &&
                string.IsNullOrWhiteSpace(normalizedAlias))
            {
                throw new InvalidOperationException(
                    "Script argument uses an invalid external-target path. Use a canonical alias without traversal segments.");
            }

            fileAccess.EnsureExternalAliasAllowed(candidate.Path, requireWrite: false);
            var normalizedPath = fileAccess.NormalizeAllowedExternalPath(candidate.Path) ?? candidate.Path;
            return candidate.ReplacePath(normalizedPath);
        }

        private string? PrepareArtifactTransformationReadPath(string? path)
        {
            EnsureArtifactTransformationAllowed(path);
            return fileAccess.NormalizeAllowedExternalPath(path);
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

        private void EnsureGitMutationAllowed(string? path)
        {
            if (!accessSettings.CanManageWorkspacePaths)
            {
                throw new InvalidOperationException($"This agent is not allowed to mutate git state. Effective workspace tool profile '{FormatEffectiveWorkspaceProfile()}' does not grant workspace path management.");
            }

            fileAccess.EnsureFileWriteAllowed(path);
        }

        private void EnsureValidationCommandAllowed(string? path)
        {
            if (!accessSettings.CanRunValidationCommands)
            {
                throw new InvalidOperationException($"This agent is not allowed to run workspace validation commands. Effective workspace tool profile '{FormatEffectiveWorkspaceProfile()}' does not grant validation commands; repair the agent or governed process workspace-tool profile instead of retrying the command.");
            }

            fileAccess.EnsureFileReadAllowed(path);
        }

        private void EnsureScaffoldAllowed(string? path)
        {
            if (!accessSettings.CanScaffoldProjects)
            {
                throw new InvalidOperationException($"This agent is not allowed to scaffold workspace projects. Effective workspace tool profile '{FormatEffectiveWorkspaceProfile()}' does not grant project scaffolding; implementation process steps must use a software-development workspace-tool profile.");
            }

            fileAccess.EnsureFileWriteAllowed(path);
        }

        private void EnsureLocalScriptAllowed(string? path)
        {
            if (!accessSettings.CanRunLocalScripts)
            {
                throw new InvalidOperationException("This agent is not allowed to run local workspace scripts.");
            }

            fileAccess.EnsureFileReadAllowed(path);
        }

        private void EnsureArtifactTransformationAllowed(string? path)
        {
            if (!accessSettings.CanTransformArtifacts)
            {
                throw new InvalidOperationException("This agent is not allowed to transform workspace artifacts.");
            }

            fileAccess.EnsureFileReadAllowed(path);
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

        private string FormatEffectiveWorkspaceProfile()
            => AgentWorkspaceToolAccessProfiles.GetProfileKey(accessSettings.Profile);
}
