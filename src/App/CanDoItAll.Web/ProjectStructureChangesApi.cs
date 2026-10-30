using System.Globalization;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Web;

internal static class ProjectStructureChangesApi {
    internal static void MapProjectStructureChanges(this RouteGroupBuilder group) {
        group.MapGet("/projects/{projectId:guid}/changes", PollAsync)
            .WithSummary("Poll committed project structure changes.")
            .WithDescription("Returns a bounded page of node and relationship changes in this server process and active database profile. Omit after to establish a current cursor without history. Resume with after={cursor.streamId:N}:{cursor.sequence}; take is 1–100 (default 100). requiresRefresh means replay was lost or the process restarted. This is a refresh notification feed, not a durable audit log. Uses the Project Structure API authorization policy; no write lease is needed.")
            .Produces<ProjectStructureChanges>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);
        group.MapGet("/projects/{projectId:guid}/changes/stream", StreamAsync)
            .WithSummary("Follow committed project structure changes with SSE.")
            .WithDescription("Same scope and cursor as polling. Resume with after or Last-Event-ID (matching values if both supplied). Each SSE id is a 32-digit hexadecimal stream GUID, a colon, and the sequence. Events project.structure.ready, project.structure.changed, project.structure.gap and stream.heartbeat carry the polling response. A gap requires an explicit structure refresh. Heartbeats occur at least every 15 seconds while idle. A database-profile switch ends the stream with a gap; reconnect in the new profile. The request is cancelled on disconnect. Uses the Project Structure API authorization policy; no write lease is needed.")
            .Produces(StatusCodes.Status200OK, contentType: "text/event-stream")
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> PollAsync(Guid projectId, HttpContext http,
        ProjectStructureChangeFeed feed, ProjectWriteAdmissionService admissions, ProjectRecordQueryService projects,
        int? take, CancellationToken cancellationToken) {
        if (!TryCursor(http.Request, out var cursor) || take is < 1 or > 100) {
            return Error(StatusCodes.Status400BadRequest, "ProjectStructureChangeCursorInvalid",
                "Use one matching non-negative streamId:sequence cursor and a take value from 1 to 100.");
        }
        if (await projects.GetAsync(projectId, cancellationToken) is null) {
            return Error(StatusCodes.Status404NotFound, "ProjectNotFound", "The project is unavailable in the active database profile.");
        }
        return Results.Json(feed.Read(admissions.DatabaseProfileId, [projectId], cursor, take ?? 100),
            ProjectStructureHttpJsonContract.SerializerOptions);
    }

    private static async Task<IResult> StreamAsync(Guid projectId, HttpContext http,
        ProjectStructureChangeFeed feed, ProjectWriteAdmissionService admissions, ProjectRecordQueryService projects,
        IDatabaseRuntimeState runtime, CancellationToken cancellationToken) {
        if (!TryCursor(http.Request, out var cursor)) {
            return Error(StatusCodes.Status400BadRequest, "ProjectStructureChangeCursorInvalid",
                "Use one matching non-negative streamId:sequence cursor in after or Last-Event-ID.");
        }
        if (await projects.GetAsync(projectId, cancellationToken) is null) {
            return Error(StatusCodes.Status404NotFound, "ProjectNotFound", "The project is unavailable in the active database profile.");
        }
        http.Response.Headers.CacheControl = "no-cache, no-store";
        http.Response.Headers["X-Accel-Buffering"] = "no";
        return TypedResults.ServerSentEvents(Events(feed, admissions.DatabaseProfileId, projectId, runtime, cursor, cancellationToken));
    }

    private static async IAsyncEnumerable<SseItem<ProjectStructureChanges>> Events(ProjectStructureChangeFeed feed,
        Guid profileId, Guid projectId, IDatabaseRuntimeState runtime, ProjectStructureChangeCursor? cursor,
        [EnumeratorCancellation] CancellationToken cancellationToken) {
        var first = true;
        while (!cancellationToken.IsCancellationRequested) {
            var batch = feed.Read(profileId, [projectId], cursor);
            var profileChanged = runtime.GetSnapshot().ActiveProfileId != profileId;
            if (profileChanged) {
                batch = batch with { Changes = [], RequiresRefresh = true };
            }
            var eventName = batch.RequiresRefresh ? "project.structure.gap"
                : first ? "project.structure.ready"
                : batch.Changes.Count > 0 ? "project.structure.changed" : "stream.heartbeat";
            yield return new(batch, eventName) { EventId = FormatCursor(batch.Cursor) };
            if (profileChanged) {
                yield break;
            }
            first = false;
            cursor = batch.Cursor;
            using var heartbeat = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            heartbeat.CancelAfter(TimeSpan.FromSeconds(15));
            try {
                await feed.WaitAsync(cursor, heartbeat.Token);
            } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
                // Send an idle heartbeat and recheck the database profile.
            }
        }
    }

    private static bool TryCursor(HttpRequest request, out ProjectStructureChangeCursor? cursor) {
        cursor = null;
        var query = request.Query["after"];
        var header = request.Headers["Last-Event-ID"];
        if (query.Count > 1 || header.Count > 1 || query.Count == 1 && header.Count == 1 && query[0] != header[0]) {
            return false;
        }
        var value = header.Count == 1 ? header[0] : query.Count == 1 ? query[0] : null;
        if (query.Count == 0 && header.Count == 0) {
            return true;
        }
        var parts = value?.Split(':');
        if (parts is not { Length: 2 } || !Guid.TryParseExact(parts[0], "N", out var streamId) ||
            !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var sequence)) {
            return false;
        }
        cursor = new(streamId, sequence);
        return true;
    }

    private static string FormatCursor(ProjectStructureChangeCursor cursor)
        => $"{cursor.StreamId:N}:{cursor.Sequence.ToString(CultureInfo.InvariantCulture)}";

    private static IResult Error(int statusCode, string code, string message)
        => Results.Json(new { Error = new { ErrorCode = code, Message = message } },
            ProjectStructureHttpJsonContract.SerializerOptions, statusCode: statusCode);
}
