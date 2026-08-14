using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Drivers.Abstractions;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessDriverAbstractionTests
{
    [Fact]
    public void Driver_catalog_orders_dependencies_before_dependents()
    {
        var foundation = NewPackage(
            "driver.foundation",
            ProcessDriverLayer.BroadBase,
            Tags("capability.foundation"));
        var specialized = NewPackage(
            "driver.specialized",
            ProcessDriverLayer.Scenario,
            Tags("capability.specialized"),
            [new ProcessDriverDependency(new DriverId("driver.foundation"), ">=1.0")]);
        var catalog = new ProcessDriverCatalog([specialized, foundation]);

        var result = catalog.Match(new ProcessCapabilityRequest(
            Tags("capability.specialized"),
            NoTags(),
            NoTags()));

        Assert.True(result.Succeeded);
        Assert.Equal(["driver.foundation", "driver.specialized"], result.OrderedDrivers.Select(driver => driver.DriverId.Value));
    }

    [Fact]
    public void Driver_catalog_reports_missing_required_capabilities()
    {
        var catalog = new ProcessDriverCatalog([]);

        var result = catalog.Match(new ProcessCapabilityRequest(
            Tags("capability.required"),
            NoTags(),
            NoTags()));

        Assert.False(result.Succeeded);
        Assert.Contains(new CapabilityTag("capability.required"), result.MissingCapabilityTags);
    }

    [Fact]
    public void Driver_catalog_reports_unavailable_required_host_capability()
    {
        var package = NewPackage(
            "driver.python",
            ProcessDriverLayer.Platform,
            Tags("capability.python"),
            requiredHostCapabilities: new HashSet<ProcessHostCapabilityId>
            {
                ProcessHostCapabilityIds.PythonRuntime
            });
        var catalog = new ProcessDriverCatalog([package]);
        var request = new ProcessCapabilityRequest(
            Tags("capability.python"),
            NoTags(),
            NoTags())
        {
            HostCapabilities = new ProcessHostCapabilitySnapshot(
                new ProcessHostProfileId("linux"),
                [
                    new ProcessHostCapabilityFact(
                        ProcessHostCapabilityIds.PythonRuntime,
                        ProcessHostCapabilityAvailability.Unavailable,
                        ProcessHostCapabilityReason.DependencyMissing,
                        ProcessHostExecutionPort.None)
                ])
        };

        var result = catalog.Match(request);

        Assert.False(result.Succeeded);
        Assert.Equal([ProcessHostCapabilityIds.PythonRuntime], result.MissingHostCapabilities);
        Assert.Contains("host capabilities", Assert.Single(result.Diagnostics), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Driver_catalog_reports_duplicate_exclusive_capabilities()
    {
        var capability = new CapabilityTag("capability.exclusive");
        var first = NewPackage("driver.first", ProcessDriverLayer.Framework, Tags(capability));
        var second = NewPackage("driver.second", ProcessDriverLayer.Scenario, Tags(capability));
        var catalog = new ProcessDriverCatalog([first, second]);

        var result = catalog.Match(new ProcessCapabilityRequest(
            Tags(capability),
            NoTags(),
            Tags(capability)));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Conflicts, conflict => conflict.ExclusiveCapabilityTag == capability);
    }

    [Fact]
    public void Driver_catalog_reports_declared_driver_conflicts()
    {
        var first = NewPackage(
            "driver.first",
            ProcessDriverLayer.Framework,
            Tags("capability.first"),
            conflicts:
            [
                new ProcessDriverConflict(
                    new DriverId("driver.second"),
                    null,
                    "Cannot combine these drivers.")
            ]);
        var second = NewPackage("driver.second", ProcessDriverLayer.Scenario, Tags("capability.second"));
        var catalog = new ProcessDriverCatalog([first, second]);

        var result = catalog.Match(new ProcessCapabilityRequest(
            Tags("capability.first", "capability.second"),
            NoTags(),
            NoTags()));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Conflicts, conflict => conflict.DriverId == new DriverId("driver.second"));
    }

    [Fact]
    public async Task Strategy_factory_returns_result_envelope_without_runtime_mutation_contracts()
    {
        var descriptor = new ProcessStrategyDescriptor(
            new StrategyId("strategy.execute"),
            "1.0.0",
            ProcessStrategyKind.StepExecution,
            Tags("capability.execute"));
        var factory = new FakeStrategyFactory(descriptor);
        var binding = new ProcessStrategyBindingSnapshot(
            new DriverId("driver.execute"),
            descriptor.StrategyId,
            descriptor.StrategyVersion,
            "factory/1.0",
            "runtime/1.0",
            "runtime/2.x",
            "sha256:binding",
            [new StrategyBindingInput(new StrategyBindingInputKey("input"), "sha256:input")]);

        var strategy = await factory.CreateAsync(binding);
        var result = await strategy.ExecuteAsync(new ProcessStrategyExecutionContext(
            ProcessRunId.New(),
            ProcessStepInstanceId.New(),
            binding,
            binding.Inputs)
        {
            DispatchClaimIdentity = new ProcessDispatchClaimIdentity(Guid.NewGuid())
        });

        Assert.Equal(descriptor.StrategyId, result.StrategyId);
        Assert.Equal(StrategyOutcome.Succeeded, result.Outcome);
        Assert.Equal("sha256:result", result.ResultHash);
    }

    [Fact]
    public void Capability_tags_are_compared_as_opaque_values()
    {
        var requested = new CapabilityTag("opaque.any-value");
        var catalog = new ProcessDriverCatalog([
            NewPackage("driver.opaque", ProcessDriverLayer.Framework, Tags(requested))
        ]);

        var result = catalog.Match(new ProcessCapabilityRequest(Tags(requested), NoTags(), NoTags()));

        Assert.True(result.Succeeded);
        Assert.Equal("driver.opaque", Assert.Single(result.OrderedDrivers).DriverId.Value);
    }

    [Fact]
    public void Driver_catalog_rejects_platform_driver_without_host_capability_authority()
    {
        var package = NewPackage(
            "driver.unconstrained-platform",
            ProcessDriverLayer.Platform,
            Tags("capability.execution"));

        var exception = Assert.Throws<ArgumentException>(() => new ProcessDriverCatalog([package]));

        Assert.Contains("must declare at least one host capability", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Driver_catalog_rejects_platform_driver_with_default_host_capability_identifier()
    {
        var package = NewPackage(
            "driver.invalid-platform",
            ProcessDriverLayer.Platform,
            Tags("capability.execution"),
            requiredHostCapabilities: new HashSet<ProcessHostCapabilityId>
            {
                default
            });

        var exception = Assert.Throws<ArgumentException>(() => new ProcessDriverCatalog([package]));

        Assert.Contains("valid capability identifiers", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Driver_catalog_rejects_strategy_with_over_bound_host_capability_contract()
    {
        var strategy = new ProcessStrategyDescriptor(
            new StrategyId("strategy.invalid"),
            "1.0.0",
            ProcessStrategyKind.StepExecution,
            Tags("capability.execution"))
        {
            RequiredHostCapabilities = Enumerable.Range(0, 33)
                .Select(index => new ProcessHostCapabilityId($"host.test.cap-{index:D2}"))
                .ToHashSet()
        };
        var package = NewPackage(
            "driver.invalid-strategy",
            ProcessDriverLayer.Framework,
            Tags("capability.execution"));
        package = package with
        {
            Descriptor = package.Descriptor with
            {
                Strategies = [strategy]
            }
        };

        var exception = Assert.Throws<ArgumentException>(() => new ProcessDriverCatalog([package]));

        Assert.Contains("Strategy host capability requirements", exception.Message, StringComparison.Ordinal);
    }

    private static ProcessDriverPackage NewPackage(
        string driverId,
        ProcessDriverLayer layer,
        IReadOnlySet<CapabilityTag> capabilities,
        IReadOnlyList<ProcessDriverDependency>? dependencies = null,
        IReadOnlyList<ProcessDriverConflict>? conflicts = null,
        IReadOnlySet<ProcessHostCapabilityId>? requiredHostCapabilities = null)
    {
        var descriptor = new ProcessDriverDescriptor(
            new DriverId(driverId),
            driverId,
            "1.0.0",
            "runtime/1.0",
            "runtime/2.x",
            layer,
            capabilities,
            dependencies ?? [],
            conflicts ?? [],
            [],
            [])
        {
            RequiredHostCapabilities = requiredHostCapabilities ?? new HashSet<ProcessHostCapabilityId>()
        };

        return new ProcessDriverPackage(descriptor, [], [], [], [], [], []);
    }

    private static IReadOnlySet<CapabilityTag> Tags(params string[] values)
    {
        return values.Select(value => new CapabilityTag(value)).ToHashSet();
    }

    private static IReadOnlySet<CapabilityTag> Tags(params CapabilityTag[] values)
    {
        return values.ToHashSet();
    }

    private static IReadOnlySet<CapabilityTag> NoTags()
    {
        return new HashSet<CapabilityTag>();
    }

    private sealed class FakeStrategyFactory : IProcessStrategyFactory
    {
        public FakeStrategyFactory(ProcessStrategyDescriptor descriptor)
        {
            Descriptor = descriptor;
        }

        public ProcessStrategyDescriptor Descriptor { get; }

        public ValueTask<IProcessStrategy> CreateAsync(
            ProcessStrategyBindingSnapshot binding,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<IProcessStrategy>(new FakeStrategy(binding));
        }
    }

    private sealed class FakeStrategy : IProcessStrategy
    {
        private readonly ProcessStrategyBindingSnapshot binding;

        public FakeStrategy(ProcessStrategyBindingSnapshot binding)
        {
            this.binding = binding;
        }

        public ValueTask<StrategyResultEnvelope> ExecuteAsync(
            ProcessStrategyExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(new StrategyResultEnvelope(
                binding.StrategyId,
                binding.StrategyVersion,
                Guid.NewGuid(),
                StrategyOutcome.Succeeded,
                [],
                [],
                [],
                [],
                "sha256:result"));
        }
    }
}
