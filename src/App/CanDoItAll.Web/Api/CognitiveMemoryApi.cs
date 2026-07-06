using Microsoft.AspNetCore.Http;

namespace CanDoItAll.Web.Api;

internal static class CognitiveMemoryApi
{
    private const string ContractVersion = "retired-v1";
    private const string LegacyBasePath = "/api/cognitive-memory";
    private const string V1BasePath = "/api/cognitive-memory/v1";
    private const string GenericMemoryUiPath = "/memory";
    private static readonly string[] RetiredEndpointMethods =
    [
        HttpMethods.Get,
        HttpMethods.Post,
        HttpMethods.Put,
        HttpMethods.Patch,
        HttpMethods.Delete
    ];

    public static RouteGroupBuilder MapCognitiveMemoryApi(this RouteGroupBuilder group)
    {
        MapRetiredSurface(
            group.MapGroup("/cognitive-memory"),
            "CognitiveMemoryLegacy",
            LegacyBasePath,
            V1BasePath);
        MapRetiredSurface(
            group.MapGroup("/cognitive-memory/v1"),
            "CognitiveMemoryV1",
            V1BasePath,
            LegacyBasePath);

        return group;
    }

    private static void MapRetiredSurface(
        RouteGroupBuilder memory,
        string endpointNamePrefix,
        string basePath,
        string compatibilityBasePath)
    {
        memory.WithTags("Cognitive Memory")
            .DisableAntiforgery();

        memory.MapGet("/contract", () => Results.Ok(CreateContract(basePath, compatibilityBasePath)))
            .WithName($"{endpointNamePrefix}Contract");
        memory.MapMethods("/", RetiredEndpointMethods, (HttpContext context) => Retired(context, basePath))
            .WithName($"{endpointNamePrefix}RetiredRoot");
        memory.MapMethods("/{**path}", RetiredEndpointMethods, (HttpContext context) => Retired(context, basePath))
            .WithName($"{endpointNamePrefix}RetiredCatchAll");
    }

    private static IResult Retired(HttpContext context, string basePath)
    {
        return Results.Json(
            new CognitiveMemoryApiRetiredEndpointResponse(
                "gone",
                "The in-process Cognitive Memory API has been removed from the base host.",
                context.Request.Path.Value ?? basePath,
                $"{basePath}/contract",
                GenericMemoryUiPath,
                "Configure a generic memory provider profile. Use the native remote provider driver when the separate native Cognitive Memory service is required."),
            statusCode: StatusCodes.Status410Gone);
    }

    private static CognitiveMemoryApiContractResponse CreateContract(
        string basePath,
        string compatibilityBasePath)
    {
        return new CognitiveMemoryApiContractResponse(
            ContractVersion,
            basePath,
            compatibilityBasePath,
            "Retired from base host",
            GenericMemoryUiPath,
            "The legacy in-process Cognitive Memory API no longer registers native module services in CanDoItAll.Web. Use the generic Memory UI/API surfaces and explicit memory provider profiles; configure the native remote provider driver for the separate native service.",
            [
                new("GET", $"{basePath}/contract", "Returns this retirement contract."),
                new("*", $"{basePath}/{{**path}}", "Returns 410 Gone with migration guidance.")
            ]);
    }
}

internal sealed record CognitiveMemoryApiContractResponse(
    string Version,
    string BasePath,
    string CompatibilityBasePath,
    string Status,
    string GenericMemoryUiPath,
    string Guidance,
    IReadOnlyList<CognitiveMemoryApiRouteContract> Routes);

internal sealed record CognitiveMemoryApiRouteContract(
    string Method,
    string Path,
    string Summary);

internal sealed record CognitiveMemoryApiRetiredEndpointResponse(
    string Status,
    string Message,
    string RequestedPath,
    string ContractPath,
    string GenericMemoryUiPath,
    string Guidance);
