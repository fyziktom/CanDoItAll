using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.CrmHr.UI.Parties;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.CrmHr;

public sealed class PartyImportExportActionsTests {
    [Fact]
    public async Task Changing_csv_after_a_preview_requires_a_new_preview() {
        using var context = CreateContext();
        var cut = Render(context);
        cut.Find("[data-testid='crmhr-import-textarea']").Input("First CSV input");
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-import-preview-button']").ClickAsync(new()));
        Assert.False(cut.Find("[data-testid='crmhr-import-apply-button']").HasAttribute("disabled"));

        cut.Find("[data-testid='crmhr-import-textarea']").Input("Changed CSV input");

        Assert.True(cut.Find("[data-testid='crmhr-import-apply-button']").HasAttribute("disabled"));
        Assert.Empty(cut.FindAll("[data-testid='crmhr-import-row']"));
    }

    [Fact]
    public async Task Accepted_import_preserves_later_csv_and_requires_another_preview() {
        using var context = CreateContext();
        var completion = new TaskCompletionSource<Result<int>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var cut = Render(context, apply: _ => {
            calls++;
            return completion.Task;
        });
        cut.Find("[data-testid='crmhr-import-textarea']").Input("Submitted CSV input");
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-import-preview-button']").ClickAsync(new()));
        var applying = cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-import-apply-button']").ClickAsync(new()));
        cut.WaitForAssertion(() => Assert.Equal(1, calls));
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-import-textarea']").Input("Later CSV input"));

        completion.SetResult(Result<int>.Success(1));
        await applying;

        Assert.Equal("Later CSV input", cut.Find("[data-testid='crmhr-import-textarea']").GetAttribute("value"));
        Assert.True(cut.Find("[data-testid='crmhr-import-apply-button']").HasAttribute("disabled"));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task A_late_preview_cannot_replace_the_preview_for_changed_csv() {
        using var context = CreateContext();
        var completion = new TaskCompletionSource<Result<PartyCsvImportPreviewModel>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cut = Render(context, preview: _ => completion.Task);
        cut.Find("[data-testid='crmhr-import-textarea']").Input("Old CSV input");
        var reading = cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-import-preview-button']").ClickAsync(new()));
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-import-textarea']").Input("Current CSV input"));

        completion.SetResult(Preview("Old preview"));
        await reading;

        Assert.Equal("Current CSV input", cut.Find("[data-testid='crmhr-import-textarea']").GetAttribute("value"));
        Assert.Empty(cut.FindAll("[data-testid='crmhr-import-row']"));
        Assert.True(cut.Find("[data-testid='crmhr-import-apply-button']").HasAttribute("disabled"));
    }

    [Fact]
    public async Task An_unknown_import_keeps_csv_and_blocks_apply_until_an_explicit_preview() {
        using var context = CreateContext();
        var cut = Render(context, apply: _ => Task.FromException<Result<int>>(new IOException("Uncertain command response")));
        cut.Find("[data-testid='crmhr-import-textarea']").Input("Uncertain CSV input");
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-import-preview-button']").ClickAsync(new()));

        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-import-apply-button']").ClickAsync(new()));

        Assert.Equal("Uncertain CSV input", cut.Find("[data-testid='crmhr-import-textarea']").GetAttribute("value"));
        Assert.True(cut.Find("[data-testid='crmhr-import-apply-button']").HasAttribute("disabled"));
        Assert.Contains("could not be confirmed", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    private static IRenderedComponent<PartyImportExportActions> Render(BunitContext context,
        Func<IReadOnlyList<PartyCsvImportPreviewRowModel>, Task<Result<int>>>? apply = null,
        Func<string, Task<Result<PartyCsvImportPreviewModel>>>? preview = null) =>
        context.Render<PartyImportExportActions>(parameters => parameters
            .Add(component => component.ExportCsv, (_, _) => Task.FromResult("Current export"))
            .Add(component => component.PreviewImport, preview ?? (csv => Task.FromResult(Preview(csv))))
            .Add(component => component.ApplyImport, apply ?? (_ => Task.FromResult(Result<int>.Success(1)))));

    private static Result<PartyCsvImportPreviewModel> Preview(string name) => Result<PartyCsvImportPreviewModel>.Success(new() {
        Rows = [new() { RowNumber = 2, Party = new() { DisplayName = name }, CanImport = true }]
    });

    private static BunitContext CreateContext() {
        var context = new BunitContext();
        context.Services.AddCanDoItAllBaseLib();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        return context;
    }
}
