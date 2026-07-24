using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Tests.Integration;

public sealed class CrmHrApiIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Api_round_trips_linked_hiring_and_workforce_scenario_with_bounded_pages()
    {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        var unique = Guid.NewGuid().ToString("N");

        var deliveryUnitId = await CreatePartyAsync(
            host.Client,
            $"Platform Delivery {unique}",
            PartyType.OrganizationUnit,
            $"unit-{unique}",
            PartyRoleKind.DeliveryUnit);
        var managerId = await CreatePartyAsync(
            host.Client,
            $"Morgan Manager {unique}",
            PartyType.Person,
            $"manager-{unique}",
            PartyRoleKind.Employee);
        var buddyId = await CreatePartyAsync(
            host.Client,
            $"Bailey Buddy {unique}",
            PartyType.Person,
            $"buddy-{unique}",
            PartyRoleKind.Employee);
        var candidateId = await CreatePartyAsync(
            host.Client,
            $"Casey Candidate {unique}",
            PartyType.Person,
            $"candidate-{unique}");
        var sensitiveId = await CreatePartyAsync(
            host.Client,
            $"Protected Person {unique}",
            PartyType.Person,
            $"protected-{unique}",
            isSensitive: true);

        await PostAsync<Guid>(
            host.Client,
            "/api/crm-hr/workforce/profiles",
            new
            {
                partyId = managerId,
                workforceKind = WorkforceKind.Employee,
                employeeCode = $"EMP-{unique[..8]}",
                jobTitle = "Engineering Manager",
                discipline = "Platform Engineering",
                seniority = "Lead",
                startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-2)),
                location = "La Paz",
                timeZone = "America/La_Paz",
                rateUnit = ProjectResourceRateUnit.Hour,
                rateCurrencyCode = "USD",
                capacityHoursPerWeek = 40m,
                status = "Active"
            });

        var relationshipStart = new DateTimeOffset(
            2026,
            7,
            24,
            9,
            30,
            0,
            TimeSpan.FromHours(-4));
        var relationshipsResponse = await host.Client.PutAsJsonAsync(
            $"/api/crm-hr/parties/{candidateId:D}/relationships",
            new
            {
                relationships = new[]
                {
                    new
                    {
                        relatedPartyId = managerId,
                        relationshipKind = PartyRelationshipKind.ManagedBy,
                        isOutgoing = true,
                        isPrimary = true,
                        startDateUtc = relationshipStart,
                        notes = "Hiring manager relationship"
                    }
                }
            });
        await AssertSuccessAsync(relationshipsResponse);

        var relationships = await GetAsync<IReadOnlyList<PartyRelationshipListItemModel>>(
            host.Client,
            $"/api/crm-hr/parties/{candidateId:D}/relationships");
        var relationship = Assert.Single(relationships);
        Assert.Equal(managerId, relationship.RelatedPartyId);
        Assert.Equal(PartyRelationshipKind.ManagedBy, relationship.RelationshipKind);
        Assert.Equal(
            relationshipStart.UtcDateTime,
            relationship.StartDateUtc?.UtcDateTime);

        var relationshipRoundTripResponse = await host.Client.PutAsJsonAsync(
            $"/api/crm-hr/parties/{candidateId:D}/relationships",
            new
            {
                relationships
            });
        await AssertSuccessAsync(relationshipRoundTripResponse);

        var skillId = await PostAsync<Guid>(
            host.Client,
            "/api/crm-hr/workforce/skills",
            new
            {
                name = $"Distributed Systems {unique}",
                category = "Engineering",
                description = "Designs resilient distributed services.",
                isActive = true
            });
        var skillCatalog = await GetAsync<IReadOnlyList<SkillCatalogItemModel>>(
            host.Client,
            "/api/crm-hr/workforce/skills");
        Assert.Contains(skillCatalog, item => item.Id == skillId);

        var applicationId = await PostAsync<Guid>(
            host.Client,
            "/api/crm-hr/recruiting/applications",
            new
            {
                partyId = candidateId,
                targetUnitPartyId = deliveryUnitId,
                recruiterPartyId = managerId,
                hiringManagerPartyId = managerId,
                desiredRole = "Senior Platform Engineer",
                source = "Employee referral",
                stage = RecruitmentStage.Interviewing,
                availableFrom = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14)),
                decision = RecruitmentDecision.Pending,
                stageNotes = "Passed screening."
            });

        var scheduledAtUtc = DateTimeOffset.UtcNow.AddDays(2);
        await PostAsync<Guid>(
            host.Client,
            "/api/crm-hr/recruiting/interviews",
            new
            {
                applicationId,
                scheduledAtUtc,
                interviewType = RecruitmentInterviewType.Technical,
                interviewerPartyId = managerId,
                outcome = RecruitmentInterviewOutcome.StrongYes,
                feedback = "Strong systems reasoning.",
                recommendation = "Proceed to offer."
            });

        var supportResponse = await host.Client.PostAsJsonAsync(
            "/api/crm-hr/recruiting/support-assignments",
            new
            {
                partyId = candidateId,
                managerPartyId = managerId,
                buddyPartyId = buddyId,
                mentorPartyId = managerId
            });
        await AssertSuccessAsync(supportResponse);

        await PostAsync<Guid>(
            host.Client,
            "/api/crm-hr/recruiting/lifecycle-tasks",
            new
            {
                partyId = candidateId,
                taskKind = LifecycleTaskKind.Onboarding,
                title = "Prepare engineering workstation",
                ownerPartyId = managerId,
                dueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
                status = LifecycleTaskStatus.InProgress,
                notes = "Coordinate security access before the start date."
            });

        var recruitmentWorkspace = await GetAsync<RecruitmentWorkspaceModel>(
            host.Client,
            $"/api/crm-hr/recruiting/applications/{applicationId:D}");
        Assert.True(recruitmentWorkspace.HasSelectedApplication);
        Assert.Equal(candidateId, recruitmentWorkspace.Application.PartyId);
        Assert.Single(recruitmentWorkspace.Interviews);
        Assert.Single(recruitmentWorkspace.LifecycleTasks);
        Assert.Equal(managerId, recruitmentWorkspace.SupportAssignments.ManagerPartyId);

        var convertedPartyId = await PostAsync<Guid>(
            host.Client,
            "/api/crm-hr/recruiting/conversions",
            new
            {
                applicationId,
                workforceKind = WorkforceKind.Employee,
                jobTitle = "Senior Platform Engineer",
                discipline = "Platform Engineering",
                seniority = "Senior",
                homeUnitPartyId = deliveryUnitId,
                managerPartyId = managerId,
                startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14)),
                location = "La Paz",
                timeZone = "America/La_Paz",
                capacityHoursPerWeek = 40m,
                status = "Active"
            });
        Assert.Equal(candidateId, convertedPartyId);

        await PostAsync<Guid>(
            host.Client,
            "/api/crm-hr/workforce/party-skills",
            new
            {
                partyId = candidateId,
                skillId,
                proficiency = SkillProficiencyLevel.Expert,
                yearsExperience = 8,
                certificationStatus = "Validated",
                lastValidatedOn = DateOnly.FromDateTime(DateTime.UtcNow),
                notes = "Validated during technical interview."
            });

        await PostAsync<Guid>(
            host.Client,
            "/api/crm-hr/workforce/capacity-blocks",
            new
            {
                partyId = candidateId,
                blockKind = CapacityBlockKind.Reserve,
                startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(21)),
                endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(23)),
                percentage = 50m,
                notes = "New-hire platform training."
            });

        var workforceWorkspace = await GetAsync<WorkforceWorkspaceModel>(
            host.Client,
            $"/api/crm-hr/workforce/{candidateId:D}");
        Assert.Equal("Senior Platform Engineer", workforceWorkspace.Profile.JobTitle);
        Assert.Equal(deliveryUnitId, workforceWorkspace.Profile.HomeUnitPartyId);
        Assert.Equal(managerId, workforceWorkspace.Profile.ManagerPartyId);
        Assert.Contains(workforceWorkspace.Skills, item => item.SkillId == skillId);
        Assert.Single(workforceWorkspace.CapacityBlocks);

        var partyPage = await GetAsync<PartyRecordPage>(
            host.Client,
            $"/api/crm-hr/parties?search={Uri.EscapeDataString(unique)}&pageIndex=0&pageSize=2");
        Assert.Equal(2, partyPage.Items.Count);
        Assert.True(partyPage.TotalCount >= 5);
        Assert.Equal(2, partyPage.PageSize);

        var workforcePage = await GetAsync<PartyRecordPage>(
            host.Client,
            $"/api/crm-hr/workforce?search={Uri.EscapeDataString(unique)}&pageIndex=0&pageSize=1");
        Assert.Single(workforcePage.Items);
        Assert.True(workforcePage.TotalCount >= 4);

        var recruitmentPage = await GetAsync<RecruitmentApplicationPage>(
            host.Client,
            $"/api/crm-hr/recruiting/applications?search={Uri.EscapeDataString(unique)}&pageIndex=0&pageSize=1");
        Assert.Single(recruitmentPage.Items);
        Assert.Equal(1, recruitmentPage.TotalCount);

        var sensitiveParty = await GetAsync<PartyRecordQueryItem>(
            host.Client,
            $"/api/crm-hr/parties/{sensitiveId:D}");
        Assert.True(sensitiveParty.IsSensitive);
        Assert.Empty(sensitiveParty.ExternalCode);
        Assert.Empty(sensitiveParty.Summary);
        Assert.Empty(sensitiveParty.Tags);
    }

    [Fact]
    public async Task Api_returns_structured_errors_for_invalid_references_and_query_validation()
    {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        var partyId = await CreatePartyAsync(
            host.Client,
            $"Validation Party {Guid.NewGuid():N}",
            PartyType.Person,
            $"validation-{Guid.NewGuid():N}");

        var invalidRelationshipResponse = await host.Client.PutAsJsonAsync(
            $"/api/crm-hr/parties/{partyId:D}/relationships",
            new
            {
                relationships = new[]
                {
                    new
                    {
                        relatedPartyId = Guid.NewGuid(),
                        relationshipKind = PartyRelationshipKind.ManagedBy,
                        isOutgoing = true
                    }
                }
            });
        Assert.Equal(HttpStatusCode.NotFound, invalidRelationshipResponse.StatusCode);
        Assert.Equal(
            "crmhr.party.relationship-party-not-found",
            await ReadSingleErrorCodeAsync(invalidRelationshipResponse));

        var invalidApplicationResponse = await host.Client.PostAsJsonAsync(
            "/api/crm-hr/recruiting/applications",
            new
            {
                partyId,
                desiredRole = ""
            });
        Assert.Equal(HttpStatusCode.BadRequest, invalidApplicationResponse.StatusCode);
        Assert.Equal(
            "crmhr.recruiting.role-required",
            await ReadSingleErrorCodeAsync(invalidApplicationResponse));

        var invalidPageResponse = await host.Client.GetAsync(
            $"/api/crm-hr/parties?search={partyId:D}&pageSize={PartyRecordQueryLimits.MaximumPageSize + 1}");
        Assert.Equal(HttpStatusCode.BadRequest, invalidPageResponse.StatusCode);
        Assert.Equal(
            "crmhr.party.query-invalid",
            await ReadSingleErrorCodeAsync(invalidPageResponse));
    }

    private static async Task<Guid> CreatePartyAsync(
        HttpClient client,
        string displayName,
        PartyType partyType,
        string externalCode,
        PartyRoleKind? role = null,
        bool isSensitive = false)
    {
        var roles = role.HasValue
            ? new object[]
            {
                new
                {
                    roleKind = role.Value,
                    title = role.Value.ToString(),
                    isPrimary = true
                }
            }
            : [];
        var email = $"{externalCode}@example.test";
        return await PostAsync<Guid>(
            client,
            "/api/crm-hr/parties",
            new
            {
                partyType,
                lifecycleStatus = PartyLifecycleStatus.Active,
                displayName,
                externalCode,
                summary = $"Integration scenario party {displayName}.",
                tags = new[] { "integration", externalCode },
                region = "La Paz",
                countryCode = "BO",
                timeZone = "America/La_Paz",
                isSensitive,
                roles,
                publicContacts = new[]
                {
                    new
                    {
                        contactType = PartyContactType.Email,
                        label = "Primary email",
                        value = email,
                        isPrimary = true,
                        tags = new[] { "work" }
                    }
                }
            });
    }

    private static async Task<T> PostAsync<T>(
        HttpClient client,
        string route,
        object request)
    {
        var response = await client.PostAsJsonAsync(route, request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.IsSuccessStatusCode,
            $"{(int)response.StatusCode} {response.StatusCode}: {body}");
        return JsonSerializer.Deserialize<T>(body, JsonOptions)
            ?? throw new InvalidOperationException(
                $"Response from '{route}' deserialized to null.");
    }

    private static async Task<T> GetAsync<T>(
        HttpClient client,
        string route)
    {
        var response = await client.GetAsync(route);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.IsSuccessStatusCode,
            $"{(int)response.StatusCode} {response.StatusCode}: {body}");
        return JsonSerializer.Deserialize<T>(body, JsonOptions)
            ?? throw new InvalidOperationException(
                $"Response from '{route}' deserialized to null.");
    }

    private static async Task AssertSuccessAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.IsSuccessStatusCode,
            $"{(int)response.StatusCode} {response.StatusCode}: {body}");
    }

    private static async Task<string> ReadSingleErrorCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return Assert.Single(document.RootElement
                .GetProperty("errors")
                .EnumerateArray())
            .GetProperty("code")
            .GetString()
            ?? throw new InvalidOperationException("API error code was null.");
    }
}
