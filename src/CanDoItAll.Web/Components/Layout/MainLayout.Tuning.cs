using CanDoItAll.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

namespace CanDoItAll.Web.Components.Layout;

public partial class MainLayout
{
    private Task HandleTuningInstructionChanged(string value)
    {
        tuningInstruction = value;
        return Task.CompletedTask;
    }

    private async Task SendTuningRequestAsync()
    {
        if (TuningCoordinator.CurrentRequest is null)
        {
            tuningMessage = "Select a tunable surface before sending a request.";
            return;
        }

        tuningBusy = true;
        tuningMessage = null;
        StateHasChanged();

        var result = await DevelopmentManagerClient.CreateTuningRequestAsync(
            TuningCoordinator.CurrentRequest,
            tuningInstruction,
            tuningAttachments,
            autoSubmit: !DevelopmentManagerOptions.Value.ReviewBeforeSend);

        tuningBusy = false;
        tuningMessage = result.IsSuccess
            ? $"Tuning request queued as {result.Value!.Status}."
            : string.Join(" ", result.Errors.Select(error => error.Message));

        if (result.IsSuccess)
        {
            pendingTuningRequest = result.Value;
            tuningInstruction = string.Empty;
            tuningAttachments.Clear();
            TuningCoordinator.Clear();
        }

        StateHasChanged();
    }

    private async Task SubmitPendingTuningRequestAsync()
    {
        if (pendingTuningRequest is null)
        {
            tuningMessage = "No tuning request is awaiting approval.";
            return;
        }

        tuningBusy = true;
        tuningMessage = null;
        StateHasChanged();

        var result = await DevelopmentManagerClient.SubmitTuningRequestAsync(pendingTuningRequest.Id);
        tuningBusy = false;
        if (result.IsSuccess)
        {
            pendingTuningRequest = result.Value;
            tuningMessage = $"Tuning request submitted as {result.Value!.Status}.";
        }
        else
        {
            tuningMessage = string.Join(" ", result.Errors.Select(error => error.Message));
        }

        StateHasChanged();
    }

    private async Task HandleTuningFilesSelectedAsync(InputFileChangeEventArgs args)
    {
        foreach (var file in args.GetMultipleFiles())
        {
            await using var stream = file.OpenReadStream(maxAllowedSize: 5 * 1024 * 1024);
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            tuningAttachments.Add(new TuningAttachmentRequest(
                file.Name,
                file.ContentType,
                Convert.ToBase64String(memoryStream.ToArray()),
                "upload"));
        }

        tuningMessage = $"{tuningAttachments.Count} attachment(s) ready.";
    }

    private async Task PasteClipboardImageAsync()
    {
        var image = await JS.InvokeAsync<ClipboardImagePayload?>("candoitallTuning.readClipboardImage");
        if (image is null)
        {
            tuningMessage = "No clipboard image was found.";
            return;
        }

        tuningAttachments.Add(new TuningAttachmentRequest(
            image.FileName,
            image.ContentType,
            image.ContentBase64,
            "clipboard"));
        tuningMessage = $"{tuningAttachments.Count} attachment(s) ready.";
    }

    private Task RemoveTuningAttachmentAsync(string fileName)
    {
        tuningAttachments.RemoveAll(item => string.Equals(item.FileName, fileName, StringComparison.OrdinalIgnoreCase));
        return Task.CompletedTask;
    }

    private void ClearTuningRequest()
    {
        tuningInstruction = string.Empty;
        tuningMessage = null;
        tuningAttachments.Clear();
        TuningCoordinator.Clear();
    }

    private sealed record ClipboardImagePayload(string FileName, string ContentType, string ContentBase64);
}
