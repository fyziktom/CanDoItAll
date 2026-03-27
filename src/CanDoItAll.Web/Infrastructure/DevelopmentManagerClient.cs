using System.Net.Http.Json;
using CanDoItAll.Components;
using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Web.Infrastructure;

public sealed record DevelopmentTuningRequestResult(
    Guid Id,
    string CorrelationId,
    string CapsuleKey,
    string ComponentName,
    string Route,
    Guid? ProjectId,
    string? TabId,
    string? SelectionId,
    string? ContextSummary,
    string Instruction,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string Summary,
    long? ReadyWatchEventId,
    int? ReadyWatchIteration,
    bool CapsuleDriftDetected,
    int AttachmentCount,
    string CapsuleSummary,
    string EvidenceDirectory,
    string? AdapterJobId);

public sealed class DevelopmentManagerClient(HttpClient httpClient, IOptions<DevelopmentManagerOptions> options)
{
    public async Task<Result<DevelopmentTuningRequestResult>> CreateTuningRequestAsync(
        TuningBoundaryRequest request,
        string instruction,
        IReadOnlyList<TuningAttachmentRequest> attachments,
        bool autoSubmit,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(instruction))
        {
            return Result<DevelopmentTuningRequestResult>.Failure(Error.Validation("Add an instruction before sending a tuning request."));
        }

        if (!TryCreateBaseUri(out var baseUri))
        {
            return Result<DevelopmentTuningRequestResult>.Failure(Error.Failure("Development manager URL is not configured."));
        }

        try
        {
            var status = await httpClient.GetFromJsonAsync<ManagerStatusEnvelope>(new Uri(baseUri, "/api/status"), cancellationToken);
            if (status is null || string.IsNullOrWhiteSpace(status.SessionToken))
            {
                return Result<DevelopmentTuningRequestResult>.Failure(Error.Failure("The development manager did not return a session token."));
            }

            using var message = new HttpRequestMessage(HttpMethod.Post, new Uri(baseUri, "/api/tuning/requests"))
            {
                Content = JsonContent.Create(new
                {
                    request.CapsuleKey,
                    request.ComponentName,
                    Route = request.Route ?? "/",
                    request.ProjectId,
                    request.TabId,
                    request.SelectionId,
                    request.ContextSummary,
                    Instruction = instruction.Trim(),
                    Attachments = attachments.Select(attachment => new
                    {
                        attachment.FileName,
                        attachment.ContentType,
                        attachment.ContentBase64,
                        attachment.Source
                    }),
                    AutoSubmit = autoSubmit
                })
            };

            message.Headers.Add("X-CanDoItAll-Manager-Session", status.SessionToken);
            using var response = await httpClient.SendAsync(message, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var record = await response.Content.ReadFromJsonAsync<DevelopmentTuningRequestResult>(cancellationToken);
                return record is null
                    ? Result<DevelopmentTuningRequestResult>.Failure(Error.Failure("The development manager returned an empty tuning response."))
                    : Result<DevelopmentTuningRequestResult>.Success(record);
            }

            var error = await response.Content.ReadFromJsonAsync<ManagerErrorEnvelope>(cancellationToken);
            var messageText = error?.Error
                ?? $"Development manager request failed with {(int)response.StatusCode} {response.ReasonPhrase}.";
            return Result<DevelopmentTuningRequestResult>.Failure(Error.Failure(messageText));
        }
        catch (Exception ex)
        {
            return Result<DevelopmentTuningRequestResult>.Failure(Error.Failure($"Unable to contact the development manager. {ex.Message}"));
        }
    }

    public async Task<Result<DevelopmentTuningRequestResult>> SubmitTuningRequestAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        if (!TryCreateBaseUri(out var baseUri))
        {
            return Result<DevelopmentTuningRequestResult>.Failure(Error.Failure("Development manager URL is not configured."));
        }

        try
        {
            var status = await httpClient.GetFromJsonAsync<ManagerStatusEnvelope>(new Uri(baseUri, "/api/status"), cancellationToken);
            if (status is null || string.IsNullOrWhiteSpace(status.SessionToken))
            {
                return Result<DevelopmentTuningRequestResult>.Failure(Error.Failure("The development manager did not return a session token."));
            }

            using var message = new HttpRequestMessage(HttpMethod.Post, new Uri(baseUri, $"/api/tuning/requests/{requestId}/submit"));
            message.Headers.Add("X-CanDoItAll-Manager-Session", status.SessionToken);

            using var response = await httpClient.SendAsync(message, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var record = await response.Content.ReadFromJsonAsync<DevelopmentTuningRequestResult>(cancellationToken);
                return record is null
                    ? Result<DevelopmentTuningRequestResult>.Failure(Error.Failure("The development manager returned an empty tuning response."))
                    : Result<DevelopmentTuningRequestResult>.Success(record);
            }

            var error = await response.Content.ReadFromJsonAsync<ManagerErrorEnvelope>(cancellationToken);
            var messageText = error?.Error
                ?? $"Development manager request failed with {(int)response.StatusCode} {response.ReasonPhrase}.";
            return Result<DevelopmentTuningRequestResult>.Failure(Error.Failure(messageText));
        }
        catch (Exception ex)
        {
            return Result<DevelopmentTuningRequestResult>.Failure(Error.Failure($"Unable to contact the development manager. {ex.Message}"));
        }
    }

    private bool TryCreateBaseUri(out Uri baseUri)
    {
        var configured = options.Value.ManagerBaseUrl?.Trim();
        return Uri.TryCreate(configured, UriKind.Absolute, out baseUri!);
    }

    private sealed record ManagerStatusEnvelope(string SessionToken);

    private sealed record ManagerErrorEnvelope(string Error);
}


