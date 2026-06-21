using CanDoItAll.Processes.Abstractions;

namespace CanDoItAll.Processes.Drivers.Abstractions;

public sealed record ProcessCapabilityRequest(
    IReadOnlySet<CapabilityTag> RequiredCapabilityTags,
    IReadOnlySet<CapabilityTag> OptionalCapabilityTags,
    IReadOnlySet<CapabilityTag> ExclusiveCapabilityTags);

public sealed record ProcessCapabilityMatchResult(
    bool Succeeded,
    IReadOnlyList<ProcessDriverDescriptor> OrderedDrivers,
    IReadOnlySet<CapabilityTag> MissingCapabilityTags,
    IReadOnlyList<ProcessDriverConflict> Conflicts,
    IReadOnlyList<string> Diagnostics);

public sealed class ProcessDriverCatalog
{
    private readonly IReadOnlyList<ProcessDriverPackage> packages;
    private readonly Dictionary<DriverId, ProcessDriverPackage> packagesById;

    public ProcessDriverCatalog(IReadOnlyList<ProcessDriverPackage> packages)
    {
        ArgumentNullException.ThrowIfNull(packages);

        this.packages = packages;
        packagesById = new Dictionary<DriverId, ProcessDriverPackage>();
        foreach (var package in packages)
        {
            if (!packagesById.TryAdd(package.Descriptor.DriverId, package))
            {
                throw new ArgumentException(
                    $"Duplicate driver id '{package.Descriptor.DriverId}'.",
                    nameof(packages));
            }
        }
    }

    public ProcessCapabilityMatchResult Match(ProcessCapabilityRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var selected = new Dictionary<DriverId, ProcessDriverPackage>();
        var requestedTags = request.RequiredCapabilityTags
            .Concat(request.OptionalCapabilityTags)
            .ToHashSet();

        foreach (var package in packages)
        {
            if (package.Descriptor.CapabilityTags.Overlaps(requestedTags))
            {
                selected.TryAdd(package.Descriptor.DriverId, package);
            }
        }

        var missing = request.RequiredCapabilityTags
            .Where(tag => !selected.Values.Any(package => package.Descriptor.CapabilityTags.Contains(tag)))
            .ToHashSet();

        var conflicts = new List<ProcessDriverConflict>();
        AddDependencyDrivers(selected, conflicts);
        AddDeclaredConflicts(selected.Values, conflicts);
        AddExclusiveCapabilityConflicts(selected.Values, request.ExclusiveCapabilityTags, conflicts);

        var orderedDrivers = OrderDrivers(selected.Values, conflicts)
            .Select(package => package.Descriptor)
            .ToArray();

        var diagnostics = new List<string>();
        if (missing.Count > 0)
        {
            diagnostics.Add("Required capabilities are missing.");
        }

        if (conflicts.Count > 0)
        {
            diagnostics.Add("Driver conflicts were detected.");
        }

        return new ProcessCapabilityMatchResult(
            missing.Count == 0 && conflicts.Count == 0,
            orderedDrivers,
            missing,
            conflicts,
            diagnostics);
    }

    private void AddDependencyDrivers(
        IDictionary<DriverId, ProcessDriverPackage> selected,
        ICollection<ProcessDriverConflict> conflicts)
    {
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var package in selected.Values.ToArray())
            {
                foreach (var dependency in package.Descriptor.Dependencies)
                {
                    if (selected.ContainsKey(dependency.DriverId))
                    {
                        continue;
                    }

                    if (packagesById.TryGetValue(dependency.DriverId, out var dependencyPackage))
                    {
                        selected.Add(dependency.DriverId, dependencyPackage);
                        changed = true;
                    }
                    else
                    {
                        conflicts.Add(new ProcessDriverConflict(
                            dependency.DriverId,
                            null,
                            $"Driver '{package.Descriptor.DriverId}' requires missing driver '{dependency.DriverId}'."));
                    }
                }
            }
        }
    }

    private static void AddDeclaredConflicts(
        IEnumerable<ProcessDriverPackage> selected,
        ICollection<ProcessDriverConflict> conflicts)
    {
        var selectedIds = selected.Select(package => package.Descriptor.DriverId).ToHashSet();
        foreach (var package in selected)
        {
            foreach (var conflict in package.Descriptor.Conflicts)
            {
                if (conflict.DriverId is { } driverId && selectedIds.Contains(driverId))
                {
                    conflicts.Add(conflict);
                }
            }
        }
    }

    private static void AddExclusiveCapabilityConflicts(
        IEnumerable<ProcessDriverPackage> selected,
        IReadOnlySet<CapabilityTag> exclusiveCapabilityTags,
        ICollection<ProcessDriverConflict> conflicts)
    {
        foreach (var tag in exclusiveCapabilityTags)
        {
            var providers = selected
                .Where(package => package.Descriptor.CapabilityTags.Contains(tag))
                .Select(package => package.Descriptor.DriverId)
                .ToArray();
            if (providers.Length > 1)
            {
                conflicts.Add(new ProcessDriverConflict(
                    null,
                    tag,
                    $"Exclusive capability '{tag}' is provided by multiple drivers."));
            }
        }
    }

    private static IReadOnlyList<ProcessDriverPackage> OrderDrivers(
        IEnumerable<ProcessDriverPackage> selected,
        ICollection<ProcessDriverConflict> conflicts)
    {
        var selectedById = selected.ToDictionary(package => package.Descriptor.DriverId);
        var ordered = new List<ProcessDriverPackage>();
        var visiting = new HashSet<DriverId>();
        var visited = new HashSet<DriverId>();

        foreach (var package in selectedById.Values.OrderBy(package => package.Descriptor.Layer).ThenBy(package => package.Descriptor.DriverId.Value, StringComparer.Ordinal))
        {
            Visit(package, selectedById, ordered, visiting, visited, conflicts);
        }

        return ordered;
    }

    private static void Visit(
        ProcessDriverPackage package,
        IReadOnlyDictionary<DriverId, ProcessDriverPackage> selectedById,
        ICollection<ProcessDriverPackage> ordered,
        ISet<DriverId> visiting,
        ISet<DriverId> visited,
        ICollection<ProcessDriverConflict> conflicts)
    {
        if (visited.Contains(package.Descriptor.DriverId))
        {
            return;
        }

        if (!visiting.Add(package.Descriptor.DriverId))
        {
            conflicts.Add(new ProcessDriverConflict(
                package.Descriptor.DriverId,
                null,
                $"Driver dependency cycle detected at '{package.Descriptor.DriverId}'."));
            return;
        }

        foreach (var dependency in package.Descriptor.Dependencies)
        {
            if (selectedById.TryGetValue(dependency.DriverId, out var dependencyPackage))
            {
                Visit(dependencyPackage, selectedById, ordered, visiting, visited, conflicts);
            }
        }

        visiting.Remove(package.Descriptor.DriverId);
        visited.Add(package.Descriptor.DriverId);
        ordered.Add(package);
    }
}
