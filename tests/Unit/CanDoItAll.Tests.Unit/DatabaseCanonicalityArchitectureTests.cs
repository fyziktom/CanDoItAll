using System.Reflection;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Tests.Support;
using CanDoItAll.Tests.Unit.Architecture;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;
using GlobalContextFactoryAlias = Microsoft.EntityFrameworkCore.IDbContextFactory<CanDoItAll.Infrastructure.Persistence.AppDbContext>;

namespace CanDoItAll.Tests.Unit.Infrastructure;

public sealed class DatabaseCanonicalityArchitectureTests(ITestOutputHelper output)
{
    // Explicit maintenance boundaries that may create a profile-specific complete AppDbContext (migrations, transfer,
    // schema health). An entry that is no longer used is reported, not failed: removing a consumer is allowed.
    private static readonly string[] ProfileFactoryMaintenanceFiles =
    [
        "src/Foundation/CanDoItAll.Infrastructure/Persistence/SwitchableAppDbContextFactory.cs",
        "src/Foundation/CanDoItAll.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs",
        "src/Foundation/CanDoItAll.Infrastructure/ControlPlane/DatabaseTransferService.cs",
        "src/App/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs",
        "src/Modules/CanDoItAll.Modules.Workspace/Database/DatabaseProfileWorkspaceService.cs",
        "src/Foundation/CanDoItAll.Infrastructure/ControlPlane/DatabaseTransferOperationRunner.cs"
    ];

    // Product-module types that may reference the complete AppDbContext, each with its owning maintenance reason.
    private static readonly GlobalContextMaintenanceException[] GlobalContextMaintenanceExceptions =
    [
        new(
            "CanDoItAll.Modules.Workspace",
            "CanDoItAll.Modules.Workspace.DatabaseProfileWorkspaceService",
            "Profile schema health inspection opens the complete migration model through IProfileAppDbContextFactory.")
    ];

    [Fact]
    public void Profile_specific_db_context_factory_is_limited_to_explicit_maintenance_boundaries()
    {
        var root = TestRepositoryRoot.Find();
        var allowed = ProfileFactoryMaintenanceFiles
            .Select(path => NormalizePath(Path.Combine(root, path)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var actual = Directory
            .EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .Where(path => File.ReadAllText(path).Contains("IProfileAppDbContextFactory", StringComparison.Ordinal))
            .Select(NormalizePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        ReportStale("profile-specific AppDbContext factory", allowed.Except(actual, StringComparer.OrdinalIgnoreCase));
        var unexpected = actual.Except(allowed, StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Assert.True(
            unexpected.Length == 0,
            "The profile-specific AppDbContext factory is limited to explicit maintenance boundaries; unexpected users: " +
            string.Join(", ", unexpected));
    }

    [Fact]
    public void Product_modules_do_not_reach_the_global_app_db_context()
    {
        // The pooled AppDbContext factory exists for migrations and bounded maintenance composition only. Module
        // services own their persistence through their own contexts and contracts; a module that references the global
        // context in compiled code (any identifier, alias, qualification, lambda or service lookup) would reacquire
        // every foreign entity as a back door.
        var modules = LoadProductModuleAssemblies(TestRepositoryRoot.Find());
        var evaluation = EvaluateGlobalContextAccess(
            modules.SelectMany(TopLevelTypes),
            GlobalContextMaintenanceExceptions);

        ReportStale("global AppDbContext", evaluation.StaleExceptions.Select(exception => exception.TypeName));
        Assert.True(
            evaluation.Violations.Count == 0,
            "Product modules must not reference the global AppDbContext outside maintained exceptions: " +
            string.Join(", ", evaluation.Violations));
    }

    [Fact]
    public void Global_context_detection_does_not_depend_on_names_aliases_or_trivia()
    {
        var evaluation = EvaluateGlobalContextAccess(
            [
                typeof(RenamedFieldFixture),
                typeof(AliasedFactoryFixture),
                typeof(ServiceLookupFixture),
                typeof(LambdaCaptureFixture),
                typeof(CleanFixture)
            ],
            []);

        Assert.Equal(
            new[]
            {
                nameof(AliasedFactoryFixture),
                nameof(LambdaCaptureFixture),
                nameof(RenamedFieldFixture),
                nameof(ServiceLookupFixture)
            },
            evaluation.Violations.Select(violation => violation.Split('+').Last()).Order(StringComparer.Ordinal));
        Assert.Empty(evaluation.StaleExceptions);
    }

    [Fact]
    public void Maintenance_exceptions_cover_only_their_owner_and_unused_ones_do_not_fail()
    {
        var owner = MaintenanceExceptionFor(typeof(RenamedFieldFixture));
        var unused = MaintenanceExceptionFor(typeof(CleanFixture));

        var evaluation = EvaluateGlobalContextAccess(
            [typeof(RenamedFieldFixture), typeof(ServiceLookupFixture), typeof(CleanFixture)],
            [owner, unused]);

        Assert.Equal(nameof(ServiceLookupFixture), Assert.Single(evaluation.Violations).Split('+').Last());
        Assert.Equal(unused, Assert.Single(evaluation.StaleExceptions));
    }

    private void ReportStale(string boundary, IEnumerable<string> stale)
    {
        foreach (var entry in stale.Order(StringComparer.OrdinalIgnoreCase))
        {
            output.WriteLine($"Stale {boundary} exception (no longer used; remove it from the list): {entry}");
        }
    }

    private static GlobalContextEvaluation EvaluateGlobalContextAccess(
        IEnumerable<Type> candidates,
        IReadOnlyCollection<GlobalContextMaintenanceException> exceptions)
    {
        var violations = new List<string>();
        var used = new HashSet<GlobalContextMaintenanceException>();
        foreach (var candidate in candidates)
        {
            if (!CompiledTypeReferences.ReferencedTypes(candidate).Contains(typeof(AppDbContext)))
            {
                continue;
            }

            var assemblyName = candidate.Assembly.GetName().Name!;
            var exception = exceptions.FirstOrDefault(entry =>
                string.Equals(entry.AssemblyName, assemblyName, StringComparison.Ordinal) &&
                string.Equals(entry.TypeName, candidate.FullName, StringComparison.Ordinal));
            if (exception is null)
            {
                violations.Add($"{assemblyName}:{candidate.FullName}");
            }
            else
            {
                used.Add(exception);
            }
        }

        return new(
            violations.Order(StringComparer.Ordinal).ToArray(),
            exceptions.Where(exception => !used.Contains(exception)).ToArray());
    }

    private static IReadOnlyList<Assembly> LoadProductModuleAssemblies(string root)
    {
        var projectNames = Directory
            .EnumerateDirectories(Path.Combine(root, "src", "Modules"))
            .Where(directory => File.Exists(Path.Combine(directory, Path.GetFileName(directory) + ".csproj")))
            .Select(Path.GetFileName)
            .OfType<string>()
            .Order(StringComparer.Ordinal)
            .ToArray();
        var assemblies = new List<Assembly>();
        var missing = new List<string>();
        foreach (var projectName in projectNames)
        {
            try
            {
                assemblies.Add(Assembly.Load(new AssemblyName(projectName)));
            }
            catch (FileNotFoundException)
            {
                missing.Add(projectName);
            }
        }

        Assert.True(
            missing.Count == 0,
            "Every product module must be compiled into this guard's scope; reference it from the unit test project: " +
            string.Join(", ", missing));
        Assert.NotEmpty(assemblies);
        return assemblies;
    }

    private static IEnumerable<Type> TopLevelTypes(Assembly assembly)
    {
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            throw new InvalidOperationException(
                $"Types of {assembly.GetName().Name} could not be loaded for the global-context guard: " +
                string.Join("; ", exception.LoaderExceptions.OfType<Exception>().Select(error => error.Message).Distinct().Take(5)),
                exception);
        }

        return types.Where(type => type.DeclaringType is null);
    }

    private static GlobalContextMaintenanceException MaintenanceExceptionFor(Type owner)
        => new(owner.Assembly.GetName().Name!, owner.FullName!, "fixture");

    private static bool IsBuildOutput(string path)
    {
        var separators = new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
        return path.Split(separators).Any(segment =>
            string.Equals(segment, "bin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "obj", StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizePath(string path)
        => Path.GetFullPath(path).Replace('\\', '/');

    private sealed record GlobalContextMaintenanceException(string AssemblyName, string TypeName, string Reason);

    private sealed record GlobalContextEvaluation(
        IReadOnlyList<string> Violations,
        IReadOnlyList<GlobalContextMaintenanceException> StaleExceptions);

#pragma warning disable CS0169, CS0649
    private sealed class RenamedFieldFixture
    {
        private readonly global::CanDoItAll.Infrastructure.Persistence.AppDbContext
            somethingEntirelyDifferent = null!;

        public object Read() => somethingEntirelyDifferent;
    }

    private sealed class AliasedFactoryFixture(GlobalContextFactoryAlias factory)
    {
        public object Use() => factory;
    }

    private sealed class ServiceLookupFixture
    {
        public object? Resolve(IServiceProvider services) => services.GetService(typeof(AppDbContext));
    }

    private sealed class LambdaCaptureFixture
    {
        public Func<IServiceProvider, object?> Build() => services => services.GetService(typeof(AppDbContext));
    }

    private sealed class CleanFixture
    {
        public string Describe(DbContext context) => context.GetType().Name;
    }
#pragma warning restore CS0169, CS0649
}
