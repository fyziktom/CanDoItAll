using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Processes;

internal sealed class ProcessDecisionPublicationTestClock : TimeProvider {
    private long ticks = ProcessProjectAdmissionFixture.Now.AddMinutes(30).UtcTicks;
    public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref ticks), TimeSpan.Zero);
    internal void SetUtcNow(DateTimeOffset value) => Interlocked.Exchange(ref ticks, value.UtcTicks);

    internal TestHarnessOptions Harness(CanDoItAllTestEnvironment? environment = null, TestDatabaseProfile? profile = null)
        => new() { TestEnvironment = environment, ActiveProfile = profile, ConfigureServices = services => services.AddSingleton<TimeProvider>(this) };
}
