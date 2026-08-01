using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Modules.Processes;

internal sealed class BrowserExecutionMetadataContribution : IProcessExecutionMetadataContribution
{
    public string ContributionKey => "browser.execution-metadata";

    public int Order => 100;

    public IReadOnlyDictionary<string, object> BuildMetadata(
        ProcessExecutionMetadataContributionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [ExecutionInvocationMetadata.ProcessBrowserToolsAllowedMetadataKey] =
                BrowserRuntimeToolAccessPolicy.AllowsBrowserTools(context.Assignment)
        };
    }
}
