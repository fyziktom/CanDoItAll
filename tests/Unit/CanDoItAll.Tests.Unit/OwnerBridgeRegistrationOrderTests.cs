using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit.Infrastructure;

// Owner adapters replace the no-op defaults of the contracts they implement; the composed host must resolve the owner
// whatever the module registration order, and never two writers for the same contract.
public sealed class OwnerBridgeRegistrationOrderTests
{
    [Theory]
    [InlineData(typeof(IProjectPartyIntegrationBridge))]
    [InlineData(typeof(IProjectPartyCostRateBridge))]
    public void CrmHr_party_bridges_replace_the_Projects_defaults_regardless_of_module_registration_order(Type bridgeType)
    {
        Action<IServiceCollection>[] registrations =
        [
            services =>
            {
                services.AddProjectsModule();
                services.AddCrmHrModule();
            },
            services =>
            {
                services.AddCrmHrModule();
                services.AddProjectsModule();
            }
        ];

        foreach (var register in registrations)
        {
            var services = new ServiceCollection();

            register(services);

            var descriptor = Assert.Single(services, item => item.ServiceType == bridgeType);
            Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
            Assert.Null(descriptor.ImplementationType);
            Assert.NotNull(descriptor.ImplementationFactory);
        }
    }

    [Fact]
    public void Agents_technical_agent_bridge_is_the_only_registered_writer_regardless_of_module_registration_order()
    {
        var configuration = new ConfigurationBuilder().Build();
        Action<IServiceCollection>[] registrations =
        [
            services =>
            {
                services.AddCrmHrModule();
                services.AddAgentFrameworkModule(configuration);
            },
            services =>
            {
                services.AddAgentFrameworkModule(configuration);
                services.AddCrmHrModule();
            }
        ];

        foreach (var register in registrations)
        {
            var services = new ServiceCollection();

            register(services);

            var descriptor = Assert.Single(services, item => item.ServiceType == typeof(IAiTechnicalAgentBridge));
            Assert.Equal("AgentFrameworkAiTechnicalAgentBridge", descriptor.ImplementationType?.Name);
            Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        }
    }
}
