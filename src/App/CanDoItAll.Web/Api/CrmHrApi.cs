using CanDoItAll.Modules.CrmHr;
using Microsoft.AspNetCore.Mvc;

namespace CanDoItAll.Web.Api;

internal static class CrmHrApi
{
    private const string PartyNotFoundCode = "crmhr.party.not-found";
    private const string RelationshipPartyNotFoundCode = "crmhr.party.relationship-party-not-found";
    private const string WorkforcePartyNotFoundCode = "crmhr.workforce.party-not-found";
    private const string SkillPartyNotFoundCode = "crmhr.skills.party-not-found";
    private const string SkillNotFoundCode = "crmhr.skills.skill-not-found";
    private const string CapacityPartyNotFoundCode = "crmhr.capacity.party-not-found";
    private const string CapacityProjectNotFoundCode = "crmhr.capacity.project-not-found";
    private const string InterviewApplicationNotFoundCode = "crmhr.recruiting.interview.application-not-found";
    private const string LifecyclePartyNotFoundCode = "crmhr.recruiting.task.party-not-found";
    private const string LifecycleProjectNotFoundCode = "crmhr.recruiting.task.project-not-found";
    private const string SupportPartyNotFoundCode = "crmhr.recruiting.support.party-not-found";
    private const string ConversionApplicationNotFoundCode = "crmhr.recruiting.convert.application-not-found";
    private const string ConversionPartyNotFoundCode = "crmhr.recruiting.convert.party-not-found";

    public static RouteGroupBuilder MapCrmHrApi(this RouteGroupBuilder group)
    {
        var crmHr = group.MapGroup("/crm-hr")
            .WithTags("CRM / HR");

        MapPartyEndpoints(crmHr);
        MapWorkforceEndpoints(crmHr);
        MapRecruitingEndpoints(crmHr);

        return group;
    }

    private static void MapPartyEndpoints(RouteGroupBuilder crmHr)
    {
        crmHr.MapGet("/parties", ListPartiesAsync)
            .WithName("ListCrmHrParties")
            .Produces<PartyRecordPage>()
            .ProducesApiErrors(StatusCodes.Status400BadRequest);

        crmHr.MapGet("/parties/{partyId:guid}", async (
                Guid partyId,
                IPartyRecordQueryService partyQueryService,
                CancellationToken cancellationToken) =>
            {
                var party = await partyQueryService.GetAsync(
                    partyId,
                    includeArchived: true,
                    cancellationToken);
                return party is null
                    ? ApiEndpointResults.NotFound("The party was not found.", PartyNotFoundCode)
                    : Results.Ok(party);
            })
            .WithName("GetCrmHrParty");

        crmHr.MapPost("/parties", async (
                PartyCreateApiRequest request,
                PartyDirectoryService partyDirectoryService,
                CancellationToken cancellationToken) =>
            ApiEndpointResults.FromResult(await partyDirectoryService.SavePartyAsync(
                request.ToEditorModel(),
                cancellationToken)))
            .WithName("CreateCrmHrParty");

        crmHr.MapGet("/parties/{partyId:guid}/relationships", async (
                Guid partyId,
                IPartyRecordQueryService partyQueryService,
                PartyDirectoryManagementService managementService,
                CancellationToken cancellationToken) =>
            {
                var party = await partyQueryService.GetAsync(
                    partyId,
                    includeArchived: true,
                    cancellationToken);
                if (party is null)
                {
                    return ApiEndpointResults.NotFound("The party was not found.", PartyNotFoundCode);
                }

                return Results.Ok(await managementService.ListRelationshipsAsync(
                    partyId,
                    cancellationToken));
            })
            .WithName("ListCrmHrPartyRelationships");

        crmHr.MapPut("/parties/{partyId:guid}/relationships", async (
                Guid partyId,
                PartyRelationshipsReplaceApiRequest request,
                PartyDirectoryManagementService managementService,
                CancellationToken cancellationToken) =>
            ApiEndpointResults.FromResult(
                await managementService.SaveRelationshipsAsync(
                    partyId,
                    request.Relationships
                        .Select(relationship => relationship.ToEditorModel())
                        .ToList(),
                    CrmHrApiContractDefaults.Actor,
                    cancellationToken),
                PartyNotFoundCode,
                RelationshipPartyNotFoundCode))
            .WithName("ReplaceCrmHrPartyRelationships");
    }

    private static void MapWorkforceEndpoints(RouteGroupBuilder crmHr)
    {
        crmHr.MapGet("/workforce", async (
                [AsParameters] CrmHrWorkforcePageApiQuery query,
                IPartyRecordQueryService partyQueryService,
                CancellationToken cancellationToken) =>
            await ExecuteBoundedQueryAsync(
                async () => Results.Ok(await partyQueryService.SearchAsync(
                    query.ToQuery(),
                    cancellationToken)),
                "crmhr.workforce.query-invalid"))
            .WithName("ListCrmHrWorkforce");

        crmHr.MapGet("/workforce/{partyId:guid}", async (
                Guid partyId,
                HrService hrService,
                CancellationToken cancellationToken) =>
            {
                var workspace = await hrService.GetWorkforceWorkspaceAsync(
                    partyId,
                    cancellationToken);
                return workspace is null
                    ? ApiEndpointResults.NotFound("The workforce party was not found.", WorkforcePartyNotFoundCode)
                    : Results.Ok(workspace);
            })
            .WithName("GetCrmHrWorkforceWorkspace");

        crmHr.MapPost("/workforce/profiles", async (
                WorkforceProfileSaveApiRequest request,
                HrService hrService,
                CancellationToken cancellationToken) =>
            ApiEndpointResults.FromResult(
                await hrService.SaveWorkforceProfileAsync(
                    request.ToEditorModel(),
                    cancellationToken),
                WorkforcePartyNotFoundCode))
            .WithName("SaveCrmHrWorkforceProfile");

        crmHr.MapGet("/workforce/skills", async (
                HrService hrService,
                CancellationToken cancellationToken) =>
            Results.Ok(await hrService.ListSkillCatalogAsync(cancellationToken)))
            .WithName("ListCrmHrSkillDefinitions");

        crmHr.MapPost("/workforce/skills", async (
                SkillDefinitionSaveApiRequest request,
                HrService hrService,
                CancellationToken cancellationToken) =>
            ApiEndpointResults.FromResult(await hrService.SaveSkillDefinitionAsync(
                request.ToEditorModel(),
                cancellationToken)))
            .WithName("SaveCrmHrSkillDefinition");

        crmHr.MapPost("/workforce/party-skills", async (
                PartySkillSaveApiRequest request,
                HrService hrService,
                CancellationToken cancellationToken) =>
            ApiEndpointResults.FromResult(
                await hrService.SavePartySkillAsync(
                    request.ToEditorModel(),
                    cancellationToken),
                SkillPartyNotFoundCode,
                SkillNotFoundCode))
            .WithName("SaveCrmHrPartySkill");

        crmHr.MapPost("/workforce/capacity-blocks", async (
                CapacityBlockSaveApiRequest request,
                HrService hrService,
                CancellationToken cancellationToken) =>
            ApiEndpointResults.FromResult(
                await hrService.SaveCapacityBlockAsync(
                    request.ToEditorModel(),
                    cancellationToken),
                CapacityPartyNotFoundCode,
                CapacityProjectNotFoundCode))
            .WithName("SaveCrmHrCapacityBlock");
    }

    private static void MapRecruitingEndpoints(RouteGroupBuilder crmHr)
    {
        crmHr.MapGet("/recruiting/applications", async (
                [AsParameters] RecruitmentApplicationPageApiQuery query,
                RecruitingService recruitingService,
                CancellationToken cancellationToken) =>
            await ExecuteBoundedQueryAsync(
                async () => Results.Ok(await recruitingService.SearchRecruitmentApplicationsAsync(
                    query.ToQuery(),
                    cancellationToken)),
                "crmhr.recruiting.query-invalid"))
            .WithName("ListCrmHrRecruitmentApplications");

        crmHr.MapGet("/recruiting/applications/{applicationId:guid}", async (
                Guid applicationId,
                RecruitingService recruitingService,
                CancellationToken cancellationToken) =>
            {
                var workspace = await recruitingService.GetRecruitmentWorkspaceAsync(
                    applicationId,
                    partyId: null,
                    cancellationToken);
                return workspace.HasSelectedApplication
                    ? Results.Ok(workspace)
                    : ApiEndpointResults.NotFound(
                        "The recruitment application was not found.",
                        "crmhr.recruiting.application-not-found");
            })
            .WithName("GetCrmHrRecruitmentApplication");

        crmHr.MapPost("/recruiting/applications", async (
                RecruitmentApplicationSaveApiRequest request,
                RecruitingService recruitingService,
                CancellationToken cancellationToken) =>
            ApiEndpointResults.FromResult(await recruitingService.SaveRecruitmentApplicationAsync(
                request.ToEditorModel(),
                cancellationToken)))
            .WithName("SaveCrmHrRecruitmentApplication");

        crmHr.MapPost("/recruiting/interviews", async (
                RecruitmentInterviewSaveApiRequest request,
                RecruitingService recruitingService,
                CancellationToken cancellationToken) =>
            ApiEndpointResults.FromResult(
                await recruitingService.SaveRecruitmentInterviewAsync(
                    request.ToEditorModel(),
                    cancellationToken),
                InterviewApplicationNotFoundCode))
            .WithName("SaveCrmHrRecruitmentInterview");

        crmHr.MapPost("/recruiting/lifecycle-tasks", async (
                LifecycleTaskSaveApiRequest request,
                RecruitingService recruitingService,
                CancellationToken cancellationToken) =>
            ApiEndpointResults.FromResult(
                await recruitingService.SaveLifecycleTaskAsync(
                    request.ToEditorModel(),
                    cancellationToken),
                LifecyclePartyNotFoundCode,
                LifecycleProjectNotFoundCode))
            .WithName("SaveCrmHrLifecycleTask");

        crmHr.MapPost("/recruiting/support-assignments", async (
                RecruitmentSupportAssignmentsSaveApiRequest request,
                RecruitingService recruitingService,
                CancellationToken cancellationToken) =>
            ApiEndpointResults.FromResult(
                await recruitingService.SaveSupportAssignmentsAsync(
                    request.ToEditorModel(),
                    cancellationToken),
                SupportPartyNotFoundCode))
            .WithName("SaveCrmHrRecruitmentSupportAssignments");

        crmHr.MapPost("/recruiting/conversions", async (
                RecruitmentConversionApiRequest request,
                RecruitingService recruitingService,
                CancellationToken cancellationToken) =>
            ApiEndpointResults.FromResult(
                await recruitingService.ConvertCandidateAsync(
                    request.ToEditorModel(),
                    cancellationToken),
                ConversionApplicationNotFoundCode,
                ConversionPartyNotFoundCode))
            .WithName("ConvertCrmHrRecruitmentCandidate");
    }

    /// <summary>
    /// List CRM/HR parties that match optional text, tag and party-type filters, one page at a time.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Returns people, organizations, organization units and AI agents recorded as CRM/HR parties, ordered by display
    /// name and then party identifier. Archived parties are excluded unless <c>IncludeArchived</c> is true. Use the
    /// returned <c>id</c> with the other <c>/api/crm-hr/parties/{partyId}</c> operations; a party identifier is not a
    /// login account, workforce-profile or recruitment-application identifier.
    /// </para>
    /// <para>
    /// Sensitive parties are listed with their display name, type and status, but their external code, summary and
    /// tags are returned empty, their external code and summary are not searched and they never match a tag filter.
    /// </para>
    /// <para>
    /// Paging is zero-based: request the next page with <c>PageIndex</c> + 1 while <c>pageIndex</c> + 1 is less than
    /// <c>totalPages</c>. Authority: when API authorization is enabled, any valid bearer token issued by this host.
    /// </para>
    /// </remarks>
    /// <param name="query">Search, tag, party-type, paging and archive filters.</param>
    /// <response code="200">The requested page. An empty <c>items</c> array means no party matched.</response>
    /// <response code="400">
    /// A filter is out of range (<c>crmhr.party.query-invalid</c>): negative page index, page size outside 1 through
    /// 100, search text longer than 200 characters, more than 20 tags, or a party-type scope without a supported type.
    /// </response>
    internal static Task<IResult> ListPartiesAsync(
        [AsParameters] CrmHrPartyPageApiQuery query,
        IPartyRecordQueryService partyQueryService,
        CancellationToken cancellationToken)
        => ExecuteBoundedQueryAsync(
            async () => Results.Ok(await partyQueryService.SearchAsync(
                query.ToQuery(PartyRecordPopulation.All),
                cancellationToken)),
            "crmhr.party.query-invalid");

    private static async Task<IResult> ExecuteBoundedQueryAsync(
        Func<Task<IResult>> query,
        string validationCode)
    {
        try
        {
            return await query();
        }
        catch (ArgumentException exception)
        {
            return ApiEndpointResults.BadRequest(exception.Message, validationCode);
        }
    }
}
