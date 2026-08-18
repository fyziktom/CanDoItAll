using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Modules.Workspace.Pages.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.Workspace;

public sealed class StorageCatalogSelectionComponentsTests
{
    private static readonly Guid AlphaId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid BetaId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid MissingId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public async Task Dialog_loads_the_current_catalog_on_every_open()
    {
        var source = new RecordingStorageCatalogSelectionSource
        {
            Catalogs = [CreateCatalog(AlphaId, "Alpha catalog")]
        };
        using var context = CreateContext(source);
        var host = context.Render<DialogHost>();

        var firstResult = OpenDialog(context, []);
        host.WaitForElement($"[data-testid='storage-dialog-option-{AlphaId:N}']");
        Assert.Contains("Alpha catalog", host.Markup, StringComparison.Ordinal);
        host.Find("[data-testid='storage-dialog-cancel']").Click();
        Assert.Null(await firstResult.WaitAsync(TimeSpan.FromSeconds(2)));

        source.Catalogs = [CreateCatalog(BetaId, "Beta catalog")];
        var secondResult = OpenDialog(context, []);
        host.WaitForElement($"[data-testid='storage-dialog-option-{BetaId:N}']");
        Assert.Contains("Beta catalog", host.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Alpha catalog", host.Markup, StringComparison.Ordinal);
        host.Find("[data-testid='storage-dialog-cancel']").Click();
        Assert.Null(await secondResult.WaitAsync(TimeSpan.FromSeconds(2)));

        Assert.Equal(2, source.CallCount);
    }

    [Fact]
    public async Task Dialog_preserves_missing_and_disabled_selected_ids_until_removed()
    {
        var source = new RecordingStorageCatalogSelectionSource
        {
            Catalogs = [CreateCatalog(AlphaId, "Offline archive", isEnabled: false)]
        };
        using var context = CreateContext(source);
        var host = context.Render<DialogHost>();

        var resultTask = OpenDialog(context, [AlphaId, MissingId]);
        var disabledSelection = host.WaitForElement(
            $"[data-testid='storage-dialog-option-{AlphaId:N}']");
        var missingSelection = host.Find(
            $"[data-testid='storage-dialog-option-{MissingId:N}']");

        Assert.False(disabledSelection.HasAttribute("disabled"));
        Assert.Equal("true", disabledSelection.GetAttribute("aria-pressed"));
        Assert.Equal("true", missingSelection.GetAttribute("aria-pressed"));
        Assert.Contains(
            "Disabled",
            host.Find($"[data-testid='storage-dialog-option-{AlphaId:N}-shell']").TextContent,
            StringComparison.Ordinal);
        Assert.Contains(
            "Missing",
            host.Find($"[data-testid='storage-dialog-option-{MissingId:N}-shell']").TextContent,
            StringComparison.Ordinal);

        missingSelection.Click();
        host.WaitForAssertion(() => Assert.Empty(host.FindAll(
            $"[data-testid='storage-dialog-option-{MissingId:N}']")));

        disabledSelection.Click();
        host.WaitForAssertion(() =>
        {
            var removedDisabledSelection = host.Find(
                $"[data-testid='storage-dialog-option-{AlphaId:N}']");
            Assert.True(removedDisabledSelection.HasAttribute("disabled"));
            Assert.Equal("false", removedDisabledSelection.GetAttribute("aria-pressed"));
        });

        host.Find("[data-testid='storage-dialog-apply']").Click();
        var result = Assert.IsType<StorageCatalogSelectionDialogResult>(
            await resultTask.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Empty(result.SelectedCatalogIds);
    }

    [Fact]
    public async Task Dialog_blocks_new_disabled_ids_and_allows_read_only_ids()
    {
        var source = new RecordingStorageCatalogSelectionSource
        {
            Catalogs =
            [
                CreateCatalog(AlphaId, "Disabled catalog", isEnabled: false),
                CreateCatalog(BetaId, "Read-only catalog", isReadOnly: true)
            ]
        };
        using var context = CreateContext(source);
        var host = context.Render<DialogHost>();

        var resultTask = OpenDialog(context, []);
        var disabledOption = host.WaitForElement(
            $"[data-testid='storage-dialog-option-{AlphaId:N}']");
        var readOnlyOption = host.Find(
            $"[data-testid='storage-dialog-option-{BetaId:N}']");

        Assert.True(disabledOption.HasAttribute("disabled"));
        Assert.Contains(
            "This storage catalog is disabled and cannot be newly selected.",
            host.Markup,
            StringComparison.Ordinal);
        Assert.False(readOnlyOption.HasAttribute("disabled"));
        Assert.Contains(
            "Read only",
            host.Find($"[data-testid='storage-dialog-option-{BetaId:N}-shell']").TextContent,
            StringComparison.Ordinal);

        host.Find("[data-testid='storage-dialog-picker-search']")
            .Input(BetaId.ToString("D"));
        Assert.Empty(host.FindAll($"[data-testid='storage-dialog-option-{AlphaId:N}']"));
        readOnlyOption = host.Find($"[data-testid='storage-dialog-option-{BetaId:N}']");
        readOnlyOption.Click();

        host.Find("[data-testid='storage-dialog-apply']").Click();
        var result = Assert.IsType<StorageCatalogSelectionDialogResult>(
            await resultTask.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Equal([BetaId], result.SelectedCatalogIds);
    }

    [Fact]
    public async Task Cancel_discards_staged_selection()
    {
        var source = new RecordingStorageCatalogSelectionSource
        {
            Catalogs = [CreateCatalog(AlphaId, "Alpha catalog")]
        };
        using var context = CreateContext(source);
        var host = context.Render<DialogHost>();

        var resultTask = OpenDialog(context, []);
        host.WaitForElement($"[data-testid='storage-dialog-option-{AlphaId:N}']").Click();
        Assert.Equal(
            "true",
            host.Find($"[data-testid='storage-dialog-option-{AlphaId:N}']")
                .GetAttribute("aria-pressed"));

        host.Find("[data-testid='storage-dialog-cancel']").Click();

        Assert.Null(await resultTask.WaitAsync(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public async Task Dialog_retry_recovers_from_a_catalog_load_failure()
    {
        var source = new RecordingStorageCatalogSelectionSource
        {
            Catalogs = [CreateCatalog(AlphaId, "Recovered catalog")],
            FailuresRemaining = 1
        };
        using var context = CreateContext(source);
        var host = context.Render<DialogHost>();

        var resultTask = OpenDialog(context, []);
        var retry = host.WaitForElement("[data-testid='storage-dialog-retry']");
        Assert.Contains("Temporary catalog failure", host.Markup, StringComparison.Ordinal);
        Assert.True(host.Find("[data-testid='storage-dialog-apply']").HasAttribute("disabled"));

        retry.Click();
        host.WaitForElement($"[data-testid='storage-dialog-option-{AlphaId:N}']").Click();
        host.Find("[data-testid='storage-dialog-apply']").Click();

        var result = Assert.IsType<StorageCatalogSelectionDialogResult>(
            await resultTask.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Equal([AlphaId], result.SelectedCatalogIds);
        Assert.Equal(2, source.CallCount);
    }

    [Fact]
    public void Field_preserves_selection_and_disables_editing_when_allow_all_is_enabled()
    {
        var source = new RecordingStorageCatalogSelectionSource();
        using var context = CreateContext(source);
        IReadOnlyList<Guid>? changedValue = null;

        var cut = context.Render<StorageCatalogSelectionField>(parameters => parameters
            .Add(component => component.Value, [MissingId])
            .Add(component => component.ValueChanged, value => changedValue = value)
            .Add(component => component.AllowAll, true)
            .Add(component => component.DataTestId, "storage-field"));

        Assert.True(cut.Find("[data-testid='storage-field-choose']").HasAttribute("disabled"));
        Assert.Contains(MissingId.ToString("D"), cut.Markup, StringComparison.Ordinal);
        Assert.True(cut.Find(
                $"[data-testid='storage-field-selected-row-{MissingId:N}-remove']")
            .HasAttribute("disabled"));
        Assert.Null(changedValue);
        Assert.Equal(1, source.CallCount);
    }

    [Fact]
    public void Field_resolves_a_saved_catalog_id_to_its_readable_name_without_opening_the_chooser()
    {
        var source = new RecordingStorageCatalogSelectionSource
        {
            Catalogs = [CreateCatalog(AlphaId, "Customer documents")]
        };
        using var context = CreateContext(source);

        var cut = context.Render<StorageCatalogSelectionField>(parameters => parameters
            .Add(component => component.Value, [AlphaId])
            .Add(component => component.DataTestId, "storage-field"));

        cut.WaitForAssertion(() =>
        {
            var row = cut.Find($"[data-testid='storage-field-selected-row-{AlphaId:N}']");
            Assert.Contains("Customer documents", row.TextContent, StringComparison.Ordinal);
            Assert.Contains(AlphaId.ToString("D"), row.TextContent, StringComparison.Ordinal);
            Assert.Contains("Enabled", row.TextContent, StringComparison.Ordinal);
        });
        Assert.Equal(1, source.CallCount);
    }

    [Fact]
    public void Field_resolves_catalog_details_when_selected_ids_arrive_after_initial_render()
    {
        var source = new RecordingStorageCatalogSelectionSource
        {
            Catalogs = [CreateCatalog(AlphaId, "Later customer documents")]
        };
        using var context = CreateContext(source);
        var cut = context.Render<StorageCatalogSelectionField>(parameters => parameters
            .Add(component => component.Value, [])
            .Add(component => component.DataTestId, "storage-field"));

        Assert.Equal(0, source.CallCount);
        Assert.Contains(
            "No storage catalogs selected",
            cut.Markup,
            StringComparison.Ordinal);

        cut.Render(parameters => parameters
            .Add(component => component.Value, [AlphaId])
            .Add(component => component.DataTestId, "storage-field"));

        cut.WaitForAssertion(() =>
        {
            var row = cut.Find($"[data-testid='storage-field-selected-row-{AlphaId:N}']");
            Assert.Contains("Later customer documents", row.TextContent, StringComparison.Ordinal);
            Assert.Contains(AlphaId.ToString("D"), row.TextContent, StringComparison.Ordinal);
            Assert.DoesNotContain("Not loaded", row.TextContent, StringComparison.Ordinal);
        });
        Assert.Equal(1, source.CallCount);
    }

    private static BunitContext CreateContext(RecordingStorageCatalogSelectionSource source)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddSingleton<IStorageCatalogSelectionSource>(source);
        return context;
    }

    private static Task<object?> OpenDialog(
        BunitContext context,
        IReadOnlyList<Guid> selectedCatalogIds)
    {
        return context.Services.GetRequiredService<DialogService>()
            .OpenAsync<StorageCatalogSelectionDialog>(
                "Choose storage catalogs",
                new Dictionary<string, object?>
                {
                    [nameof(StorageCatalogSelectionDialog.SelectedCatalogIds)] = selectedCatalogIds,
                    [nameof(StorageCatalogSelectionDialog.DataTestId)] = "storage-dialog"
                },
                new DialogOptions
                {
                    TestId = "storage-dialog-shell"
                });
    }

    private static StorageCatalogSummary CreateCatalog(
        Guid id,
        string name,
        bool isEnabled = true,
        bool isReadOnly = false)
    {
        return new StorageCatalogSummary(
            id,
            name,
            StorageProviderKind.FileSystem,
            StorageConnectionMode.Local,
            $"C:\\catalogs\\{name}",
            0,
            isEnabled,
            false,
            isReadOnly,
            StorageCapability.Read | StorageCapability.Write,
            StorageHealthStatus.Healthy,
            DateTimeOffset.UtcNow,
            "Available");
    }

    private sealed class RecordingStorageCatalogSelectionSource : IStorageCatalogSelectionSource
    {
        public IReadOnlyList<StorageCatalogSummary> Catalogs { get; set; } = [];

        public int CallCount { get; private set; }

        public int FailuresRemaining { get; set; }

        public Task<IReadOnlyList<StorageCatalogSummary>> ListAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            if (FailuresRemaining > 0)
            {
                FailuresRemaining--;
                throw new InvalidOperationException("Temporary catalog failure.");
            }

            return Task.FromResult(Catalogs);
        }
    }
}
