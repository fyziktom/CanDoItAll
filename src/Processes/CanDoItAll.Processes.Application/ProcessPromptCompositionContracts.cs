using CanDoItAll.Processes.Abstractions;

namespace CanDoItAll.Processes.Application;

public interface IProcessPromptCompositionDriver
{
    DriverId DriverId { get; }

    bool CanCompose(ProcessStepBriefBuildRequest request);

    string Compose(ProcessStepBriefBuildRequest request);
}

public sealed class DriverProcessStepBriefBuilder(
    IEnumerable<IProcessPromptCompositionDriver> promptDrivers) : IProcessStepBriefBuilder
{
    private readonly GenericProcessStepBriefBuilder fallbackBuilder = new();

    public string Build(ProcessStepBriefBuildRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        foreach (var driver in promptDrivers)
        {
            if (driver.CanCompose(request))
            {
                return driver.Compose(request);
            }
        }

        return fallbackBuilder.Build(request);
    }
}
