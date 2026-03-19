using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Factory;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Resources;
using CanDoItAll.Modules.TestLab;
using CanDoItAll.Modules.Validation;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

public enum ProjectStructureNodeKind
{
    Project,
    Phase,
    Resource,
    PromptRun,
    PromptStep,
    ValidationRun,
    TestPlan
}

public enum ProjectStructureCommandKind
{
    Open,
    Branch,
    Validate,
    Test,
    Skip,
    MarkUsed
}

public sealed record ProjectStructureNode(
    string Id,
    string? ParentId,
    ProjectStructureNodeKind Kind,
    string Title,
    string Subtitle,
    string Status,
    string Route,
    string ArtifactKind,
    Guid? ArtifactId);

public sealed record ProjectStructureLink(string SourceId, string TargetId, string Kind);

public sealed record ProjectStructureSurface(
    Guid ProjectId,
    string ProjectName,
    IReadOnlyList<ProjectStructureNode> Nodes,
    IReadOnlyList<ProjectStructureLink> Links);

public sealed record ProjectCalendarEvent(
    Guid Id,
    string Title,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    string Status,
    string Route,
    string ArtifactKind,
    Guid? ArtifactId);

public sealed record ProjectCalendarSurface(
    Guid ProjectId,
    string ProjectName,
    IReadOnlyList<ProjectCalendarEvent> Events);

/* codex-capsule
kind: service
name: ProjectWorkbenchService
summary: Aggregates project, resource, prompt-flow, validation, and test data into structure and calendar surfaces.
owns: workbench projections, structure commands, calendar events
deps: AppDbContext
risks: stale-cross-module-read, route-mismatch
tests: integration:ProjectWorkbenchServiceTests
inputs: project id, command requests
outputs: ProjectStructureSurface, ProjectCalendarSurface, ArtifactReference
*/
public sealed class ProjectWorkbenchService(IDbContextFactory<AppDbContext> dbContextFactory, IClock clock)
{
    public async Task<ProjectStructureSurface> GetStructureAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var project = await dbContext.Set<Project>().FirstAsync(item => item.Id == projectId, cancellationToken);
        var phases = await dbContext.Set<ProjectPhase>().Where(item => item.ProjectId == projectId).OrderBy(item => item.OrderIndex).ToListAsync(cancellationToken);
        var resources = await dbContext.Set<ProjectResource>().Where(item => item.ProjectId == projectId).OrderBy(item => item.Name).ToListAsync(cancellationToken);
        var runs = (await dbContext.Set<PromptRun>().Where(item => item.ProjectId == projectId).ToListAsync(cancellationToken))
            .OrderBy(item => item.CreatedAtUtc)
            .ToList();
        var runNodes = await dbContext.Set<PromptRunNode>().Where(item => runs.Select(run => run.Id).Contains(item.PromptRunId)).OrderBy(item => item.Sequence).ToListAsync(cancellationToken);
        var validations = (await dbContext.Set<ValidationRun>().Where(item => item.ProjectId == projectId).ToListAsync(cancellationToken))
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ToList();
        var testPlans = (await dbContext.Set<TestPlan>().Where(item => item.ProjectId == projectId).ToListAsync(cancellationToken))
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ToList();

        var nodes = new List<ProjectStructureNode>
        {
            new($"project:{project.Id}", null, ProjectStructureNodeKind.Project, project.Name, project.Status.ToString(), project.CurrentPhase, $"/projects?projectId={project.Id}", "project", project.Id)
        };
        var links = new List<ProjectStructureLink>();

        foreach (var phase in phases)
        {
            var phaseId = $"phase:{phase.Id}";
            nodes.Add(new ProjectStructureNode(phaseId, $"project:{project.Id}", ProjectStructureNodeKind.Phase, phase.Name, phase.Goal, phase.Status.ToString(), $"/projects?projectId={project.Id}", "phase", phase.Id));
            links.Add(new ProjectStructureLink($"project:{project.Id}", phaseId, "contains"));
        }

        foreach (var resource in resources)
        {
            var nodeId = $"resource:{resource.Id}";
            nodes.Add(new ProjectStructureNode(nodeId, $"project:{project.Id}", ProjectStructureNodeKind.Resource, resource.Name, resource.ResourceKind.ToString(), resource.ValidationStatus.ToString(), $"/resources?resourceId={resource.Id}", "resource", resource.Id));
            links.Add(new ProjectStructureLink($"project:{project.Id}", nodeId, "uses"));
        }

        foreach (var run in runs)
        {
            var phaseNodeId = phases.FirstOrDefault(phase => string.Equals(phase.Name, run.Phase, StringComparison.OrdinalIgnoreCase)) is { } phase
                ? $"phase:{phase.Id}"
                : $"project:{project.Id}";
            var runNodeId = $"prompt-run:{run.Id}";
            nodes.Add(new ProjectStructureNode(runNodeId, phaseNodeId, ProjectStructureNodeKind.PromptRun, run.Name, run.Phase, "Active", $"/prompt-factory?runId={run.Id}", "prompt-run", run.Id));
            links.Add(new ProjectStructureLink(phaseNodeId, runNodeId, "follows"));

            foreach (var node in runNodes.Where(item => item.PromptRunId == run.Id))
            {
                var promptNodeId = $"prompt-node:{node.Id}";
                nodes.Add(new ProjectStructureNode(promptNodeId, runNodeId, ProjectStructureNodeKind.PromptStep, node.Title, node.BranchKey, node.State.ToString(), node.PromptArtifactId.HasValue ? $"/prompt-gallery?promptId={node.PromptArtifactId}" : $"/prompt-factory?runId={run.Id}", "prompt-node", node.Id));
                links.Add(new ProjectStructureLink(runNodeId, promptNodeId, "contains"));
            }
        }

        foreach (var validation in validations)
        {
            var nodeId = $"validation:{validation.Id}";
            nodes.Add(new ProjectStructureNode(nodeId, $"project:{project.Id}", ProjectStructureNodeKind.ValidationRun, validation.ArtifactTitle, validation.ValidationType.ToString(), validation.Decision.ToString(), $"/validation?runId={validation.Id}", "validation", validation.Id));
            links.Add(new ProjectStructureLink($"project:{project.Id}", nodeId, "validates"));
        }

        foreach (var testPlan in testPlans)
        {
            var nodeId = $"test-plan:{testPlan.Id}";
            nodes.Add(new ProjectStructureNode(nodeId, $"project:{project.Id}", ProjectStructureNodeKind.TestPlan, testPlan.Title, testPlan.Phase, "Planned", $"/test-lab?planId={testPlan.Id}", "test-plan", testPlan.Id));
            links.Add(new ProjectStructureLink($"project:{project.Id}", nodeId, "tests"));
        }

        return new ProjectStructureSurface(project.Id, project.Name, nodes, links);
    }

    public async Task<ProjectCalendarSurface> GetCalendarAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var project = await dbContext.Set<Project>().FirstAsync(item => item.Id == projectId, cancellationToken);
        var phases = await dbContext.Set<ProjectPhase>().Where(item => item.ProjectId == projectId).OrderBy(item => item.OrderIndex).ToListAsync(cancellationToken);
        var validations = (await dbContext.Set<ValidationRun>().Where(item => item.ProjectId == projectId).ToListAsync(cancellationToken))
            .OrderByDescending(item => item.UpdatedAtUtc)
            .Take(10)
            .ToList();
        var testRuns = (await dbContext.Set<TestRunRecord>()
            .Where(item => dbContext.Set<TestPlan>().Any(plan => plan.Id == item.TestPlanId && plan.ProjectId == projectId))
            .ToListAsync(cancellationToken))
            .OrderByDescending(item => item.ExecutedAtUtc)
            .Take(10)
            .ToList();

        var events = new List<ProjectCalendarEvent>();
        foreach (var phase in phases)
        {
            if (!phase.StartDateUtc.HasValue && !phase.EndDateUtc.HasValue)
            {
                continue;
            }

            var start = phase.StartDateUtc.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(phase.StartDateUtc.Value, DateTimeKind.Utc)) : clock.GetUtcNow();
            var end = phase.EndDateUtc.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(phase.EndDateUtc.Value, DateTimeKind.Utc)) : start.AddDays(1);
            events.Add(new ProjectCalendarEvent(phase.Id, phase.Name, start, end, phase.Status.ToString(), $"/projects?projectId={projectId}", "phase", phase.Id));
        }

        foreach (var validation in validations)
        {
            events.Add(new ProjectCalendarEvent(validation.Id, $"Validation · {validation.ArtifactTitle}", validation.UpdatedAtUtc, validation.UpdatedAtUtc.AddHours(1), validation.Decision.ToString(), $"/validation?runId={validation.Id}", "validation", validation.Id));
        }

        foreach (var run in testRuns)
        {
            events.Add(new ProjectCalendarEvent(run.Id, $"Test run · {run.Runner}", run.ExecutedAtUtc, run.ExecutedAtUtc.AddHours(1), run.Result.ToString(), "/test-lab", "test-run", run.Id));
        }

        return new ProjectCalendarSurface(project.Id, project.Name, events.OrderBy(item => item.StartUtc).ToList());
    }

    public async Task<ArtifactReference?> ExecuteNodeCommandAsync(Guid projectId, string nodeId, ProjectStructureCommandKind commandKind, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (nodeId.StartsWith("prompt-node:", StringComparison.OrdinalIgnoreCase) &&
            Guid.TryParse(nodeId["prompt-node:".Length..], out var promptNodeId))
        {
            var node = await dbContext.Set<PromptRunNode>().FirstOrDefaultAsync(item => item.Id == promptNodeId, cancellationToken);
            if (node is null)
            {
                return null;
            }

            switch (commandKind)
            {
                case ProjectStructureCommandKind.Branch:
                    var branchNode = new PromptRunNode
                    {
                        PromptRunId = node.PromptRunId,
                        PromptBlockDefinitionId = node.PromptBlockDefinitionId,
                        Title = $"{node.Title} follow-up",
                        BranchKey = $"branch-{clock.GetUtcNow():yyyyMMddHHmmss}",
                        Sequence = node.Sequence + 1,
                        State = PromptRunNodeState.Pending,
                        Notes = "Created from workbench branch action."
                    };
                    await dbContext.Set<PromptRunNode>().AddAsync(branchNode, cancellationToken);
                    await dbContext.SaveChangesAsync(cancellationToken);
                    return new ArtifactReference("prompt-node", branchNode.Id, branchNode.Title, "/prompt-factory", "Follow-up prompt branch");
                case ProjectStructureCommandKind.Skip:
                    node.State = PromptRunNodeState.Skipped;
                    await dbContext.SaveChangesAsync(cancellationToken);
                    return null;
                case ProjectStructureCommandKind.MarkUsed:
                    node.State = PromptRunNodeState.Used;
                    await dbContext.SaveChangesAsync(cancellationToken);
                    return null;
                case ProjectStructureCommandKind.Open:
                    return new ArtifactReference("prompt-node", node.Id, node.Title, node.PromptArtifactId.HasValue ? $"/prompt-gallery?promptId={node.PromptArtifactId}" : "/prompt-factory");
            }
        }

        return commandKind switch
        {
            ProjectStructureCommandKind.Validate => new ArtifactReference("validation", null, "Validation Center", $"/validation?projectId={projectId}"),
            ProjectStructureCommandKind.Test => new ArtifactReference("test-plan", null, "Test Lab", $"/test-lab?projectId={projectId}"),
            ProjectStructureCommandKind.Open => ResolveGenericRoute(nodeId),
            _ => null
        };
    }

    private static ArtifactReference? ResolveGenericRoute(string nodeId)
    {
        return Resolve(nodeId, "resource:", "resource", "/resources?resourceId={0}")
            ?? Resolve(nodeId, "validation:", "validation", "/validation?runId={0}")
            ?? Resolve(nodeId, "test-plan:", "test-plan", "/test-lab?planId={0}")
            ?? Resolve(nodeId, "project:", "project", "/projects?projectId={0}")
            ?? Resolve(nodeId, "phase:", "phase", "/projects");
    }

    private static ArtifactReference? Resolve(string nodeId, string prefix, string kind, string routeFormat)
    {
        if (!nodeId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Guid.TryParse(nodeId[prefix.Length..], out var id)
            ? new ArtifactReference(kind, id, kind, string.Format(routeFormat, id))
            : null;
    }
}
