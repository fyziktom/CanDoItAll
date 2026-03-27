using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Modules.Projects;

public static class ProjectsModuleServiceCollectionExtensions
{
    public static IServiceCollection AddProjectsModule(this IServiceCollection services)
    {
        services.AddScoped<ProjectsService>();
        return services;
    }
}

public static class ProjectsModuleAssemblyMarker;


