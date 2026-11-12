using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Modules.Workspace.ApiAccess;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel;

namespace CanDoItAll.Web.Api;

internal static class WorkflowAttachmentsApi {
    private const long RequestLimit = WorkflowDocumentStagingService.MaximumBytes + 64 * 1024;

    internal static RouteGroupBuilder MapWorkflowAttachmentsApi(this RouteGroupBuilder workflows) {
        workflows.MapPost("/attachments/documents", StageAsync)
            .WithName("StageWorkflowDocument")
            .WithApiPermission(ApiAccessScopeNames.WriteWorkflows)
            .Accepts<WorkflowDocumentUploadRequest>("multipart/form-data")
            .WithMetadata(new RequestSizeLimitAttribute(RequestLimit), new RequestFormLimitsAttribute {
                MultipartBodyLengthLimit = RequestLimit
            })
            .Produces<WorkflowDocumentStagingResult>()
            .ProducesApiErrors(400, 401, 403, 413, 415)
            .DescribeApi("Stage a PDF for workflow document conversion.",
                "Requires api.workflows.write. Send exactly one multipart file named file and no text fields. " +
                "The file must have a .pdf extension, application/pdf media type and PDF signature, and contain " +
                "1–10485760 bytes. Whole-request limit adds 64 KiB. A unique managed workspace path is returned " +
                "for document.to-markdown sourcePath; the upload does not convert or execute the document. " +
                "Treat uploaded content as untrusted. Each upload creates a new file; cancellation removes partial files. " +
                "Invalid input returns 400 workflows.document-invalid; framework size/type errors may have no envelope.",
                "Managed workspace-relative path, application/pdf content type and stored byte count.",
                "Exactly one PDF file part named file.")
            .AddOpenApiOperationTransformer((operation, _, _) => {
                operation.Responses!["413"].Description = "The multipart request exceeds the 10 MiB document limit plus 64 KiB of request overhead; no document was staged.";
                operation.Responses["415"].Description = "The request must use multipart/form-data with exactly one PDF file part named file.";
                return Task.CompletedTask;
            });
        return workflows;
    }

    internal static async Task<IResult> StageAsync(HttpRequest request, WorkflowDocumentStagingService staging,
        CancellationToken cancellationToken) {
        if (!request.HasFormContentType) {
            return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
        }
        var form = await request.ReadFormAsync(cancellationToken);
        if (form.Count != 0 || form.Files.Count != 1 || form.Files[0].Name != "file") {
            return ApiEndpointResults.BadRequest("Send exactly one PDF file part named file.", "workflows.document-invalid");
        }
        var file = form.Files[0];
        try {
            await using var input = file.OpenReadStream();
            return Results.Ok(await staging.StageAsync(file.FileName, file.ContentType, file.Length, input, cancellationToken));
        } catch (InvalidOperationException error) {
            return ApiEndpointResults.BadRequest(error.Message, "workflows.document-invalid");
        }
    }
}

[Description("Multipart request containing exactly one PDF file part named file and no text fields. Staging stores the document without converting or executing it.")]
internal sealed record WorkflowDocumentUploadRequest(
    [property: Description("One nonempty PDF, at most 10 MiB, with application/pdf media type.")] IFormFile File);
