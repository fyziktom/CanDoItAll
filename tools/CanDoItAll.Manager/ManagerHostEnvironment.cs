namespace CanDoItAll.Manager;

public static class ManagerHostEnvironment
{
    public static string ResolveEnvironmentName()
    {
        var aspnetcore = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        if (!string.IsNullOrWhiteSpace(aspnetcore))
        {
            return aspnetcore.Trim();
        }

        var dotnet = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        if (!string.IsNullOrWhiteSpace(dotnet))
        {
            return dotnet.Trim();
        }

        return Environments.Development;
    }
}
