using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ZyphoNote.MarketingPrompts.Data;
using ZyphoNote.MarketingPrompts.Data.Entities;
using ZyphoNote.MarketingPrompts.Options;

namespace ZyphoNote.MarketingPrompts.Services;

public sealed class ComfyUiService(
    IHttpClientFactory httpClientFactory,
    IDbContextFactory<MarketingDbContext> dbContextFactory,
    IWebHostEnvironment environment,
    IOptions<ComfyUiOptions> options,
    ILogger<ComfyUiService> logger)
{
    private readonly ComfyUiOptions options = options.Value;

    public async Task<ComfyUiGenerationResult> GenerateAsync(
        ComfyUiGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var record = new GeneratedImageRecord
        {
            MarketingPostId = request.MarketingPostId,
            RequestSource = request.RequestSource,
            PositivePrompt = request.PositivePrompt.Trim(),
            NegativePrompt = request.NegativePrompt?.Trim() ?? string.Empty,
            BaseUrl = ResolveBaseUrl(),
            Status = ImageGenerationStatuses.Pending,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        db.GeneratedImageRecords.Add(record);
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(5, options.TimeoutSeconds)));

            var http = httpClientFactory.CreateClient(nameof(ComfyUiService));
            var workflow = await LoadWorkflowAsync(timeout.Token);
            SetPromptText(workflow, options.PositivePromptNodeId, request.PositivePrompt);
            if (!string.IsNullOrWhiteSpace(options.NegativePromptNodeId))
            {
                SetPromptText(workflow, options.NegativePromptNodeId, request.NegativePrompt ?? string.Empty);
            }

            RandomizeSeed(workflow);

            var response = await http.PostAsJsonAsync(
                $"{ResolveBaseUrl()}/prompt",
                new JsonObject { ["prompt"] = workflow },
                timeout.Token);

            response.EnsureSuccessStatusCode();
            var responseJson = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken: timeout.Token);
            var promptId = responseJson?["prompt_id"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(promptId))
            {
                throw new InvalidOperationException("ComfyUI did not return a prompt_id.");
            }

            record.PromptId = promptId;
            await db.SaveChangesAsync(timeout.Token);

            var history = await WaitForHistoryAsync(http, promptId, timeout.Token);
            var images = await DownloadImagesAsync(http, promptId, history, record.Id, timeout.Token);
            var firstImage = images.FirstOrDefault();
            if (firstImage is null)
            {
                throw new InvalidOperationException("ComfyUI completed but did not return any downloadable images.");
            }

            record.LocalFilePath = firstImage.LocalPath;
            record.WebPath = firstImage.WebPath;
            record.Status = ImageGenerationStatuses.Completed;
            record.CompletedAtUtc = DateTimeOffset.UtcNow;

            if (request.MarketingPostId is not null)
            {
                var post = await db.MarketingPosts.FirstOrDefaultAsync(post => post.Id == request.MarketingPostId, timeout.Token);
                if (post is not null)
                {
                    post.UpdatedAtUtc = DateTimeOffset.UtcNow;
                }
            }

            await db.SaveChangesAsync(timeout.Token);
            return new ComfyUiGenerationResult(true, ToSummary(record), null);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "ComfyUI image generation failed.");
            record.Status = ImageGenerationStatuses.Failed;
            record.ErrorMessage = exception.Message;
            record.CompletedAtUtc = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(CancellationToken.None);
            return new ComfyUiGenerationResult(false, ToSummary(record), exception.Message);
        }
    }

    public async Task<IReadOnlyList<GeneratedImageSummary>> GetHistoryAsync(
        Guid? marketingPostId = null,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var records = db.GeneratedImageRecords.AsNoTracking().AsQueryable();
        if (marketingPostId is not null)
        {
            records = records.Where(record => record.MarketingPostId == marketingPostId);
        }

        var result = await records.ToListAsync(cancellationToken);

        return result
            .OrderByDescending(record => record.CreatedAtUtc)
            .Take(take)
            .Select(record => new GeneratedImageSummary(
                record.Id,
                record.MarketingPostId,
                record.RequestSource,
                record.Provider,
                record.Status,
                record.PositivePrompt,
                record.NegativePrompt,
                record.PromptId,
                record.WebPath,
                record.ErrorMessage,
                record.CreatedAtUtc,
                record.CompletedAtUtc))
            .ToList();
    }

    private async Task<JsonObject> LoadWorkflowAsync(CancellationToken cancellationToken)
    {
        var workflowPath = ResolveContentPath(options.WorkflowPath);
        var workflowJson = await File.ReadAllTextAsync(workflowPath, cancellationToken);
        return JsonNode.Parse(workflowJson)?.AsObject()
            ?? throw new InvalidOperationException($"Invalid ComfyUI workflow JSON at {workflowPath}.");
    }

    private async Task<JsonObject> WaitForHistoryAsync(HttpClient http, string promptId, CancellationToken cancellationToken)
    {
        while (true)
        {
            var history = await http.GetFromJsonAsync<JsonObject>($"{ResolveBaseUrl()}/history/{promptId}", cancellationToken);
            if (history is not null && history.ContainsKey(promptId))
            {
                return history;
            }

            await Task.Delay(Math.Max(250, options.PollIntervalMilliseconds), cancellationToken);
        }
    }

    private async Task<IReadOnlyList<SavedComfyImage>> DownloadImagesAsync(
        HttpClient http,
        string promptId,
        JsonObject historyRoot,
        Guid recordId,
        CancellationToken cancellationToken)
    {
        var outputDirectory = Path.Combine(ResolveContentPath(options.OutputDirectory), DateTime.UtcNow.ToString("yyyyMMdd"));
        Directory.CreateDirectory(outputDirectory);

        var promptHistory = historyRoot[promptId]?.AsObject()
            ?? throw new InvalidOperationException("Invalid ComfyUI history response.");

        var outputs = promptHistory["outputs"]?.AsObject()
            ?? throw new InvalidOperationException("ComfyUI history does not contain outputs.");

        var savedFiles = new List<SavedComfyImage>();
        foreach (var outputNode in outputs)
        {
            var images = outputNode.Value?["images"]?.AsArray();
            if (images is null)
            {
                continue;
            }

            foreach (var imageNode in images)
            {
                var image = imageNode?.AsObject();
                var filename = image?["filename"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(filename))
                {
                    continue;
                }

                var subfolder = image?["subfolder"]?.GetValue<string>() ?? string.Empty;
                var type = image?["type"]?.GetValue<string>() ?? "output";
                var viewUrl =
                    $"{ResolveBaseUrl()}/view" +
                    $"?filename={WebUtility.UrlEncode(filename)}" +
                    $"&subfolder={WebUtility.UrlEncode(subfolder)}" +
                    $"&type={WebUtility.UrlEncode(type)}";

                var imageBytes = await http.GetByteArrayAsync(viewUrl, cancellationToken);
                var safeFileName = $"{recordId:N}-{SanitizeFileName(Path.GetFileName(filename))}";
                var localPath = Path.Combine(outputDirectory, safeFileName);
                await File.WriteAllBytesAsync(localPath, imageBytes, cancellationToken);

                var webPath = $"{options.PublicOutputPath.TrimEnd('/')}/{DateTime.UtcNow:yyyyMMdd}/{Uri.EscapeDataString(safeFileName)}";
                savedFiles.Add(new SavedComfyImage(localPath, webPath));
            }
        }

        return savedFiles;
    }

    private void SetPromptText(JsonObject workflow, string nodeId, string text)
    {
        var inputs = workflow[nodeId]?["inputs"]?.AsObject()
            ?? throw new InvalidOperationException($"ComfyUI workflow is missing inputs for node {nodeId}.");

        inputs["text"] = text;
    }

    private void RandomizeSeed(JsonObject workflow)
    {
        if (string.IsNullOrWhiteSpace(options.SamplerNodeId))
        {
            return;
        }

        var inputs = workflow[options.SamplerNodeId]?["inputs"]?.AsObject();
        if (inputs is null || !inputs.ContainsKey("seed"))
        {
            return;
        }

        inputs["seed"] = Random.Shared.NextInt64(1, long.MaxValue);
    }

    private string ResolveBaseUrl()
        => options.BaseUrl.TrimEnd('/');

    private string ResolveContentPath(string path)
        => Path.IsPathRooted(path) ? path : Path.Combine(environment.ContentRootPath, path);

    private static string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(fileName.Select(character => invalid.Contains(character) ? '-' : character).ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "comfyui-image.png" : safe;
    }

    private static GeneratedImageSummary ToSummary(GeneratedImageRecord record)
        => new(
            record.Id,
            record.MarketingPostId,
            record.RequestSource,
            record.Provider,
            record.Status,
            record.PositivePrompt,
            record.NegativePrompt,
            record.PromptId,
            record.WebPath,
            record.ErrorMessage,
            record.CreatedAtUtc,
            record.CompletedAtUtc);

    private sealed record SavedComfyImage(string LocalPath, string WebPath);
}
