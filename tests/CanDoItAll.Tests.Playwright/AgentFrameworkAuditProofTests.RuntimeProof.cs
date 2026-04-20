using System.Text;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Playwright;

public sealed partial class AgentFrameworkAuditProofTests
{
    private async Task WaitForScenarioCompletionAsync(string scenarioId, string requiredArtifactName)
    {
        await using var serviceProvider = await BuildSeedServiceProviderAsync();
        var timeoutAt = DateTimeOffset.UtcNow.AddMinutes(2);
        var lastSnapshot = string.Empty;

        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            await using var scope = serviceProvider.CreateAsyncScope();
            var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
            var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
            var providers = await workspaceService.ListProvidersAsync();
            var provider = providers.Single(item =>
                string.Equals(item.BaseUrl, ScenarioHarnessProviderBaseUrl, StringComparison.OrdinalIgnoreCase));
            var agents = await workspaceService.ListAgentsAsync(includeTemplates: false);
            var scenarioAgent = agents.Single(item =>
                item.ProviderProfileId == provider.Id &&
                string.Equals(item.Name, ScenarioHarnessOperatorName, StringComparison.Ordinal));
            var run = (await workspaceService.ListExecutionRunsAsync(
                new ExecutionRunQuery(
                    AgentId: scenarioAgent.Id,
                    SourceKind: "scenario-harness",
                    SourceId: scenarioId,
                    Take: 3)))
                .FirstOrDefault();
            if (run is null)
            {
                lastSnapshot = "no execution run";
                await Task.Delay(1_000);
                continue;
            }

            var detail = await workspaceService.GetExecutionRunDetailAsync(run.Id);
            var recentLogSummary = string.Join(
                " || ",
                detail.ExecutionLog
                    .OrderByDescending(item => item.CreatedAtUtc)
                    .Take(5)
                    .Select(item => $"{item.State}:{item.Phase}:{item.Message}"));
            lastSnapshot = $"{run.State} / pendingApprovals={run.PendingApprovals.Count} / artifacts={detail.Artifacts.Count} / result={run.ResultSummary} / logs={recentLogSummary}";
            if (run.State == ExecutionState.Completed &&
                run.PendingApprovals.Count == 0 &&
                detail.Artifacts.Any(item =>
                    item.RelativePath.EndsWith(requiredArtifactName, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            await Task.Delay(1_000);
        }

        throw new TimeoutException($"Timed out waiting for scenario '{scenarioId}' to complete. Last snapshot: {lastSnapshot}");
    }

    private async Task WaitForDirectMessageDenialAsync(Guid runId, string expectedMessageFragment)
    {
        await using var serviceProvider = await BuildSeedServiceProviderAsync();
        var timeoutAt = DateTimeOffset.UtcNow.AddMinutes(1);
        var lastSnapshot = string.Empty;

        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            await using var scope = serviceProvider.CreateAsyncScope();
            var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
            var details = await processesService.GetRunDetailsAsync(runId);
            var deniedObservation = details.ConformanceObservations.FirstOrDefault(item =>
                string.Equals(item.Category, "DirectMessagingPolicy", StringComparison.Ordinal) &&
                item.Observation.Contains(expectedMessageFragment, StringComparison.Ordinal));
            var deniedDecision = details.Decisions.FirstOrDefault(item =>
                item.DecisionKind == ProcessDecisionKind.DirectMessage &&
                item.Outcome == ProcessDecisionOutcome.Rejected);
            lastSnapshot = $"decisions={details.Decisions.Count} / observations={details.ConformanceObservations.Count} / threads={details.DirectMessageThreads.Count}";
            if (deniedObservation is not null && deniedDecision is not null)
            {
                return;
            }

            await Task.Delay(500);
        }

        throw new TimeoutException($"Timed out waiting for denied direct-message evidence. Last snapshot: {lastSnapshot}");
    }

    private async Task<CalculatorRunEvidence> WaitForCalculatorReviewGateAsync(
        CalculatorScenarioSeed seed,
        string launchName)
    {
        await using var serviceProvider = await BuildSeedServiceProviderAsync();
        var timeoutAt = DateTimeOffset.UtcNow.AddMinutes(3);
        var lastSnapshot = string.Empty;

        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            await using var scope = serviceProvider.CreateAsyncScope();
            var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
            var runDetailsLoader = scope.ServiceProvider.GetRequiredService<ProcessWorkspaceRunDetailsLoader>();
            var run = (await processesService.ListRunsAsync(seed.DefinitionId, seed.ProjectId))
                .FirstOrDefault(item => string.Equals(item.Name, launchName, StringComparison.Ordinal));
            if (run is not null)
            {
                var details = await runDetailsLoader.LoadAsync(run.Id);
                var generationStep = details.StepRuns.FirstOrDefault(item => item.Title == seed.GenerationStepTitle);
                var handoffStep = details.StepRuns.FirstOrDefault(item => item.Title == seed.HandoffStepTitle);
                var generationExecution = details.ExecutionRuns.FirstOrDefault(item =>
                    item.StepTitle == seed.GenerationStepTitle &&
                    item.Outcome == RunOutcome.Succeeded);
                var generationArtifact = details.ExecutionRuns
                    .SelectMany(item => item.Artifacts)
                    .FirstOrDefault(item => item.RelativePath.EndsWith("/generation-report.md", StringComparison.OrdinalIgnoreCase));
                var projectRoot = TryResolveCalculatorProjectRoot(details.ExecutionRuns) ?? ResolveScenarioCalculatorProjectRoot(run.Id);
                var homePageFile = Path.Combine(projectRoot, "Components", "Pages", "Home.razor");
                var readmeFile = Path.Combine(projectRoot, "README.md");
                var projectFile = Path.Combine(projectRoot, "ScenarioCalculator.csproj");

                var executionArtifactPaths = details.ExecutionRuns
                    .SelectMany(item => item.Artifacts)
                    .Select(item => item.RelativePath)
                    .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                lastSnapshot =
                    $"{run.Status} / generation={generationStep?.Status} / handoff={handoffStep?.Status} / execution={generationExecution?.State} / generationArtifact={(generationArtifact?.RelativePath ?? "<missing>")} / projectFile={File.Exists(projectFile)} / homePage={File.Exists(homePageFile)} / readme={File.Exists(readmeFile)} / executionArtifacts={string.Join(" | ", executionArtifactPaths)}";
                if (generationStep?.Status == ProcessStepRunStatus.Completed &&
                    handoffStep?.Status == ProcessStepRunStatus.Ready &&
                    generationExecution is not null &&
                    generationArtifact is not null &&
                    File.Exists(homePageFile) &&
                    File.Exists(readmeFile) &&
                    File.Exists(projectFile))
                {
                    return new CalculatorRunEvidence(
                        run.Id,
                        run.Status,
                        details.ExecutionRuns.SelectMany(item => item.Artifacts).Select(item => item.RelativePath).ToList(),
                        details.Artifacts.Select(item => item.Title).ToList(),
                        projectRoot,
                        projectFile,
                        homePageFile,
                        readmeFile,
                        details.DirectMessageThreads.FirstOrDefault()?.ThreadId,
                        details.ExecutionRuns.Count);
                }
            }

            await Task.Delay(1_000);
        }

        throw new TimeoutException($"Timed out waiting for the SC11 review gate. Last snapshot: {lastSnapshot}");
    }

    private async Task<CalculatorRunEvidence> WaitForCalculatorRunCompletionAsync(
        CalculatorScenarioSeed seed,
        string launchName)
    {
        await using var serviceProvider = await BuildSeedServiceProviderAsync();
        var timeoutAt = DateTimeOffset.UtcNow.AddMinutes(3);
        var lastSnapshot = string.Empty;

        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            await using var scope = serviceProvider.CreateAsyncScope();
            var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
            var runDetailsLoader = scope.ServiceProvider.GetRequiredService<ProcessWorkspaceRunDetailsLoader>();
            var run = (await processesService.ListRunsAsync(seed.DefinitionId, seed.ProjectId))
                .FirstOrDefault(item => string.Equals(item.Name, launchName, StringComparison.Ordinal));
            if (run is not null)
            {
                var details = await runDetailsLoader.LoadAsync(run.Id);
                var reviewStep = details.StepRuns.FirstOrDefault(item => item.Title == seed.ReviewStepTitle);
                var generationArtifactRecorded = details.Artifacts.Any(item =>
                    string.Equals(item.Title, "generation-report.md", StringComparison.Ordinal));
                var reviewArtifactRecorded = details.Artifacts.Any(item =>
                    string.Equals(item.Title, "review-report.md", StringComparison.Ordinal));
                var projectRoot = TryResolveCalculatorProjectRoot(details.ExecutionRuns) ?? ResolveScenarioCalculatorProjectRoot(run.Id);
                var homePageFile = Path.Combine(projectRoot, "Components", "Pages", "Home.razor");
                var readmeFile = Path.Combine(projectRoot, "README.md");
                var homePageContent = File.Exists(homePageFile)
                    ? await File.ReadAllTextAsync(homePageFile)
                    : string.Empty;
                var readmeContent = File.Exists(readmeFile)
                    ? await File.ReadAllTextAsync(readmeFile)
                    : string.Empty;

                lastSnapshot = $"{run.Status} / review={reviewStep?.Status} / artifacts={details.Artifacts.Count} / executionRuns={details.ExecutionRuns.Count}";
                if (run.Status == ProcessRunStatus.Completed &&
                    reviewStep?.Status == ProcessStepRunStatus.Completed &&
                    generationArtifactRecorded &&
                    reviewArtifactRecorded &&
                    homePageContent.Contains("Add", StringComparison.OrdinalIgnoreCase) &&
                    homePageContent.Contains("Subtract", StringComparison.OrdinalIgnoreCase) &&
                    homePageContent.Contains("Multiply", StringComparison.OrdinalIgnoreCase) &&
                    homePageContent.Contains("Divide", StringComparison.OrdinalIgnoreCase) &&
                    readmeContent.Contains("dotnet run", StringComparison.OrdinalIgnoreCase))
                {
                    return new CalculatorRunEvidence(
                        run.Id,
                        run.Status,
                        details.ExecutionRuns.SelectMany(item => item.Artifacts).Select(item => item.RelativePath).ToList(),
                        details.Artifacts.Select(item => item.Title).ToList(),
                        projectRoot,
                        Path.Combine(projectRoot, "ScenarioCalculator.csproj"),
                        homePageFile,
                        readmeFile,
                        details.DirectMessageThreads.FirstOrDefault()?.ThreadId,
                        details.ExecutionRuns.Count);
                }
            }

            await Task.Delay(1_000);
        }

        throw new TimeoutException($"Timed out waiting for the SC11 calculator run to complete. Last snapshot: {lastSnapshot}");
    }

    private string? TryResolveCalculatorProjectRoot(IReadOnlyList<ProcessExecutionRunViewModel> executionRuns)
    {
        if (string.IsNullOrWhiteSpace(fixture.StorageWorkspaceRoot))
        {
            return null;
        }

        var projectArtifact = executionRuns
            .SelectMany(item => item.Artifacts)
            .Select(item => item.RelativePath)
            .FirstOrDefault(item => item.EndsWith("/ScenarioCalculator.csproj", StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(projectArtifact))
        {
            return null;
        }

        var projectPath = Path.Combine(
            fixture.StorageWorkspaceRoot,
            projectArtifact.Replace('/', Path.DirectorySeparatorChar));
        return Path.GetDirectoryName(projectPath);
    }

    private string BuildCalculatorEvidenceMarkdown(
        CalculatorScenarioSeed seed,
        string launchName,
        CalculatorRunEvidence evidence)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# SC11 Calculator Delivery Evidence")
            .AppendLine()
            .Append("- Project id: `")
            .Append(seed.ProjectId.ToString("D"))
            .AppendLine("`")
            .Append("- Definition id: `")
            .Append(seed.DefinitionId.ToString("D"))
            .AppendLine("`")
            .Append("- Run id: `")
            .Append(evidence.RunId.ToString("D"))
            .AppendLine("`")
            .Append("- Launch name: `")
            .Append(launchName)
            .AppendLine("`")
            .Append("- Manager: `")
            .Append(seed.ManagerName)
            .AppendLine("`")
            .Append("- Builder agent: `")
            .Append(seed.BuilderAgentName)
            .AppendLine("`")
            .Append("- Reviewer agent: `")
            .Append(seed.ReviewerAgentName)
            .AppendLine("`")
            .Append("- Run status: `")
            .Append(evidence.RunStatus)
            .AppendLine("`")
            .Append("- Execution runs observed: `")
            .Append(evidence.ExecutionRunCount)
            .AppendLine("`");

        if (evidence.DirectMessageThreadId.HasValue)
        {
            builder.Append("- Direct message thread: `")
                .Append(evidence.DirectMessageThreadId.Value.ToString("D"))
                .AppendLine("`");
        }

        builder.AppendLine()
            .AppendLine("## Generated project")
            .Append("- Root: `")
            .Append(evidence.ProjectRoot.Replace('\\', '/'))
            .AppendLine("`")
            .Append("- Project file: `")
            .Append(evidence.ProjectFile.Replace('\\', '/'))
            .AppendLine("`")
            .Append("- Home page: `")
            .Append(evidence.HomePageFile.Replace('\\', '/'))
            .AppendLine("`")
            .Append("- README: `")
            .Append(evidence.ReadmeFile.Replace('\\', '/'))
            .AppendLine("`")
            .AppendLine()
            .AppendLine("## Projected process artifacts");

        foreach (var artifactTitle in evidence.ProcessArtifactTitles.OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
        {
            builder.Append("- `")
                .Append(artifactTitle)
                .AppendLine("`");
        }

        builder.AppendLine()
            .AppendLine("## Execution artifact paths");
        foreach (var artifactPath in evidence.ExecutionArtifactPaths.OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
        {
            builder.Append("- `")
                .Append(artifactPath)
                .AppendLine("`");
        }

        return builder.ToString().TrimEnd();
    }

    private string ResolveScenarioCalculatorProjectRoot(Guid runId)
    {
        if (string.IsNullOrWhiteSpace(fixture.StorageWorkspaceRoot))
        {
            throw new InvalidOperationException("Playwright fixture did not expose the storage workspace root.");
        }

        return Path.Combine(
            fixture.StorageWorkspaceRoot,
            "output",
            "ps",
            CreateProcessScenarioStorageKey(runId),
            "sc03",
            "w",
            "ScenarioCalculator");
    }

    private static string CreateProcessScenarioStorageKey(Guid runId)
    {
        return runId.ToString("N")[..12];
    }

    private sealed record CollaborationBrowserSeed(
        Guid ThreadId,
        string Subject,
        string MessageBody,
        string ContextLabel);

    private sealed record DirectMessagingBrowserSeed(
        Guid ProjectId,
        Guid DefinitionId,
        Guid RunId,
        Guid SourceRoleRequirementId,
        Guid TargetRoleRequirementId);

    private sealed record CalculatorScenarioSeed(
        Guid ProjectId,
        Guid DefinitionId,
        string ManagerName,
        Guid BuilderPartyId,
        Guid ReviewerPartyId,
        string BuilderAgentName,
        string ReviewerAgentName,
        Guid BuilderRoleRequirementId,
        Guid ReviewerRoleRequirementId,
        string BuilderRoleName,
        string ReviewerRoleName,
        string GenerationStepTitle,
        string HandoffStepTitle,
        string ReviewStepTitle);

    private sealed record CalculatorRunEvidence(
        Guid RunId,
        ProcessRunStatus RunStatus,
        IReadOnlyList<string> ExecutionArtifactPaths,
        IReadOnlyList<string> ProcessArtifactTitles,
        string ProjectRoot,
        string ProjectFile,
        string HomePageFile,
        string ReadmeFile,
        Guid? DirectMessageThreadId,
        int ExecutionRunCount);
}
