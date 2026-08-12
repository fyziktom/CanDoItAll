using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Hosting;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Drivers.Standard;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Processes.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Modules.Processes;


internal sealed class StandardProcessRuntimeStrategyFactoryResolver(
    IProcessStepExecutionDriver executionDriver) : IProcessRuntimeStrategyFactoryResolver
{
    public ValueTask<IProcessStrategyFactory> ResolveAsync(
        ProcessStrategyBindingSnapshot binding,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(binding);
        cancellationToken.ThrowIfCancellationRequested();

        var descriptor = executionDriver.Descriptor;
        var strategy = descriptor.Strategy;
        var boundCapabilityIds = binding.HostCapabilities
            .Select(fact => fact.Id)
            .ToHashSet();
        if (binding.DriverId != descriptor.DriverId ||
            binding.StrategyId != strategy.StrategyId ||
            !string.Equals(binding.StrategyVersion, strategy.StrategyVersion, StringComparison.Ordinal) ||
            !string.Equals(
                binding.FactoryVersion,
                ProcessStrategyBindingVersions.ForDriver(
                    StandardProcessAdapterDriverPackageFactory.DriverVersion),
                StringComparison.Ordinal) ||
            !string.Equals(
                binding.MinRuntimeSchema,
                StandardProcessAdapterDriverPackageFactory.MinimumRuntimeSchema,
                StringComparison.Ordinal) ||
            !string.Equals(
                binding.MaxRuntimeSchema,
                StandardProcessAdapterDriverPackageFactory.MaximumRuntimeSchema,
                StringComparison.Ordinal) ||
            !boundCapabilityIds.SetEquals(strategy.RequiredHostCapabilities))
        {
            throw new InvalidOperationException(
                "The immutable process strategy binding does not match the registered Standard driver package.");
        }

        return ValueTask.FromResult<IProcessStrategyFactory>(new StandardProcessAdapterStrategyFactory(executionDriver));
    }
}
