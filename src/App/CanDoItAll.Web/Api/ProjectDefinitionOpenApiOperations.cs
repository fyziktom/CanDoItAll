using System.Reflection;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using Microsoft.OpenApi;

namespace CanDoItAll.Web.Api;

internal static class ProjectDefinitionOpenApiOperations {
    private enum Endpoint { Context, Read, Create, Replace, Scalars, Lookup, CompatibilitySave, StructureRead, StructureUpdate }

    internal static Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken) {
        var endpoint = Resolve(context);
        if (endpoint is null) {
            return Task.CompletedTask;
        }
        if (endpoint == Endpoint.Context) {
            operation.Description = "Read current dataset/caller binding, issued epoch, admission and retention deadlines, and supported limits/versions before freezing a new definition command. This read neither accepts a write nor reconciles an older draft or pending operation.";
        }
        foreach (var parameter in operation.Parameters?.OfType<OpenApiParameter>() ?? []) {
            parameter.Description = parameter.Name switch {
                "projectId" => "Existing project ID. For a conditional write it must match the project in the original submitted expectation.",
                "namespaceId" => "Original dataset namespace from the frozen operation. Do not replace it with the currently active namespace.",
                "epoch" => ProjectOperationEpochHttpContract.Description,
                "operationId" => "Original nonempty operation ID chosen once before dispatch. Lookup does not execute the command.",
                "callerBinding" => "Original opaque caller binding from the frozen operation, supplied as a query value. Current credentials are independently required.",
                _ => parameter.Description
            };
        }
        if (operation.RequestBody is { } body) {
            body.Description = endpoint switch {
                Endpoint.Create => "Frozen operation, request version and complete creation command. Retain the exact request before dispatch; explicit phase and option arrays are required.",
                Endpoint.Replace => "Frozen operation, request version, original expectation and complete desired definition. Explicit phase and option arrays replace both stored collections; do not substitute a newer expectation into this request.",
                Endpoint.Scalars => "Frozen operation, request version, original expectation and every desired scalar value. Phases, options and external identity are preserved.",
                Endpoint.CompatibilitySave => "Compatibility editor submission. Existing IDs require the original expectedDefinition and explicit non-null replacement arrays. Creation may omit arrays. This route has no durable operation receipt or safe blind-retry guarantee.",
                Endpoint.StructureUpdate => "Project Structure scalar update with original expectedDefinition and the existing source lease/caller fields required by this API. Child collections and external identity are preserved; this compatibility operation has no durable retry receipt.",
                _ => body.Description
            };
        }
        foreach (var (status, response) in operation.Responses ?? []) {
            response.Description = DescribeResponse(endpoint.Value, status) ?? response.Description;
        }
        return Task.CompletedTask;
    }

    private static Endpoint? Resolve(OpenApiOperationTransformerContext context) {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        var name = metadata.OfType<IEndpointNameMetadata>().LastOrDefault()?.EndpointName;
        Endpoint? named = name switch {
            "GetProjectDefinitionWriteContext" => Endpoint.Context,
            "GetProjectDefinition" => Endpoint.Read,
            "CreateProjectDefinition" => Endpoint.Create,
            "ReplaceProjectDefinition" => Endpoint.Replace,
            "UpdateProjectDefinitionScalars" => Endpoint.Scalars,
            ProjectOperationEpochHttpContract.LookupEndpointName => Endpoint.Lookup,
            "SaveProject" => Endpoint.CompatibilitySave,
            _ => null
        };
        if (named is not null) {
            return named;
        }
        var method = metadata.OfType<MethodInfo>().LastOrDefault();
        return method?.DeclaringType == typeof(ProjectStructureAgentApi) ? method.Name switch {
            nameof(ProjectStructureAgentApi.GetProjectDefinitionAsync) => Endpoint.StructureRead,
            nameof(ProjectStructureAgentApi.UpdateProjectAsync) => Endpoint.StructureUpdate,
            _ => null
        } : null;
    }

    private static string? DescribeResponse(Endpoint endpoint, string status) {
        if (endpoint == Endpoint.Lookup) {
            return status switch {
                "200" => "A current authorized observation: exact historical receipt, unresolved open/draining notObserved, or fully retained closedAbsent proof. Inspect status; notObserved never proves rollback.",
                "400" => "The original lookup address or caller binding is malformed; no lookup result was established.",
                "403" => "Current read disclosure is denied, or accepted detail is accessDenied. This does not prove the original write failed or permit a replacement operation.",
                "404" => "Historical detail is unavailable because the target cannot currently be read. This is not absence or rollback proof.",
                "409" => "The original dataset/caller/epoch context differs or receipt detail cannot currently be decoded. Preserve the original pending evidence and inspect status/reason.",
                "410" => "The original epoch has been retired. Accepted detail and retained-absence proof are no longer available; do not infer rollback.",
                _ => null
            };
        }
        var protectedWrite = endpoint is Endpoint.Create or Endpoint.Replace or Endpoint.Scalars;
        return status switch {
            "200" => endpoint switch {
                Endpoint.Context => "Current operation admission context and limits; no definition mutation or acceptance occurred.",
                Endpoint.Read => "Coherent current definition and original edit expectation, with a current caller/dataset-bound write context.",
                Endpoint.StructureRead => "Coherent current definition and original expectation for a subsequent conditional update; this is not a creation template.",
                Endpoint.CompatibilitySave => "Accepted project ID from this compatibility save. No durable operation receipt is provided for a lost response.",
                Endpoint.StructureUpdate => "Exact accepted scalar values and resulting definition expectation; phases, options and external identity were preserved.",
                _ => "Exact historical acceptance for the original operation. Inspect replayed; the acknowledgement is not necessarily the project's current state."
            },
            "201" => "A newly accepted project definition and its exact durable receipt. Preserve the receipt before reconciling the submitted draft.",
            "400" => "The command, original expectation or address is invalid; this request attempt was rejected. An earlier unconfirmed attempt is not resolved by this response.",
            "401" => "Authentication did not establish a currently valid credential. No original operation outcome is disclosed.",
            "403" => endpoint is Endpoint.StructureRead or Endpoint.StructureUpdate
                ? "The required Project Structure capability or source authority is unavailable. This does not establish the outcome of an earlier ambiguous submission."
                : "Current Projects access or receipt disclosure is denied. The response cannot establish the outcome of an earlier unconfirmed operation.",
            "404" => "The addressed existing project is unavailable. No creation template or replacement project is substituted.",
            "409" => endpoint switch {
                Endpoint.Read or Endpoint.Context => "The dataset or caller binding changed during the read. Acquire a coherent current read before preparing a new operation; preserve existing pending evidence.",
                Endpoint.StructureRead or Endpoint.StructureUpdate => "The source lease, project lifetime, original definition revision or transaction admission conflicts with current owner state. Inspect the error code; preserve local edits and do not blindly repeat an ambiguous update.",
                Endpoint.CompatibilitySave => "The original definition revision/lifetime, transaction admission or external identity conflicts with current owner state. Inspect the error code; a transaction conflict does not establish that the definition itself changed.",
                _ => "The original revision, lifetime, dataset, caller, epoch, payload or identity conflicts with current owner state. Inspect the error code; preserve local edits and prior uncertainty instead of refreshing an old request's token or operation identity."
            },
            "410" => "The operation epoch is retired and this command cannot begin a new effect. Expiry does not prove that an earlier attempt failed.",
            "413" => "The submitted body or definition exceeds an enforced bound; this attempt was rejected without accepting the requested definition.",
            "415" => "The request is not supported JSON content; the definition command was not admitted. The response uses the general API errors envelope and does not resolve an earlier unconfirmed operation.",
            "500" => "The required Project Structure owner or persistence service failed. This compatibility route has no durable outcome lookup; preserve an ambiguous submission rather than blindly repeating it.",
            "503" when protectedWrite => "Inspect status: rejected means current authority could not be observed and this attempt did not commit; unconfirmed means commit acknowledgement remains unresolved. Preserve earlier uncertainty and the entire original frozen request; use authorized lookup or an explicit exact retry, never a replacement operation.",
            _ => null
        };
    }
}
