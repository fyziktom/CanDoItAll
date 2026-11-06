using System.ComponentModel;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Modules.CrmHr;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

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
        var crmHr = group.MapGroup("/crm-hr").WithApiSection(ApiAccessScopeNames.ReadCrmHr, ApiAccessScopeNames.WriteCrmHr)
            .WithTags("CRM / HR");

        MapPartyEndpoints(crmHr);
        MapWorkforceEndpoints(crmHr);
        MapRecruitingEndpoints(crmHr);
        MapAiStewardEndpoints(crmHr);

        return group;
    }

    private static void MapPartyEndpoints(RouteGroupBuilder crmHr)
    {
        crmHr.MapGet("/parties", ListPartiesAsync)
            .WithName("ListCrmHrParties")
            .Produces<PartyRecordPage>()
            .ProducesApiErrors(StatusCodes.Status400BadRequest);

        crmHr.MapGet("/parties/{partyId:guid}", GetPartyAsync)
            .WithName("GetCrmHrParty")
            .Produces<PartyRecordQueryItem>()
            .ProducesApiErrors(StatusCodes.Status404NotFound);

        crmHr.MapPost("/parties", CreatePartyAsync)
            .WithName("CreateCrmHrParty")
            .Produces<Guid>()
            .ProducesApiErrors(StatusCodes.Status400BadRequest);

        crmHr.MapGet("/parties/{partyId:guid}/relationships", ListPartyRelationshipsAsync)
            .WithName("ListCrmHrPartyRelationships")
            .Produces<IReadOnlyList<PartyRelationshipListItemModel>>()
            .ProducesApiErrors(StatusCodes.Status404NotFound);

        crmHr.MapPut("/parties/{partyId:guid}/relationships", ReplacePartyRelationshipsAsync)
            .WithName("ReplaceCrmHrPartyRelationships")
            .DescribeApi("Conditionally replace all relationships of a CRM/HR party.",
                "Send the complete desired set and the complete original relationship identities and versions returned by the relationship read. Original is required even for an originally empty set. Concurrent additions, changes or deletions reject the entire replacement with HTTP 409. An empty desired set removes all observed relationships. When API authorization is enabled, the caller must satisfy the CRM/HR capability policy; Original is a state precondition and never grants authority.",
                "The exact accepted complete relationship set and its new owner-issued versions, wrapped with the addressed party identifier.",
                "Complete desired relationships and the unchanged original relationship-set observation for the route party.")
            .Produces<PartyRelationshipSaveReceipt>()
            .ProducesApiErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict);
    }

    private static void MapAiStewardEndpoints(RouteGroupBuilder crmHr) {
        crmHr.MapGet("/ai-stewards/{partyId:guid}", GetAiStewardAsync)
            .WithName("GetCrmHrAiSteward")
            .DescribeApi("Read the business stewardship editor and original of an AI party.",
                "Returns business ownership, review state, notes and the exact original, including an absent-profile observation for a party with no stewardship profile. Technical Agent settings are excluded. The current principal is checked before and after the read; both CRM/HR read and write capabilities are required when authorization is enabled. Original is a state precondition and never grants authority.",
                "The stewardship editor and owner-issued original for the addressed AI party; a null profile ID records profile absence.")
            .WithApiPermission(ApiAccessScopeNames.WriteCrmHr)
            .Produces<AiStewardProfileEditorModel>().ProducesApiErrors(StatusCodes.Status404NotFound);
        crmHr.MapPut("/ai-stewards/{partyId:guid}", SaveAiStewardAsync)
            .WithName("ReplaceCrmHrAiSteward")
            .DescribeApi("Conditionally save an AI party business stewardship profile.",
                "Read the stewardship editor first and send its Original unchanged, even for an absent profile. The route party, profile identity, party type and original business fields must still match. Peer changes reject the write with HTTP 409. Technical Agent provider, model, capabilities and execution settings are unchanged. The current principal and both CRM/HR read and write capabilities are rechecked at admission when authorization is enabled. Original is a state precondition and never grants authority.",
                "The native accepted stewardship profile identifier and normalized editor, including its new original for a later conditional save.",
                "Complete proposed business stewardship fields and the unchanged original from the stewardship GET for the route party.")
            .Produces<AiStewardProfileSaveReceipt>()
            .ProducesApiErrors(StatusCodes.Status400BadRequest, StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> GetAiStewardAsync(
        [Description("GUID of the AI party owning the business stewardship profile; this is not a technical Agent identifier.")] Guid partyId, IAiStewardProfileEditing editing,
        WebCurrentPrincipalResolver principals, IOptions<ApiAccessOptions> options, CancellationToken cancellationToken) {
        try {
            var demand = await AiStewardAuthorityAsync(principals, options, cancellationToken);
            var model = await editing.ReadAsync(partyId, cancellationToken);
            await demand(cancellationToken);
            return model is null ? ApiEndpointResults.NotFound("The AI party was not found.", "crmhr.ai-steward.not-found") : Results.Ok(model);
        } catch (WebCurrentPrincipalDeniedException) {
            return Results.Forbid();
        }
    }

    private static async Task<IResult> SaveAiStewardAsync(
        [Description("GUID of the AI party owning the business stewardship profile; this is not a technical Agent identifier.")] Guid partyId, AiStewardProfileSaveApiRequest request,
        IAiStewardProfileEditing editing, CrmHrChanges changes, WebCurrentPrincipalResolver principals,
        IOptions<ApiAccessOptions> options, CancellationToken cancellationToken) {
        try {
            var demand = await AiStewardAuthorityAsync(principals, options, cancellationToken);
            using var admission = changes.EnterUiWrite(demand);
            return await SaveAiStewardAdmittedAsync(partyId, request, editing, cancellationToken);
        } catch (WebCurrentPrincipalDeniedException) {
            return Results.Forbid();
        }
    }

    private static async Task<IResult> SaveAiStewardAdmittedAsync(Guid partyId, AiStewardProfileSaveApiRequest request,
        IAiStewardProfileEditing editing, CancellationToken cancellationToken) {
        AiStewardProfileSaveReceipt? accepted = null;
        var result = await editing.SaveAsync(new() {
            Id = request.Id,
            PartyId = partyId,
            OwnerPartyId = request.OwnerPartyId,
            ValidationStatus = request.ValidationStatus,
            LastReviewedOn = request.LastReviewedOn,
            Notes = request.Notes,
            LastChangedBy = request.Actor,
            Original = request.Original
        }, cancellationToken, receipt => accepted = receipt);
        return result.IsSuccess
            ? Results.Ok(accepted ?? throw new InvalidOperationException("The stewardship owner did not acknowledge its accepted write."))
            : CrmHrEditingResult(result);
    }

    private static async Task<Func<CancellationToken, ValueTask>> AiStewardAuthorityAsync(WebCurrentPrincipalResolver principals,
        IOptions<ApiAccessOptions> options, CancellationToken cancellationToken) {
        if (!options.Value.Authorization.Enabled) {
            return _ => ValueTask.CompletedTask;
        }
        var initial = await principals.ResolveAsync(cancellationToken);
        async ValueTask Demand(CancellationToken token) {
            var current = await principals.ResolveAsync(token);
            if (current.Stamp != initial.Stamp) {
                throw new WebCurrentPrincipalDeniedException();
            }
            await principals.RequireScopeAsync(current, ApiAccessScopeNames.ReadCrmHr, ApiAccessScopeNames.ReadCrmHr, token);
            await principals.RequireScopeAsync(current, ApiAccessScopeNames.WriteCrmHr, ApiAccessScopeNames.WriteCrmHr, token);
        }
        await Demand(cancellationToken);
        return Demand;
    }

    private static void MapWorkforceEndpoints(RouteGroupBuilder crmHr)
    {
        crmHr.MapGet("/workforce", ListWorkforceAsync)
            .WithName("ListCrmHrWorkforce")
            .Produces<PartyRecordPage>()
            .ProducesApiErrors(StatusCodes.Status400BadRequest);

        crmHr.MapGet("/workforce/{partyId:guid}", GetWorkforceWorkspaceAsync)
            .WithName("GetCrmHrWorkforceWorkspace")
            .Produces<WorkforceWorkspaceModel>()
            .ProducesApiErrors(StatusCodes.Status404NotFound);

        crmHr.MapPost("/workforce/profiles", SaveWorkforceProfileAsync)
            .WithName("SaveCrmHrWorkforceProfile")
            .DescribeApi("Create or conditionally replace a party workforce profile.",
                "Read the workforce workspace and send its profile Original unchanged for replacement. Omission permits only creation while no profile exists. A profile that appeared or changed since the original was read rejects the entire save with HTTP 409. The party type, manager, unit and workforce values must satisfy their documented constraints. When API authorization is enabled, the caller must satisfy the CRM/HR capability policy; Original is a state precondition and never grants authority.",
                "The workforce profile identifier as a JSON GUID string; read the workforce workspace for its accepted values and next original.",
                "Complete proposed workforce profile values for PartyId and the original profile observation for conditional replacement.")
            .Produces<Guid>()
            .ProducesApiErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict);

        crmHr.MapGet("/workforce/skills", ListSkillDefinitionsAsync)
            .WithName("ListCrmHrSkillDefinitions")
            .Produces<IReadOnlyList<SkillCatalogItemModel>>();

        crmHr.MapPost("/workforce/skills", SaveSkillDefinitionAsync)
            .WithName("SaveCrmHrSkillDefinition")
            .DescribeApi("Create or conditionally replace a workforce skill definition.",
                "Omit Id for create-only intent. For replacement send the existing Id and unchanged Original from the skill catalog. Unknown identifiers, peer changes and same-name collisions never silently overwrite another definition; state conflicts return HTTP 409. When API authorization is enabled, the caller must satisfy the CRM/HR capability policy; Original is a state precondition and never grants authority.",
                "The accepted skill definition identifier as a JSON GUID string; reread the skill catalog before another replacement.",
                "Complete proposed skill definition values, with the existing identifier and unchanged catalog Original for replacement.")
            .Produces<Guid>()
            .ProducesApiErrors(StatusCodes.Status400BadRequest, StatusCodes.Status409Conflict);

        crmHr.MapPost("/workforce/party-skills", SavePartySkillAsync)
            .WithName("SaveCrmHrPartySkill")
            .DescribeApi("Create or conditionally replace a worker skill row.",
                "Omit Id for creation; for replacement send Id and unchanged Original from the workforce workspace. The row cannot move to another worker. A changed or missing original row or an occupied worker/skill pair rejects the save with HTTP 409. When API authorization is enabled, the caller must satisfy the CRM/HR capability policy; Original is a state precondition and never grants authority.",
                "The accepted worker skill row identifier as a JSON GUID string; read the worker workspace for accepted values and the next original.",
                "Complete proposed worker skill values and the unchanged row Original when replacing an existing identifier.")
            .Produces<Guid>()
            .ProducesApiErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict);

        crmHr.MapPost("/workforce/capacity-blocks", SaveCapacityBlockAsync)
            .WithName("SaveCrmHrCapacityBlock")
            .DescribeApi("Create or conditionally replace a worker capacity block.",
                "Omit Id for creation; replacement requires the exact row Original from the workforce workspace. Unknown update identifiers do not create rows or move them to another worker. A related project requires the owner-issued RelatedProjectAdmission captured when choosing that project. Peer changes reject the save with HTTP 409. When API authorization is enabled, the caller must satisfy the CRM/HR capability policy; Original is a state precondition and never grants authority.",
                "The accepted capacity block identifier as a JSON GUID string; reread the workforce workspace before a later replacement.",
                "Complete proposed capacity block, its row Original for replacement, and the original project lifetime admission whenever RelatedProjectId is supplied.")
            .Produces<Guid>()
            .ProducesApiErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict);
    }

    private static void MapRecruitingEndpoints(RouteGroupBuilder crmHr)
    {
        crmHr.MapGet("/recruiting/applications", ListRecruitmentApplicationsAsync)
            .WithName("ListCrmHrRecruitmentApplications")
            .Produces<RecruitmentApplicationPage>()
            .ProducesApiErrors(StatusCodes.Status400BadRequest);

        crmHr.MapGet("/recruiting/applications/{applicationId:guid}", GetRecruitmentApplicationAsync)
            .WithName("GetCrmHrRecruitmentApplication")
            .Produces<RecruitmentWorkspaceModel>()
            .ProducesApiErrors(StatusCodes.Status404NotFound);

        crmHr.MapPost("/recruiting/applications", SaveRecruitmentApplicationAsync)
            .WithName("SaveCrmHrRecruitmentApplication")
            .DescribeApi("Create or conditionally replace a recruitment application.",
                "Omit Id for create-only application intent. Creation can use an existing PartyId or the documented new-candidate fields. Replacement requires Id and unchanged Original from the recruitment workspace and cannot move the application to another party. A changed or missing original rejects the save with HTTP 409. When API authorization is enabled, the caller must satisfy the CRM/HR capability policy; Original is a state precondition and never grants authority.",
                "The accepted application identifier as a JSON GUID string; read its recruitment workspace for accepted values and next original.",
                "Complete proposed application values, candidate identity or creation fields, and the unchanged application Original for replacement.")
            .Produces<Guid>()
            .ProducesApiErrors(StatusCodes.Status400BadRequest, StatusCodes.Status409Conflict);

        crmHr.MapPost("/recruiting/interviews", SaveRecruitmentInterviewAsync)
            .WithName("SaveCrmHrRecruitmentInterview")
            .DescribeApi("Create or conditionally replace a recruitment interview.",
                "Omit Id for a new interview. Replacement requires Id and unchanged Original read with the recruitment workspace; it cannot move an interview to another application. A changed or missing original rejects the save with HTTP 409. When API authorization is enabled, the caller must satisfy the CRM/HR capability policy; Original is a state precondition and never grants authority.",
                "The accepted interview identifier as a JSON GUID string; reread the recruitment workspace for its next original.",
                "Complete proposed interview fields and the unchanged row Original when replacing an existing interview.")
            .Produces<Guid>()
            .ProducesApiErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict);

        crmHr.MapPost("/recruiting/lifecycle-tasks", SaveLifecycleTaskAsync)
            .WithName("SaveCrmHrLifecycleTask")
            .DescribeApi("Create or conditionally replace a party lifecycle task.",
                "This is an onboarding, offboarding or training task, separate from Project Structure tasks. Omit Id for creation. Replacement requires the exact row Original from the recruitment workspace and cannot move the task to another party. A related project requires its captured RelatedProjectAdmission. A changed or missing original rejects the save with HTTP 409. When API authorization is enabled, the caller must satisfy the CRM/HR capability policy; Original is a state precondition and never grants authority.",
                "The accepted lifecycle task identifier as a JSON GUID string; reread the recruitment workspace for its accepted fields and next original.",
                "Complete proposed lifecycle task, row Original for replacement, and the captured project lifetime admission when a related project is supplied.")
            .Produces<Guid>()
            .ProducesApiErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict);

        crmHr.MapPost("/recruiting/support-assignments", SaveSupportAssignmentsAsync)
            .WithName("SaveCrmHrRecruitmentSupportAssignments")
            .DescribeApi("Conditionally replace a party manager, buddy and mentor.",
                "Send the complete desired support assignments and unchanged complete relationship-set Original read with the recruitment workspace. Null assignments remove the corresponding support roles; unrelated relationships are preserved. A peer relationship change rejects the entire replacement with HTTP 409. When API authorization is enabled, the caller must satisfy the CRM/HR capability policy; Original is a state precondition and never grants authority.",
                "The exact accepted complete relationship set with owner-issued versions, wrapped with the party identifier.",
                "Party identifier, complete desired manager, buddy and mentor assignments, and the unchanged complete relationship-set Original.")
            .Produces<PartyRelationshipSaveReceipt>()
            .ProducesApiErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict);

        crmHr.MapPost("/recruiting/conversions", ConvertCandidateAsync)
            .WithName("ConvertCrmHrRecruitmentCandidate")
            .DescribeApi("Conditionally convert an approved candidate into workforce.",
                "Read the recruitment conversion editor and submit its entire Original unchanged. Conversion checks the original application, workforce and party lifecycle together; a peer change rejects the conversion with HTTP 409. The application must have an approved decision and an eligible stage; AI candidates also require the documented ready assessment. The requested workforce values must satisfy workforce rules. When API authorization is enabled, the caller must satisfy the CRM/HR capability policy; Original is a state precondition and never grants authority.",
                "The candidate party identifier as a JSON GUID string, rather than a workforce profile identifier; reread its workforce and recruitment workspaces.",
                "Application identifier, complete proposed workforce values and the unchanged composite conversion Original.")
            .Produces<Guid>()
            .ProducesApiErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict);
    }

    /// <summary>
    /// List CRM/HR parties that match optional text, tag and party-type filters, one page at a time.
    /// </summary>
    /// <remarks>
    /// Returns people, organizations, organization units and AI agents recorded as CRM/HR parties, ordered by display
    /// name and then party identifier. Archived parties are excluded unless <c>IncludeArchived</c> is true. Use the
    /// returned <c>id</c> with the other <c>/api/crm-hr/parties/{partyId}</c> operations; a party identifier is not a
    /// login account, workforce-profile or recruitment-application identifier.
    ///
    /// Sensitive parties are listed with their display name, type and status, but their external code, summary and
    /// tags are returned empty, their external code and summary are not searched and they never match a tag filter.
    ///
    /// Paging is zero-based: request the next page with <c>PageIndex</c> + 1 while <c>pageIndex</c> + 1 is less than
    /// <c>totalPages</c>. Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy.
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

    /// <summary>
    /// Read the directory summary of one CRM/HR party.
    /// </summary>
    /// <remarks>
    /// Returns, for one party identifier, the summary that the party list returns, whether or not the party is
    /// archived. Use it to check that a party exists and to read its type and lifecycle status before you reference it
    /// from relationships, workforce records or recruitment applications. It does not include roles, contact points or
    /// addresses.
    ///
    /// For a sensitive party the external code, summary and tags are returned empty.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy.
    /// </remarks>
    /// <param name="partyId">
    /// Identifier of the party, as returned in <c>items[].id</c> by <c>GET /api/crm-hr/parties</c> or by
    /// <c>POST /api/crm-hr/parties</c>.
    /// </param>
    /// <response code="200">The party summary.</response>
    /// <response code="404">No party has this identifier (<c>crmhr.party.not-found</c>).</response>
    internal static async Task<IResult> GetPartyAsync(
        Guid partyId,
        IPartyRecordQueryService partyQueryService,
        CancellationToken cancellationToken)
    {
        var party = await partyQueryService.GetAsync(
            partyId,
            includeArchived: true,
            cancellationToken);
        return party is null
            ? ApiEndpointResults.NotFound("The party was not found.", PartyNotFoundCode)
            : Results.Ok(party);
    }

    /// <summary>
    /// Create a CRM/HR party together with its roles, public contact points and addresses.
    /// </summary>
    /// <remarks>
    /// Every successful call creates a new party with a new identifier; this operation cannot update an existing party
    /// and is not idempotent. The external code is stored as sent and is not checked for uniqueness. To avoid
    /// duplicates, search with <c>GET /api/crm-hr/parties</c> before creating, and search again before retrying after a
    /// timeout or server error: the party is saved before the search index and the activity feed are updated, so a
    /// failed response can follow a saved party.
    ///
    /// Request rules:
    ///
    /// - <c>displayName</c> is required. Text values are trimmed.
    /// - Tags are trimmed; blank tags and case-insensitive duplicates are dropped.
    /// - Every entry of <c>publicContacts</c> is stored as a contact point with the public flag set. The flag is a data
    /// classification; it does not make the party or the contact readable without authorization. At most one contact
    /// point per contact type can be primary.
    /// - Confidential notes cannot be created through this operation.
    /// - <c>isSensitive</c> true withholds the external code, summary and tags from directory reads and keeps the party
    /// out of the application search index.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy. The change is recorded
    /// as made by <c>crm-hr-api</c>.
    /// </remarks>
    /// <param name="request">
    /// The party to create: identity, classification, roles, public contacts and addresses.
    /// </param>
    /// <response code="200">
    /// The party was created. The body is its new identifier as a JSON string (a GUID); use it as <c>partyId</c> in the
    /// other CRM/HR operations.
    /// </response>
    /// <response code="400">
    /// The party was not created: <c>crmhr.party.display-name-required</c> (blank display name) or
    /// <c>crmhr.party.multiple-primary-contacts</c> (more than one primary contact point of the same contact type). A
    /// body the framework cannot bind is rejected with HTTP 400 without the <c>errors</c> envelope.
    /// </response>
    internal static async Task<IResult> CreatePartyAsync(
        PartyCreateApiRequest request,
        PartyDirectoryService partyDirectoryService,
        CancellationToken cancellationToken)
        => ApiEndpointResults.FromResult(await partyDirectoryService.SavePartyAsync(
            request.ToEditorModel(),
            cancellationToken));

    /// <summary>
    /// List every relationship in which a CRM/HR party is the source or the target.
    /// </summary>
    /// <remarks>
    /// Each item describes the relationship from the point of view of the party in the route: <c>relatedPartyId</c> is
    /// the other party, and <c>isOutgoing</c> is true when the route party is the source. A relationship reads source,
    /// kind, target: with <c>isOutgoing</c> true and kind MemberOf, the route party is a member of the related party.
    /// Items are ordered by the related party's display name (case-insensitive), then by relationship kind, related
    /// party identifier and relationship identifier.
    ///
    /// Read this list before <c>PUT /api/crm-hr/parties/{partyId}/relationships</c>: that operation replaces the whole
    /// list, in both directions.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy.
    /// </remarks>
    /// <param name="partyId">
    /// Identifier of the party whose relationships are listed, as returned by <c>GET /api/crm-hr/parties</c>. Archived
    /// parties are accepted.
    /// </param>
    /// <response code="200">The party's relationships; an empty array means it has none.</response>
    /// <response code="404">No party has this identifier (<c>crmhr.party.not-found</c>).</response>
    internal static async Task<IResult> ListPartyRelationshipsAsync(
        Guid partyId,
        IPartyRecordQueryService partyQueryService,
        PartyDirectoryManagementService managementService,
        CancellationToken cancellationToken)
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
    }

    /// <summary>
    /// Replace the complete set of relationships of a CRM/HR party.
    /// </summary>
    /// <remarks>
    /// This is a full replacement, not a patch. The server deletes every existing relationship in which the route party
    /// is the source or the target, including relationships created from the other party's side and the manager, buddy
    /// and mentor relationships saved by <c>POST /api/crm-hr/recruiting/support-assignments</c>, and then stores
    /// exactly the submitted list. An empty <c>relationships</c> array, or a body without that member, removes all
    /// relationships of the party. Send an array, never null. Every stored relationship gets a new identifier on each
    /// call.
    ///
    /// Read, modify, write:
    ///
    /// 1. Call <c>GET /api/crm-hr/parties/{partyId}/relationships</c>.
    /// 2. Copy every item you want to keep into the request with the same <c>relatedPartyId</c>,
    /// <c>relationshipKind</c>, <c>isOutgoing</c>, <c>isPrimary</c>, dates and <c>notes</c>, then add or remove items.
    /// 3. Send the complete list and read it again.
    ///
    /// Each item must reference another existing party, and the same source, target and kind can appear only once.
    /// When any item is rejected nothing is changed. There is no concurrency check: a replacement saved by another
    /// client after your read is overwritten.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy.
    /// </remarks>
    /// <param name="partyId">
    /// Identifier of the party whose relationships are replaced, as returned by <c>GET /api/crm-hr/parties</c>.
    /// </param>
    /// <param name="request">The complete intended list of the party's relationships.</param>
    /// <response code="200">
    /// The relationships were replaced.
    /// </response>
    /// <response code="400">
    /// Nothing was changed: <c>crmhr.party.relationship-invalid</c> (an item has no related party or points to the
    /// route party itself) or <c>crmhr.party.relationship-duplicate</c> (the same source, target and kind twice). A
    /// body the framework cannot bind is rejected with HTTP 400 without the <c>errors</c> envelope.
    /// </response>
    /// <response code="404">
    /// Nothing was changed: the route party does not exist (<c>crmhr.party.not-found</c>) or one or more related
    /// parties do not exist (<c>crmhr.party.relationship-party-not-found</c>; the message lists their identifiers).
    /// </response>
    internal static async Task<IResult> ReplacePartyRelationshipsAsync(
        Guid partyId,
        PartyRelationshipsReplaceApiRequest request,
        PartyDirectoryManagementService managementService,
        CancellationToken cancellationToken) {
        var result = await managementService.SaveRelationshipsAsync(partyId,
            request.Relationships.Select(relationship => relationship.ToEditorModel()).ToList(),
            CrmHrApiContractDefaults.Actor, cancellationToken, request.Original);
        if (result.IsFailure && result.Errors.Any(error => error.Code == CrmHrEditingErrorCodes.RelationshipConflict)) {
            var conflict = result.Errors.First(error => error.Code == CrmHrEditingErrorCodes.RelationshipConflict);
            return ApiEndpointResults.Conflict(conflict.Message, conflict.Code);
        }
        return ApiEndpointResults.FromResult(result, PartyNotFoundCode, RelationshipPartyNotFoundCode);
    }

    /// <summary>
    /// List CRM/HR parties of the workforce population, one page at a time.
    /// </summary>
    /// <remarks>
    /// Returns the same party summaries as <c>GET /api/crm-hr/parties</c>, limited to the workforce population: every
    /// person and organization unit, plus organizations that have a workforce profile or the DeliveryUnit role. AI
    /// agent parties are never included, and a listed party does not necessarily have a workforce profile. Read
    /// <c>GET /api/crm-hr/workforce/{partyId}</c> for the profile, skills and capacity of one party.
    ///
    /// Search, tag, sensitivity and paging rules are those of the party list: items are ordered by display name and
    /// then party identifier, archived parties are excluded unless <c>IncludeArchived</c> is true, search text matches
    /// the display name and, for parties that are not sensitive, the external code and summary, sensitive parties
    /// return an empty external code, summary and tags, and paging is zero-based.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy.
    /// </remarks>
    /// <param name="query">Search, tag, paging and archive filters.</param>
    /// <response code="200">The requested page. An empty <c>items</c> array means no party matched.</response>
    /// <response code="400">
    /// A filter is out of range (<c>crmhr.workforce.query-invalid</c>): negative page index, page size outside 1
    /// through 100, search text longer than 200 characters or more than 20 tags.
    /// </response>
    internal static async Task<IResult> ListWorkforceAsync(
        [AsParameters] CrmHrWorkforcePageApiQuery query,
        IPartyRecordQueryService partyQueryService,
        CancellationToken cancellationToken)
        => await ExecuteBoundedQueryAsync(
            async () => Results.Ok(await partyQueryService.SearchAsync(
                query.ToQuery(),
                cancellationToken)),
            "crmhr.workforce.query-invalid");

    /// <summary>
    /// Read the workforce workspace of a CRM/HR party: profile, skills, capacity blocks and project allocations.
    /// </summary>
    /// <remarks>
    /// Combines, for one person, organization or organization unit: the party summary, its workforce profile, the whole
    /// skill catalog, the party's skills, its capacity blocks, its project allocations and a capacity summary
    /// calculated for the current UTC date. When no profile was saved yet, <c>profile</c> holds default values and its
    /// <c>id</c> is null. Use <c>profile</c> as the starting point for <c>POST /api/crm-hr/workforce/profiles</c>.
    ///
    /// Project allocations are the party's project participations and task work assignments that carry an allocation
    /// percentage, including past ones. Only allocations bound to the current, not retired lifetime of their project
    /// count in the capacity summary. An allocation whose project no longer exists, or was recreated with the same
    /// identifier, shows an empty project name.
    ///
    /// For a sensitive party the primary email and phone are returned empty; the summary and the other fields are
    /// returned. The response contains personal and HR data: do not log it.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy.
    /// </remarks>
    /// <param name="partyId">
    /// Identifier of the party, as returned by <c>GET /api/crm-hr/workforce</c> or <c>GET /api/crm-hr/parties</c>; not
    /// a workforce profile identifier.
    /// </param>
    /// <response code="200">The workforce workspace of the party.</response>
    /// <response code="404">
    /// No person, organization or organization unit has this identifier (<c>crmhr.workforce.party-not-found</c>). AI
    /// agent parties have no workforce workspace.
    /// </response>
    internal static async Task<IResult> GetWorkforceWorkspaceAsync(
        Guid partyId,
        HrService hrService,
        CancellationToken cancellationToken)
    {
        var workspace = await hrService.GetWorkforceWorkspaceAsync(
            partyId,
            cancellationToken);
        return workspace is null
            ? ApiEndpointResults.NotFound("The workforce party was not found.", WorkforcePartyNotFoundCode)
            : Results.Ok(workspace);
    }

    /// <summary>
    /// Create or replace the workforce profile of a CRM/HR party.
    /// </summary>
    /// <param name="request">All fields of the party's workforce profile.</param>
    /// <response code="200">
    /// The profile was saved. The body is the workforce profile identifier as a JSON string (a GUID), which is not the
    /// party identifier; read <c>GET /api/crm-hr/workforce/{partyId}</c> for the stored values.
    /// </response>
    /// <response code="400">
    /// The profile was not saved: <c>crmhr.workforce.party-required</c>, <c>crmhr.workforce.self-manager</c>,
    /// <c>crmhr.workforce.self-home-unit</c>, <c>crmhr.workforce.rate-negative</c>,
    /// <c>crmhr.workforce.rate-unit-invalid</c>, <c>crmhr.workforce.rate-currency-invalid</c>,
    /// <c>crmhr.workforce.person-delivery-unit</c>, <c>crmhr.workforce.organization-kind</c>,
    /// <c>crmhr.workforce.manager-invalid</c> or <c>crmhr.workforce.home-unit-invalid</c>. A body the framework cannot
    /// bind is rejected with HTTP 400 without the <c>errors</c> envelope.
    /// </response>
    /// <response code="404">The party does not exist (<c>crmhr.workforce.party-not-found</c>).</response>
    internal static async Task<IResult> SaveWorkforceProfileAsync(
        WorkforceProfileSaveApiRequest request,
        HrService hrService,
        CancellationToken cancellationToken)
        => CrmHrEditingResult(
            await hrService.SaveWorkforceProfileAsync(
                request.ToEditorModel(),
                cancellationToken),
            WorkforcePartyNotFoundCode);

    /// <summary>
    /// List the workforce skill catalog.
    /// </summary>
    /// <remarks>
    /// Returns every skill definition, active ones first, then ordered by category and name. A skill definition is a
    /// catalog entry that party skills refer to by <c>skillId</c>; it is not an agent skill or capability. The list is
    /// not paged. The workforce workspace returns the same catalog in <c>skillCatalog</c>.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy.
    /// </remarks>
    /// <response code="200">
    /// All skill definitions, including inactive ones; an empty array when the catalog is empty.
    /// </response>
    internal static async Task<IResult> ListSkillDefinitionsAsync(
        HrService hrService,
        CancellationToken cancellationToken)
        => Results.Ok(await hrService.ListSkillCatalogAsync(cancellationToken));

    /// <summary>
    /// Create or update a skill definition in the workforce skill catalog.
    /// </summary>
    /// <param name="request">The skill definition fields.</param>
    /// <response code="200">
    /// The definition was saved. The body is its identifier as a JSON string (a GUID); use it as <c>skillId</c> of a
    /// party skill.
    /// </response>
    /// <response code="400">
    /// The name is blank (<c>crmhr.skills.name-required</c>). A body the framework cannot bind is rejected with HTTP
    /// 400 without the <c>errors</c> envelope.
    /// </response>
    internal static async Task<IResult> SaveSkillDefinitionAsync(
        SkillDefinitionSaveApiRequest request,
        HrService hrService,
        CancellationToken cancellationToken)
        => CrmHrEditingResult(await hrService.SaveSkillDefinitionAsync(
            request.ToEditorModel(),
            cancellationToken));

    /// <summary>
    /// Record or update a CRM/HR party's proficiency in a catalog skill.
    /// </summary>
    /// <param name="request">The party, the skill and the proficiency details.</param>
    /// <response code="200">
    /// The party skill was saved. The body is its identifier as a JSON string (a GUID); read it back in <c>skills</c>
    /// of <c>GET /api/crm-hr/workforce/{partyId}</c>.
    /// </response>
    /// <response code="400">
    /// <c>partyId</c> or <c>skillId</c> is missing (<c>crmhr.skills.party-required</c>,
    /// <c>crmhr.skills.skill-required</c>). A body the framework cannot bind is rejected with HTTP 400 without the
    /// <c>errors</c> envelope.
    /// </response>
    /// <response code="404">
    /// The party (<c>crmhr.skills.party-not-found</c>) or the skill definition (<c>crmhr.skills.skill-not-found</c>)
    /// does not exist.
    /// </response>
    internal static async Task<IResult> SavePartySkillAsync(
        PartySkillSaveApiRequest request,
        HrService hrService,
        CancellationToken cancellationToken)
        => CrmHrEditingResult(
            await hrService.SavePartySkillAsync(
                request.ToEditorModel(),
                cancellationToken),
            SkillPartyNotFoundCode,
            SkillNotFoundCode);

    /// <summary>
    /// Create or update a capacity block that reduces the availability of a CRM/HR party for a date range.
    /// </summary>
    /// <param name="request">The party, the block kind, the date range, the percentage and an optional project.</param>
    /// <response code="200">
    /// The block was saved. The body is its identifier as a JSON string (a GUID); read it back in
    /// <c>capacityBlocks</c> of <c>GET /api/crm-hr/workforce/{partyId}</c>.
    /// </response>
    /// <response code="400">
    /// The block was not saved: <c>crmhr.capacity.party-required</c>, <c>crmhr.capacity.date-required</c>,
    /// <c>crmhr.capacity.date-range-invalid</c> or <c>crmhr.capacity.percentage-range</c>. A body the framework cannot
    /// bind is rejected with HTTP 400 without the <c>errors</c> envelope.
    /// </response>
    /// <response code="404">
    /// The party (<c>crmhr.capacity.party-not-found</c>) or the related project
    /// (<c>crmhr.capacity.project-not-found</c>) does not exist.
    /// </response>
    internal static async Task<IResult> SaveCapacityBlockAsync(
        CapacityBlockSaveApiRequest request,
        HrService hrService,
        CancellationToken cancellationToken)
        => CrmHrEditingResult(
            await hrService.SaveCapacityBlockAsync(
                request.ToEditorModel(),
                cancellationToken),
            CapacityPartyNotFoundCode,
            CapacityProjectNotFoundCode);

    /// <summary>
    /// List recruitment applications that match optional text and stage filters, one page at a time.
    /// </summary>
    /// <remarks>
    /// A recruitment application is a candidate party's application for a role; its identifier is not the candidate's
    /// party identifier. Items are ordered by last update, newest first, then by application identifier. Search text
    /// matches, case-insensitively as a substring, the desired role, the source, the display names of the candidate,
    /// recruiter, hiring manager and target unit, and the candidate's public email addresses.
    ///
    /// Paging is zero-based and <c>totalCount</c> counts all matches: request the next page while (<c>pageIndex</c> +
    /// 1) multiplied by <c>pageSize</c> is less than <c>totalCount</c>. Items carry the candidate's primary public
    /// email and phone, also for sensitive candidates.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy.
    /// </remarks>
    /// <param name="query">Search, stage and paging filters.</param>
    /// <response code="200">The requested page. An empty <c>items</c> array means no application matched.</response>
    /// <response code="400">
    /// A filter is out of range (<c>crmhr.recruiting.query-invalid</c>): negative page index, page size outside 1
    /// through 100, search text longer than 200 characters or an unknown stage scope.
    /// </response>
    internal static async Task<IResult> ListRecruitmentApplicationsAsync(
        [AsParameters] RecruitmentApplicationPageApiQuery query,
        RecruitingService recruitingService,
        CancellationToken cancellationToken)
        => await ExecuteBoundedQueryAsync(
            async () => Results.Ok(await recruitingService.SearchRecruitmentApplicationsAsync(
                query.ToQuery(),
                cancellationToken)),
            "crmhr.recruiting.query-invalid");

    /// <summary>
    /// Read the recruitment workspace of one application, with its candidate, history, interviews, tasks and support.
    /// </summary>
    /// <remarks>
    /// Returns the application, the candidate's name, summary and primary public contacts, the stage history of the
    /// application (newest first), its interviews (by scheduled time), the candidate party's lifecycle tasks and
    /// support assignments, and a conversion request prepared from the application and any existing workforce profile.
    /// Lifecycle tasks and support assignments are stored per party, not per application.
    ///
    /// Use it as the source of the recruiting writes: send <c>application</c> back to
    /// <c>POST /api/crm-hr/recruiting/applications</c> after changing it, and review <c>conversion</c> before
    /// <c>POST /api/crm-hr/recruiting/conversions</c>.
    ///
    /// The response contains personal data, including the contact values and summary of sensitive candidates: do not
    /// log it.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy.
    /// </remarks>
    /// <param name="applicationId">
    /// Identifier of the recruitment application, as returned by <c>GET /api/crm-hr/recruiting/applications</c> or
    /// <c>POST /api/crm-hr/recruiting/applications</c>; not the candidate's party identifier.
    /// </param>
    /// <response code="200">The application workspace; <c>hasSelectedApplication</c> is true.</response>
    /// <response code="404">
    /// No recruitment application has this identifier (<c>crmhr.recruiting.application-not-found</c>).
    /// </response>
    internal static async Task<IResult> GetRecruitmentApplicationAsync(
        Guid applicationId,
        RecruitingService recruitingService,
        CancellationToken cancellationToken)
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
    }

    /// <summary>
    /// Create or update a recruitment application, creating the candidate party when needed.
    /// </summary>
    /// <param name="request">
    /// The application fields and either the candidate party or the new candidate's details.
    /// </param>
    /// <response code="200">
    /// The application was saved. The body is the recruitment application identifier as a JSON string (a GUID), not the
    /// candidate's party identifier; read the workspace for the candidate's <c>partyId</c>.
    /// </response>
    /// <response code="400">
    /// The application was not saved: <c>crmhr.recruiting.role-required</c>, <c>crmhr.recruiting.candidate-invalid</c>
    /// (the party is not an existing person or AI agent), <c>crmhr.recruiting.candidate-required</c> (neither
    /// <c>partyId</c> nor <c>candidateName</c>), <c>crmhr.recruiting.recruiter-invalid</c>,
    /// <c>crmhr.recruiting.hiring-manager-self</c>, <c>crmhr.recruiting.hiring-manager-invalid</c> or
    /// <c>crmhr.recruiting.target-unit-invalid</c>. A body the framework cannot bind is rejected with HTTP 400 without
    /// the <c>errors</c> envelope.
    /// </response>
    internal static async Task<IResult> SaveRecruitmentApplicationAsync(
        RecruitmentApplicationSaveApiRequest request,
        RecruitingService recruitingService,
        CancellationToken cancellationToken)
        => CrmHrEditingResult(await recruitingService.SaveRecruitmentApplicationAsync(
            request.ToEditorModel(),
            cancellationToken));

    /// <summary>
    /// Schedule or update an interview of a recruitment application.
    /// </summary>
    /// <param name="request">The application, schedule, interview type, interviewer, outcome and feedback.</param>
    /// <response code="200">
    /// The interview was saved. The body is the interview identifier as a JSON string (a GUID); read it back in
    /// <c>interviews</c> of <c>GET /api/crm-hr/recruiting/applications/{applicationId}</c>.
    /// </response>
    /// <response code="400">
    /// The interview was not saved: <c>crmhr.recruiting.interview.application-required</c>,
    /// <c>crmhr.recruiting.interview.schedule-required</c> or <c>crmhr.recruiting.interview.interviewer-invalid</c>. A
    /// body the framework cannot bind is rejected with HTTP 400 without the <c>errors</c> envelope.
    /// </response>
    /// <response code="404">
    /// The recruitment application does not exist (<c>crmhr.recruiting.interview.application-not-found</c>).
    /// </response>
    internal static async Task<IResult> SaveRecruitmentInterviewAsync(
        RecruitmentInterviewSaveApiRequest request,
        RecruitingService recruitingService,
        CancellationToken cancellationToken)
        => CrmHrEditingResult(
            await recruitingService.SaveRecruitmentInterviewAsync(
                request.ToEditorModel(),
                cancellationToken),
            InterviewApplicationNotFoundCode);

    /// <summary>
    /// Create or update an onboarding, offboarding or training task of a CRM/HR party.
    /// </summary>
    /// <param name="request">The party, kind, title, owner, due date, status and optional project of the task.</param>
    /// <response code="200">
    /// The task was saved. The body is the lifecycle task identifier as a JSON string (a GUID); read it back in
    /// <c>lifecycleTasks</c> of the recruitment workspace.
    /// </response>
    /// <response code="400">
    /// The task was not saved: <c>crmhr.recruiting.task.party-required</c>,
    /// <c>crmhr.recruiting.task.title-required</c>, <c>crmhr.recruiting.task.owner-required</c>,
    /// <c>crmhr.recruiting.task.due-date-required</c> or <c>crmhr.recruiting.task.owner-invalid</c>. A body the
    /// framework cannot bind is rejected with HTTP 400 without the <c>errors</c> envelope.
    /// </response>
    /// <response code="404">
    /// The party (<c>crmhr.recruiting.task.party-not-found</c>) or the related project
    /// (<c>crmhr.recruiting.task.project-not-found</c>) does not exist.
    /// </response>
    internal static async Task<IResult> SaveLifecycleTaskAsync(
        LifecycleTaskSaveApiRequest request,
        RecruitingService recruitingService,
        CancellationToken cancellationToken)
        => CrmHrEditingResult(
            await recruitingService.SaveLifecycleTaskAsync(
                request.ToEditorModel(),
                cancellationToken),
            LifecyclePartyNotFoundCode,
            LifecycleProjectNotFoundCode);

    /// <summary>
    /// Replace the manager, buddy and mentor of a CRM/HR party.
    /// </summary>
    /// <param name="request">The party and its complete set of support assignments.</param>
    /// <response code="200">
    /// The assignments were replaced. Read them back in <c>supportAssignments</c> of the recruitment workspace or in
    /// <c>GET /api/crm-hr/parties/{partyId}/relationships</c>.
    /// </response>
    /// <response code="400">
    /// Nothing was changed: <c>crmhr.recruiting.support.party-required</c>,
    /// <c>crmhr.recruiting.support.self-reference</c>, <c>crmhr.recruiting.support.manager-invalid</c>,
    /// <c>crmhr.recruiting.support.buddy-invalid</c> or <c>crmhr.recruiting.support.mentor-invalid</c>. A body the
    /// framework cannot bind is rejected with HTTP 400 without the <c>errors</c> envelope.
    /// </response>
    /// <response code="404">The party does not exist (<c>crmhr.recruiting.support.party-not-found</c>).</response>
    internal static async Task<IResult> SaveSupportAssignmentsAsync(
        RecruitmentSupportAssignmentsSaveApiRequest request,
        RecruitingService recruitingService,
        CancellationToken cancellationToken) {
        var result = await recruitingService.SaveSupportAssignmentsAsync(request.ToEditorModel(), cancellationToken);
        if (result.IsFailure && result.Errors.Any(error => error.Code == CrmHrEditingErrorCodes.RelationshipConflict)) {
            var conflict = result.Errors.First(error => error.Code == CrmHrEditingErrorCodes.RelationshipConflict);
            return ApiEndpointResults.Conflict(conflict.Message, conflict.Code);
        }
        return ApiEndpointResults.FromResult(result, SupportPartyNotFoundCode);
    }

    /// <summary>
    /// Convert the candidate of an approved recruitment application into workforce.
    /// </summary>
    /// <param name="request">The application to convert and the workforce profile values.</param>
    /// <response code="200">
    /// The candidate was converted. The body is the candidate's party identifier as a JSON string (a GUID), not the
    /// workforce profile identifier; read <c>GET /api/crm-hr/workforce/{partyId}</c> for the profile.
    /// </response>
    /// <response code="400">
    /// The candidate was not converted: <c>crmhr.recruiting.convert.application-required</c>,
    /// <c>crmhr.recruiting.convert.stage-ineligible</c> (Rejected or Withdrawn),
    /// <c>crmhr.recruiting.convert.decision-not-approved</c>, <c>crmhr.recruiting.convert.assessment-not-ready</c> (AI
    /// agent candidate without a ready assessment) or a workforce profile rule such as
    /// <c>crmhr.workforce.manager-invalid</c> or <c>crmhr.workforce.person-delivery-unit</c>. A body the framework
    /// cannot bind is rejected with HTTP 400 without the <c>errors</c> envelope.
    /// </response>
    /// <response code="404">
    /// The application (<c>crmhr.recruiting.convert.application-not-found</c>) or its candidate party
    /// (<c>crmhr.recruiting.convert.party-not-found</c>) does not exist.
    /// </response>
    internal static async Task<IResult> ConvertCandidateAsync(
        RecruitmentConversionApiRequest request,
        RecruitingService recruitingService,
        CancellationToken cancellationToken)
        => CrmHrEditingResult(
            await recruitingService.ConvertCandidateAsync(
                request.ToEditorModel(),
                cancellationToken),
            ConversionApplicationNotFoundCode,
            ConversionPartyNotFoundCode);

    private static IResult CrmHrEditingResult<T>(CanDoItAll.SharedKernel.Result<T> result, params string[] notFoundCodes) {
        if (result.IsFailure && result.Errors.FirstOrDefault(error => error.Code is CrmHrEditingErrorCodes.WorkforceConflict or
            CrmHrEditingErrorCodes.SkillConflict or CrmHrEditingErrorCodes.CapacityConflict or
            RecruitmentEditingErrors.Conflict or RecruitmentEditingErrors.ConversionConflict or AiStewardEditingErrors.Conflict) is { } conflict) {
            return ApiEndpointResults.Conflict(conflict.Message, conflict.Code);
        }
        return ApiEndpointResults.FromResult(result, notFoundCodes);
    }

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
