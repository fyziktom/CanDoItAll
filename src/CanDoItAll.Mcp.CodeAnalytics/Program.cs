using CanDoItAll.CodeAnalytics.Abstractions;
using CanDoItAll.CodeAnalytics.Abstractions.Options;
using CanDoItAll.CodeAnalytics.Analysis.Graphs;
using CanDoItAll.CodeAnalytics.Analysis.Rules;
using CanDoItAll.CodeAnalytics.Application.Services;
using CanDoItAll.CodeAnalytics.Facts.Dependencies;
using CanDoItAll.CodeAnalytics.Facts.Documentation;
using CanDoItAll.CodeAnalytics.Facts.Members;
using CanDoItAll.CodeAnalytics.Facts.Persistence;
using CanDoItAll.CodeAnalytics.Facts.Services;
using CanDoItAll.CodeAnalytics.Facts.Symbols;
using CanDoItAll.CodeAnalytics.Rendering.Exports;
using CanDoItAll.CodeAnalytics.Rendering.Markdown;
using CanDoItAll.CodeAnalytics.Rendering.Mermaid;
using CanDoItAll.CodeAnalytics.Storage.Snapshots;
using CanDoItAll.CodeAnalytics.Workspace.Inventory;
using CanDoItAll.CodeAnalytics.Workspace.Loading;
using CanDoItAll.CodeAnalytics.Workspace.Normalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace CanDoItAll.Mcp.CodeAnalytics;

internal static class Program
{
    public static async Task Main(string[] args)
    {
        var settingsPath = ResolveSettingsPath(args);

        var builder = Host.CreateEmptyApplicationBuilder(settings: null);

        builder.Configuration.AddJsonFile(settingsPath, optional: false, reloadOnChange: false);
        builder.Configuration.AddEnvironmentVariables(prefix: "CanDoItAllMcp_");

        builder.Logging.ClearProviders();
        builder.Logging.AddConsole(options =>
        {
            options.LogToStandardErrorThreshold = LogLevel.Trace;
        });
        builder.Logging.SetMinimumLevel(LogLevel.Information);

        builder.Services
            .AddOptions<McpServerOptions>()
            .Bind(builder.Configuration)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<McpServerOptions>, McpServerOptionsValidator>();

        RegisterCodeAnalyticsServices(builder.Services);

        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithTools<CodeAnalyticsTools>();

        using var host = builder.Build();
        await host.RunAsync();
    }

    private static void RegisterCodeAnalyticsServices(IServiceCollection services)
    {
        services.AddSingleton<RuntimeConfiguration>();
        services.AddSingleton(
            static serviceProvider =>
            {
                var runtimeConfiguration = serviceProvider.GetRequiredService<RuntimeConfiguration>();
                return new CodeAnalyticsApplicationOptions(
                    runtimeConfiguration.OutputRootPath,
                    runtimeConfiguration.GeneratorVersion,
                    runtimeConfiguration.MaxRecentSnapshots,
                    runtimeConfiguration.MaxDiagramNodes);
            });

        services.AddSingleton<AnalysisRequestNormalizer>();
        services.AddSingleton<ProjectFileInventoryReader>();
        services.AddSingleton<MsBuildWorkspaceLoader>();
        services.AddSingleton<XmlDocumentationNormalizer>();
        services.AddSingleton<SymbolFactsCollector>();
        services.AddSingleton<MemberRelationshipCollector>();
        services.AddSingleton<DependencyFactCollector>();
        services.AddSingleton<ServiceRegistrationCollector>();
        services.AddSingleton<PersistenceFactCollector>();
        services.AddSingleton<StronglyConnectedComponentFinder>();
        services.AddSingleton<ArchitectureInsightBuilder>();
        services.AddSingleton<MarkdownSummaryWriter>();
        services.AddSingleton<ProjectGraphMermaidRenderer>();
        services.AddSingleton<ClassDiagramMermaidRenderer>();
        services.AddSingleton<ErDiagramMermaidRenderer>();
        services.AddSingleton<ExportBundleBuilder>();
        services.AddSingleton<SnapshotJsonSerializer>();
        services.AddSingleton<FileSnapshotRepository>();
        services.AddSingleton<ICodeAnalyticsApplicationService, CodeAnalyticsApplicationService>();
        services.AddSingleton<ICodeAnalyticsCoordinator, CodeAnalyticsCoordinator>();
    }

    private static string ResolveSettingsPath(string[] args)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], "--settings", StringComparison.OrdinalIgnoreCase))
            {
                return args[index + 1];
            }
        }

        return "CanDoItAll.Mcp.CodeAnalytics.settings.json";
    }
}
