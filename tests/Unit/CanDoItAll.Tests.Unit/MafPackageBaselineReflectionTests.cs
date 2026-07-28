using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using A2A;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Tests.Unit;

public sealed class MafPackageBaselineReflectionTests
{
    private const string ExpectedAssemblyVersion = "1.15.0.0";
    private const string ExpectedStablePackageVersion = "1.15.0";
    private const string ExpectedPreviewPackageVersion = "1.15.0-preview.260722.1";
    private const string ExpectedStableInformationalVersionPrefix = "1.15.0+";
    private const string ExpectedPreviewInformationalVersionPrefix = "1.15.0-preview+";
    private static readonly string[] ExpectedMafAssemblyNames =
    [
        "Microsoft.Agents.AI",
        "Microsoft.Agents.AI.A2A",
        "Microsoft.Agents.AI.Hosting.A2A",
        "Microsoft.Agents.AI.OpenAI",
        "Microsoft.Agents.AI.Workflows"
    ];
    private static readonly HashSet<string> PreviewMafAssemblyNames =
    [
        "Microsoft.Agents.AI.A2A",
        "Microsoft.Agents.AI.Hosting.A2A"
    ];

    [Fact]
    public void Maf_symbols_are_classified_from_loaded_runtime_assemblies()
    {
        var mafAssemblies = ExpectedMafAssemblyNames
            .Select(Assembly.Load)
            .ToArray();
        var symbolAssemblies = mafAssemblies
            .Concat(
            [
                typeof(AIAgent).Assembly,
                typeof(MessageAIContextProvider).Assembly,
                typeof(ApprovalRequiredAIFunction).Assembly,
                typeof(AgentCard).Assembly
            ])
            .Distinct()
            .ToArray();
        var availableTypeNames = symbolAssemblies
            .SelectMany(GetLoadableTypes)
            .Select(type => type.Name)
            .ToHashSet(StringComparer.Ordinal);
        var assemblyIdentities = mafAssemblies
            .Select(assembly => new
            {
                Name = assembly.GetName().Name ?? string.Empty,
                Version = assembly.GetName().Version?.ToString() ?? string.Empty,
                InformationalVersion = assembly
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                    .InformationalVersion ?? string.Empty
            })
            .OrderBy(identity => identity.Name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(ExpectedMafAssemblyNames, assemblyIdentities.Select(identity => identity.Name));
        Assert.All(
            assemblyIdentities,
            identity =>
            {
                Assert.Equal(ExpectedAssemblyVersion, identity.Version);
                Assert.DoesNotContain("1.13", identity.Version, StringComparison.Ordinal);
                Assert.DoesNotContain("1.13", identity.InformationalVersion, StringComparison.Ordinal);

                var expectedInformationalVersionPrefix = PreviewMafAssemblyNames.Contains(identity.Name)
                    ? ExpectedPreviewInformationalVersionPrefix
                    : ExpectedStableInformationalVersionPrefix;
                Assert.StartsWith(
                    expectedInformationalVersionPrefix,
                    identity.InformationalVersion,
                    StringComparison.Ordinal);
            });

        var packageVersionProperties = XDocument
            .Load(Path.Combine(FindRepoRoot(), "src", "MAF", "MicrosoftAgentFramework.Packages.props"))
            .Descendants()
            .Where(element => element.Name.LocalName is
                "MicrosoftAgentsAIStableVersion" or
                "MicrosoftAgentsAIPreviewVersion")
            .ToDictionary(
                element => element.Name.LocalName,
                element => element.Value,
                StringComparer.Ordinal);

        Assert.Equal(
            ExpectedStablePackageVersion,
            packageVersionProperties["MicrosoftAgentsAIStableVersion"]);
        Assert.Equal(
            ExpectedPreviewPackageVersion,
            packageVersionProperties["MicrosoftAgentsAIPreviewVersion"]);

        Assert.Contains("MessageAIContextProvider", availableTypeNames);
        Assert.Contains("ApprovalRequiredAIFunction", availableTypeNames);
        Assert.Contains("WorkflowBuilder", availableTypeNames);
        Assert.Contains("AgentWorkflowBuilder", availableTypeNames);
        Assert.Contains("AgentCard", availableTypeNames);
        Assert.Contains("A2ACardResolver", availableTypeNames);

        Assert.DoesNotContain("IChatMessageInjector", availableTypeNames);
        Assert.DoesNotContain("AgentSessionFiles", availableTypeNames);
        Assert.DoesNotContain("SkillFrontmatter", availableTypeNames);
        Assert.DoesNotContain("OpenTelemetryChatClient", availableTypeNames);
    }

    private static string FindRepoRoot([CallerFilePath] string sourceFilePath = "")
    {
        var current = new DirectoryInfo(Path.GetDirectoryName(sourceFilePath)!);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "CanDoItAll.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }

    private static IReadOnlyList<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types
                .OfType<Type>()
                .ToArray();
        }
    }
}
