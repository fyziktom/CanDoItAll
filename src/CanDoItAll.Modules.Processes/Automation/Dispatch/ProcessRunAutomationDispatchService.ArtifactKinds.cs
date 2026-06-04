using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections;
using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private static ProcessArtifactKind ResolveExpectedArtifactKind(ProcessAutomationExecutionArtifact artifact)
    {
        if (artifact.RelativePath.EndsWith("/response.md", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessArtifactKind.Transcript;
        }

        var fileName = Path.GetFileName(artifact.RelativePath.Replace('\\', '/'));
        var extension = Path.GetExtension(fileName);
        if (artifact.ContentType.Contains("image", StringComparison.OrdinalIgnoreCase) || IsImageExtension(extension))
        {
            return ProcessArtifactKind.Evidence;
        }

        if (ContainsArtifactHint(fileName, "checklist"))
        {
            return ProcessArtifactKind.Checklist;
        }

        if (ContainsArtifactHint(fileName, "log") ||
            ContainsArtifactHint(fileName, "transcript") ||
            ContainsArtifactHint(fileName, "stdout") ||
            ContainsArtifactHint(fileName, "stderr"))
        {
            return ProcessArtifactKind.Transcript;
        }

        return string.Equals(artifact.ArtifactKind, "generated-output", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".md", StringComparison.OrdinalIgnoreCase) ||
               IsCodeOrProjectExtension(extension)
            ? ProcessArtifactKind.Deliverable
            : ProcessArtifactKind.Evidence;
    }

    private static bool ContainsArtifactHint(string fileName, string hint)
    {
        return fileName.Contains(hint, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCodeOrProjectExtension(string extension)
    {
        return extension.Equals(".cs", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".razor", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".sln", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".cshtml", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".html", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".css", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".js", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".mjs", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".cjs", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jsx", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".ts", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".tsx", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".props", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".targets", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".json", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsImageExtension(string extension)
    {
        return extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".svg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".gif", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".webp", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTransientExecutionArtifact(ProcessAutomationExecutionArtifact artifact)
    {
        var relativePath = artifact.RelativePath.Replace('\\', '/');
        return relativePath.StartsWith(".playwright-mcp/", StringComparison.OrdinalIgnoreCase) ||
               relativePath.Contains("/.playwright-mcp/", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<IReadOnlyList<DispatchArtifactExpectation>> LoadExpectedArtifactsAsync(
        AppDbContext dbContext,
        Guid stepDefinitionId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Set<ProcessArtifactExpectation>()
            .AsNoTracking()
            .Where(item => item.StepDefinitionId == stepDefinitionId)
            .OrderBy(item => item.Title)
            .Select(item => new DispatchArtifactExpectation(
                item.Id,
                item.ArtifactKind,
                item.Title,
                item.IsRequired,
                item.TrustRequirement,
                item.SensitivityLevel,
                item.ValidationRequirementSummary,
                item.AllowedFutureUsageSummary))
            .ToListAsync(cancellationToken);
    }

}
