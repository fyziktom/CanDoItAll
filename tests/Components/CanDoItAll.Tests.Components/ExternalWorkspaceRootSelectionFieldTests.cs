using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class ExternalWorkspaceRootSelectionFieldTests : IDisposable
{
    private const string DataTestId = "external-root-selection";

    private readonly string temporaryRoot = TestFileSystem.CreateTemporaryRoot("external-root-selection-field");
    private readonly ExternalTargetPathRegistryFactory pathRegistryFactory = new();

    [Fact]
    public void Absolute_path_round_trips_through_persisted_bindings_in_a_fresh_component_registry()
    {
        using var context = CreateContext();
        ExternalWorkspaceRootSelection? selection = null;
        var externalRoot = CreateDirectory("round-trip-root");
        var cut = RenderField(context, [], [], value => selection = value);

        cut.Find($"[data-testid='{DataTestId}-input']").Input(externalRoot);
        cut.Find($"[data-testid='{DataTestId}-add']").Click();

        var saved = Assert.IsType<ExternalWorkspaceRootSelection>(selection);
        var alias = Assert.Single(saved.AllowedAliases);
        var binding = Assert.Single(saved.RootBindings);
        Assert.Matches("^external-target/v1/[0-9a-f]{24}$", alias);
        Assert.Contains(binding.RootId, alias, StringComparison.Ordinal);
        Assert.Equal(
            string.Empty,
            cut.Find($"[data-testid='{DataTestId}-input']").GetAttribute("value"));

        var reloaded = RenderField(context, saved.AllowedAliases, saved.RootBindings);
        var rowTestId = BuildRowTestId(alias);
        Assert.Equal(
            externalRoot,
            reloaded.Find($"[data-testid='{rowTestId}-primary']").TextContent.Trim());
        Assert.Equal(
            alias,
            reloaded.Find($"[data-testid='{rowTestId}-identifier']").TextContent.Trim());
        Assert.Empty(reloaded.FindAll($"[data-testid='{rowTestId}-status']"));
    }

    [Fact]
    public void Invalid_relative_path_keeps_the_input_and_reports_inline_validation()
    {
        using var context = CreateContext();
        ExternalWorkspaceRootSelection? selection = null;
        var cut = RenderField(context, [], [], value => selection = value);
        const string invalidPath = "relative/workspace/root";

        cut.Find($"[data-testid='{DataTestId}-input']").Input(invalidPath);
        cut.Find($"[data-testid='{DataTestId}-add']").Click();

        Assert.Null(selection);
        Assert.Equal(
            invalidPath,
            cut.Find($"[data-testid='{DataTestId}-input']").GetAttribute("value"));
        var validation = cut.Find($"[data-testid='{DataTestId}-validation']");
        Assert.Contains("absolute path", validation.TextContent, StringComparison.OrdinalIgnoreCase);
        Assert.Equal($"{DataTestId}-validation-message", validation.Id);
        Assert.Equal(
            $"{DataTestId}-hint {DataTestId}-validation-message",
            cut.Find($"[data-testid='{DataTestId}-input']").GetAttribute("aria-describedby"));
        Assert.Empty(cut.FindAll($"[data-testid^='{DataTestId}-row-']"));
    }

    [Fact]
    public void Removing_a_root_prunes_its_binding_and_preserves_other_roots()
    {
        using var context = CreateContext();
        var sourceRegistry = pathRegistryFactory.Create([]);
        Assert.True(sourceRegistry.TryCreateAlias(CreateDirectory("first-root"), out var firstAlias));
        Assert.True(sourceRegistry.TryCreateAlias(CreateDirectory("second-root"), out var secondAlias));
        var bindings = sourceRegistry.ExportBindings([firstAlias, secondAlias]);
        ExternalWorkspaceRootSelection? selection = null;
        var cut = RenderField(
            context,
            [firstAlias, secondAlias],
            bindings,
            value => selection = value);

        cut.Find($"[data-testid='{BuildRowTestId(firstAlias)}-remove']").Click();

        var saved = Assert.IsType<ExternalWorkspaceRootSelection>(selection);
        Assert.Equal([secondAlias], saved.AllowedAliases);
        var remainingBinding = Assert.Single(saved.RootBindings);
        Assert.Contains(remainingBinding.RootId, secondAlias, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll($"[data-testid='{BuildRowTestId(firstAlias)}']"));
        Assert.Single(cut.FindAll($"[data-testid='{BuildRowTestId(secondAlias)}']"));
    }

    [Fact]
    public void Canonical_alias_without_a_binding_remains_visible_as_unresolved()
    {
        using var context = CreateContext();
        const string alias = "external-target/v1/111111111111111111111111";

        var cut = RenderField(context, [alias], []);

        var rowTestId = BuildRowTestId(alias);
        Assert.Equal(
            "Path unavailable on this host",
            cut.Find($"[data-testid='{rowTestId}-primary'] strong").TextContent.Trim());
        Assert.Equal(
            alias,
            cut.Find($"[data-testid='{rowTestId}-identifier']").TextContent.Trim());
        Assert.Contains(
            "Unresolved",
            cut.Find($"[data-testid='{rowTestId}-status']").TextContent,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Canonical_alias_can_be_added_once_and_duplicate_input_is_retained_with_validation()
    {
        using var context = CreateContext();
        const string alias = "external-target/v1/222222222222222222222222";
        var selections = new List<ExternalWorkspaceRootSelection>();
        var cut = RenderField(context, [], [], selections.Add);

        cut.Find($"[data-testid='{DataTestId}-input']").Input(alias);
        cut.Find($"[data-testid='{DataTestId}-add']").Click();

        var firstSelection = Assert.Single(selections);
        Assert.Equal([alias], firstSelection.AllowedAliases);
        Assert.Empty(firstSelection.RootBindings);
        Assert.Contains(
            "Path unavailable on this host",
            cut.Find($"[data-testid='{BuildRowTestId(alias)}']").TextContent,
            StringComparison.Ordinal);

        cut.Find($"[data-testid='{DataTestId}-input']").Input(alias);
        cut.Find($"[data-testid='{DataTestId}-add']").Click();

        Assert.Single(selections);
        Assert.Equal(alias, cut.Find($"[data-testid='{DataTestId}-input']").GetAttribute("value"));
        Assert.Contains(
            "already selected",
            cut.Find($"[data-testid='{DataTestId}-validation']").TextContent,
            StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        TestFileSystem.DeleteDirectoryWithRetry(temporaryRoot);
    }

    private BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddSingleton<IExternalTargetPathRegistryFactory>(pathRegistryFactory);
        return context;
    }

    private static IRenderedComponent<ExternalWorkspaceRootSelectionField> RenderField(
        BunitContext context,
        IReadOnlyList<string> aliases,
        IReadOnlyList<ExternalTargetRootBinding> bindings,
        Action<ExternalWorkspaceRootSelection>? selectionChanged = null)
    {
        return context.Render<ExternalWorkspaceRootSelectionField>(parameters =>
        {
            parameters
                .Add(component => component.AllowedAliases, aliases)
                .Add(component => component.RootBindings, bindings)
                .Add(component => component.DataTestId, DataTestId);
            if (selectionChanged is not null)
            {
                parameters.Add(component => component.SelectionChanged, selectionChanged);
            }
        });
    }

    private string CreateDirectory(string name)
    {
        var path = Path.Combine(temporaryRoot, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private static string BuildRowTestId(string alias)
    {
        var key = string.Join(
            '-',
            new string(alias
                .ToLowerInvariant()
                .Select(character => char.IsAsciiLetterOrDigit(character) ? character : '-')
                .ToArray())
                .Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return $"{DataTestId}-row-{key}";
    }
}
