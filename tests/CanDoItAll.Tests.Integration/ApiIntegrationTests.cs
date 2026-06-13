using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using CanDoItAll.Web;
using CanDoItAll.Web.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Tests.Integration;

public sealed class ApiIntegrationTests
{
    [Fact]
    public async Task Api_allows_project_access_without_bearer_token_when_jwt_is_disabled()
    {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);

        var projectResponse = await host.Client.GetAsync("/api/projects");
        var openApiResponse = await host.Client.GetAsync("/openapi/v1.json");
        var agentExecutionRunsResponse = await host.Client.GetAsync("/api/agents/execution-runs");

        Assert.Equal(HttpStatusCode.OK, projectResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, openApiResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, agentExecutionRunsResponse.StatusCode);
    }

    [Fact]
    public async Task Api_requires_bearer_token_when_jwt_is_enabled()
    {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: true);

        var unauthorizedResponse = await host.Client.GetAsync("/api/projects");
        var statusResponse = await host.Client.GetAsync("/api/access/status");

        Assert.Equal(HttpStatusCode.Unauthorized, unauthorizedResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);

        var tokenService = host.App.Services.GetRequiredService<IApiTokenService>();
        var token = tokenService.IssueToken(new ApiTokenIssueRequest
        {
            Subject = "integration-test",
            DisplayName = "Integration test",
            LifetimeMinutes = 30,
            Scopes = ["api"]
        });

        using var authorizedClient = host.CreateClient();
        authorizedClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

        var authorizedResponse = await authorizedClient.GetAsync("/api/projects");
        var authorizedOpenApiResponse = await authorizedClient.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, authorizedResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, authorizedOpenApiResponse.StatusCode);
    }

    [Fact]
    public async Task Api_filters_process_run_artifacts_by_artifact_id()
    {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);

        Guid seededRunId;
        Guid seededStepRunId;
        ProcessArtifactViewModel expectedArtifact;
        await using (var scope = host.App.Services.CreateAsyncScope())
        {
            var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
            var definitionResult = await processesService.SaveAsync(BuildFilterTestDefinition());
            Assert.True(definitionResult.IsSuccess, string.Join(" | ", definitionResult.Errors.Select(error => error.Message)));

            var publishResult = await processesService.PublishAsync(definitionResult.Value);
            Assert.True(publishResult.IsSuccess, string.Join(" | ", publishResult.Errors.Select(error => error.Message)));

            var runResult = await processesService.StartRunAsync(new ProcessRunStartRequest
            {
                ProcessDefinitionId = definitionResult.Value,
                RunName = "API filtering run",
                OperatingMode = ProcessOperatingMode.AssistedExecution,
                TriggerReason = "Integration test"
            });
            Assert.True(runResult.IsSuccess, string.Join(" | ", runResult.Errors.Select(error => error.Message)));
            seededRunId = runResult.Value;

            var stepRun = Assert.Single(await processesService.ListStepRunsAsync(seededRunId));
            seededStepRunId = stepRun.Id;
            var artifactResult = await processesService.RecordArtifactAsync(new ProcessArtifactRecordRequest
            {
                ProcessRunId = seededRunId,
                StepRunId = stepRun.Id,
                ArtifactKind = ProcessArtifactKind.Evidence,
                Title = "Focused API artifact",
                TrustStatus = ProcessArtifactTrustStatus.ReviewRequired,
                SensitivityLevel = ProcessSensitivityLevel.Internal,
                ProvenanceSummary = "Created by ApiIntegrationTests.",
                AllowedFutureUsageSummary = "Regression validation."
            });
            Assert.True(artifactResult.IsSuccess, string.Join(" | ", artifactResult.Errors.Select(error => error.Message)));

            expectedArtifact = Assert.Single(await processesService.ListArtifactsAsync(seededRunId));
        }

        var response = await host.Client.GetAsync(
            $"/api/processes/runs/{seededRunId:D}?artifactId={expectedArtifact.Id:D}&includeWorkBriefs=false&includeExecutionRuns=false&includeDirectMessages=false");

        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, responseBody);
        using var payload = JsonDocument.Parse(responseBody);
        var artifacts = payload.RootElement.GetProperty("artifacts").EnumerateArray().ToList();
        var workBriefs = payload.RootElement.GetProperty("workBriefs").EnumerateArray().ToList();

        Assert.Single(artifacts);
        Assert.Equal(expectedArtifact.Id, artifacts[0].GetProperty("id").GetGuid());
        Assert.Empty(workBriefs);

        var stepArtifactsResponse = await host.Client.GetAsync(
            $"/api/processes/runs/{seededRunId:D}/steps/{seededStepRunId:D}/artifacts?artifactId={expectedArtifact.Id:D}");
        var stepArtifactsBody = await stepArtifactsResponse.Content.ReadAsStringAsync();
        Assert.True(stepArtifactsResponse.IsSuccessStatusCode, stepArtifactsBody);
        using var stepArtifactsPayload = JsonDocument.Parse(stepArtifactsBody);
        var stepArtifacts = stepArtifactsPayload.RootElement.EnumerateArray().ToList();
        Assert.Single(stepArtifacts);
        Assert.Equal(expectedArtifact.Id, stepArtifacts[0].GetProperty("id").GetGuid());

        var artifactResponse = await host.Client.GetAsync(
            $"/api/processes/runs/{seededRunId:D}/artifacts/{expectedArtifact.Id:D}");
        var artifactBody = await artifactResponse.Content.ReadAsStringAsync();
        Assert.True(artifactResponse.IsSuccessStatusCode, artifactBody);
        using var artifactPayload = JsonDocument.Parse(artifactBody);
        Assert.Equal(expectedArtifact.Id, artifactPayload.RootElement.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Api_nested_process_runtime_routes_preserve_typed_contract_state()
    {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);

        Guid seededRunId;
        Guid seededStepRunId;
        var workflowRunId = Guid.NewGuid();
        var workflowArtifactId = Guid.NewGuid();
        await using (var scope = host.App.Services.CreateAsyncScope())
        {
            var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
            var definition = BuildFilterTestDefinition();
            var definitionStep = Assert.Single(definition.Steps);
            definitionStep.AllowedOperations =
            [
                ProcessStepOperation.WriteManagedProcessArtifacts,
                ProcessStepOperation.CaptureRuntimeProof
            ];
            definitionStep.OperationTargetScope = ProcessStepTargetScope.ManagedProcessArtifactsOnly;

            var definitionResult = await processesService.SaveAsync(definition);
            Assert.True(definitionResult.IsSuccess, string.Join(" | ", definitionResult.Errors.Select(error => error.Message)));

            var publishResult = await processesService.PublishAsync(definitionResult.Value);
            Assert.True(publishResult.IsSuccess, string.Join(" | ", publishResult.Errors.Select(error => error.Message)));

            var runResult = await processesService.StartRunAsync(new ProcessRunStartRequest
            {
                ProcessDefinitionId = definitionResult.Value,
                RunName = "API typed contract run",
                OperatingMode = ProcessOperatingMode.AssistedExecution,
                TriggerReason = "Integration test"
            });
            Assert.True(runResult.IsSuccess, string.Join(" | ", runResult.Errors.Select(error => error.Message)));

            seededRunId = runResult.Value;
            seededStepRunId = Assert.Single(await processesService.ListStepRunsAsync(seededRunId)).Id;
        }

        var transitionResponse = await host.Client.PostAsJsonAsync(
            $"/api/processes/runs/{seededRunId:D}/steps/{seededStepRunId:D}/transition",
            new
            {
                TargetStatus = ProcessStepRunStatus.Blocked,
                Reason = "The API test supplies typed blocked ownership.",
                BlockCause = ProcessStepBlockCause.OwnOutput,
                DecidedBy = "api-integration-tests",
                SuppressAutomationDispatch = true
            });
        var transitionBody = await transitionResponse.Content.ReadAsStringAsync();
        Assert.True(transitionResponse.IsSuccessStatusCode, transitionBody);

        var artifactResponse = await host.Client.PostAsJsonAsync(
            $"/api/processes/runs/{seededRunId:D}/steps/{seededStepRunId:D}/artifacts",
            new
            {
                ArtifactKind = ProcessArtifactKind.Deliverable,
                Title = "Projected workflow deliverable",
                TrustStatus = ProcessArtifactTrustStatus.ReviewRequired,
                SensitivityLevel = ProcessSensitivityLevel.Internal,
                ProvenanceSummary = "Created by ApiIntegrationTests.",
                AllowedFutureUsageSummary = "Regression validation.",
                ReviewSummary = "Projection lineage should remain attached.",
                ManagedStoragePath = "artifacts/api/projected-workflow-deliverable.md",
                ExternalReferenceKey = $"api-projection:{Guid.NewGuid():N}",
                ProjectionLineage = new ProcessArtifactProjectionLineage
                {
                    SourceKind = ProcessArtifactProjectionSourceKind.WorkflowArtifact,
                    WorkflowRunId = workflowRunId,
                    WorkflowArtifactId = workflowArtifactId,
                    ContentHash = "sha256:api-projection-lineage"
                }
            });
        var artifactBody = await artifactResponse.Content.ReadAsStringAsync();
        Assert.True(artifactResponse.IsSuccessStatusCode, artifactBody);

        await using (var scope = host.App.Services.CreateAsyncScope())
        {
            var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
            var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var persistedStep = await dbContext.Set<ProcessStepRun>().SingleAsync(item => item.Id == seededStepRunId);
            var persistedArtifact = await dbContext.Set<ProcessArtifactRecord>().SingleAsync(item => item.ProcessRunId == seededRunId);
            var listedStep = Assert.Single(await processesService.ListStepRunsAsync(seededRunId));
            var listedArtifact = Assert.Single(await processesService.ListArtifactsAsync(seededRunId));

            Assert.Equal(ProcessStepBlockReasonCode.ArtifactContractUnsatisfied, persistedStep.BlockReasonCode);
            Assert.Equal(ProcessStepBlockReasonCode.ArtifactContractUnsatisfied, listedStep.BlockReasonCode);
            Assert.Equal(ProcessStepRecoveryOption.RecoverArtifactsOnly, listedStep.NextRecoveryAction);
            Assert.Contains(ProcessStepRecoveryOption.RecoverArtifactsOnly, listedStep.RecoveryOptions);
            Assert.Equal(
                [
                    ProcessStepOperation.ReadProcessContext,
                    ProcessStepOperation.WriteManagedProcessArtifacts,
                    ProcessStepOperation.CaptureRuntimeProof
                ],
                listedStep.AllowedOperations);
            Assert.Equal(ProcessStepTargetScope.ManagedProcessArtifactsOnly, listedStep.OperationTargetScope);
            Assert.StartsWith("sha256:", persistedArtifact.ProjectionIdentityHash, StringComparison.Ordinal);
            Assert.Equal(persistedArtifact.ProjectionIdentityHash, listedArtifact.ProjectionIdentityHash);
            Assert.Equal(persistedArtifact.ProjectionLineageJson, listedArtifact.ProjectionLineageJson);
        }

        var runResponse = await host.Client.GetAsync(
            $"/api/processes/runs/{seededRunId:D}?includeWorkBriefs=false&includeExecutionRuns=false&includeDirectMessages=false");
        var runBody = await runResponse.Content.ReadAsStringAsync();
        Assert.True(runResponse.IsSuccessStatusCode, runBody);
        using var runPayload = JsonDocument.Parse(runBody);
        var stepPayload = Assert.Single(runPayload.RootElement.GetProperty("stepRuns").EnumerateArray().ToList());
        var artifactPayload = Assert.Single(runPayload.RootElement.GetProperty("artifacts").EnumerateArray().ToList());

        Assert.Equal((int)ProcessStepBlockReasonCode.ArtifactContractUnsatisfied, stepPayload.GetProperty("blockReasonCode").GetInt32());
        Assert.Equal((int)ProcessStepRecoveryOption.RecoverArtifactsOnly, stepPayload.GetProperty("nextRecoveryAction").GetInt32());
        Assert.Contains(
            (int)ProcessStepRecoveryOption.RecoverArtifactsOnly,
            stepPayload.GetProperty("recoveryOptions").EnumerateArray().Select(item => item.GetInt32()));
        Assert.DoesNotContain(
            (int)ProcessStepRecoveryOption.WaitForArtifactMaterialization,
            stepPayload.GetProperty("recoveryOptions").EnumerateArray().Select(item => item.GetInt32()));
        Assert.Contains(
            (int)ProcessStepOperation.WriteManagedProcessArtifacts,
            stepPayload.GetProperty("allowedOperations").EnumerateArray().Select(item => item.GetInt32()));
        Assert.Equal((int)ProcessStepTargetScope.ManagedProcessArtifactsOnly, stepPayload.GetProperty("operationTargetScope").GetInt32());
        var projectionIdentityHash = artifactPayload.GetProperty("projectionIdentityHash").GetString() ?? string.Empty;
        var projectionLineageJson = artifactPayload.GetProperty("projectionLineageJson").GetString() ?? string.Empty;
        Assert.StartsWith("sha256:", projectionIdentityHash, StringComparison.Ordinal);
        Assert.Contains(
            "api-projection-lineage",
            projectionLineageJson,
            StringComparison.Ordinal);
        var healthPayload = runPayload.RootElement.GetProperty("health");
        Assert.Equal((int)ProcessStepRecoveryOption.RecoverArtifactsOnly, healthPayload.GetProperty("recommendedAction").GetInt32());
    }

    [Fact]
    public async Task Api_process_run_detail_exposes_upstream_missing_artifact_recovery_health()
    {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);

        Guid seededRunId;
        Guid seededStepRunId;
        await using (var scope = host.App.Services.CreateAsyncScope())
        {
            var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
            var definitionResult = await processesService.SaveAsync(BuildFilterTestDefinition());
            Assert.True(definitionResult.IsSuccess, string.Join(" | ", definitionResult.Errors.Select(error => error.Message)));

            var publishResult = await processesService.PublishAsync(definitionResult.Value);
            Assert.True(publishResult.IsSuccess, string.Join(" | ", publishResult.Errors.Select(error => error.Message)));

            var runResult = await processesService.StartRunAsync(new ProcessRunStartRequest
            {
                ProcessDefinitionId = definitionResult.Value,
                RunName = "API SB12 upstream recovery health",
                OperatingMode = ProcessOperatingMode.AssistedExecution,
                TriggerReason = "Integration test"
            });
            Assert.True(runResult.IsSuccess, string.Join(" | ", runResult.Errors.Select(error => error.Message)));

            seededRunId = runResult.Value;
            seededStepRunId = Assert.Single(await processesService.ListStepRunsAsync(seededRunId)).Id;
        }

        var transitionResponse = await host.Client.PostAsJsonAsync(
            $"/api/processes/runs/{seededRunId:D}/steps/{seededStepRunId:D}/transition",
            new
            {
                TargetStatus = ProcessStepRunStatus.Blocked,
                Reason = "Required upstream artifacts are missing and the source step must provide required artifact input.",
                BlockCause = ProcessStepBlockCause.UpstreamInput,
                DecidedBy = "api-integration-tests",
                SuppressAutomationDispatch = true
            });
        var transitionBody = await transitionResponse.Content.ReadAsStringAsync();
        Assert.True(transitionResponse.IsSuccessStatusCode, transitionBody);

        var runResponse = await host.Client.GetAsync(
            $"/api/processes/runs/{seededRunId:D}?includeWorkBriefs=false&includeExecutionRuns=false&includeDirectMessages=false");
        var runBody = await runResponse.Content.ReadAsStringAsync();
        Assert.True(runResponse.IsSuccessStatusCode, runBody);
        using var runPayload = JsonDocument.Parse(runBody);
        var stepPayload = Assert.Single(runPayload.RootElement.GetProperty("stepRuns").EnumerateArray().ToList());
        var recoveryOptions = stepPayload
            .GetProperty("recoveryOptions")
            .EnumerateArray()
            .Select(item => item.GetInt32())
            .ToList();

        Assert.Equal((int)ProcessStepBlockReasonCode.MissingUpstreamArtifact, stepPayload.GetProperty("blockReasonCode").GetInt32());
        Assert.Equal((int)ProcessStepRecoveryOption.WaitForArtifactMaterialization, stepPayload.GetProperty("nextRecoveryAction").GetInt32());
        Assert.Contains((int)ProcessStepRecoveryOption.WaitForArtifactMaterialization, recoveryOptions);
        Assert.Contains((int)ProcessStepRecoveryOption.RecoverArtifactsOnly, recoveryOptions);

        var healthPayload = runPayload.RootElement.GetProperty("health");
        Assert.Equal((int)ProcessStepRecoveryOption.WaitForArtifactMaterialization, healthPayload.GetProperty("recommendedAction").GetInt32());
        Assert.True(healthPayload.GetProperty("missingArtifactCount").GetInt32() > 0);
    }

    [Fact]
    public async Task Api_definition_routes_round_trip_typed_contract_and_artifact_mapping_fields()
    {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);

        var subprocessChildArtifactExpectationId = Guid.NewGuid();
        var definition = BuildFilterTestDefinition();
        definition.Name = "API contract parity definition";
        definition.ContractMode = ProcessDefinitionContractMode.Strict;
        var step = Assert.Single(definition.Steps);
        step.AllowedOperations =
        [
            ProcessStepOperation.RunValidation,
            ProcessStepOperation.WriteManagedProcessArtifacts
        ];
        step.OperationTargetScope = ProcessStepTargetScope.ManagedProcessArtifactsOnly;
        var expectation = Assert.Single(step.ArtifactExpectations);
        expectation.WorkflowOutputId = "qa-report-json";
        expectation.WorkflowOutputName = "QA report";
        expectation.WorkflowOutputKind = WorkflowArtifactKind.Json;
        expectation.SubprocessChildArtifactExpectationId = subprocessChildArtifactExpectationId;

        var saveResponse = await host.Client.PostAsJsonAsync("/api/processes/definitions", definition);
        var saveBody = await saveResponse.Content.ReadAsStringAsync();
        Assert.True(saveResponse.IsSuccessStatusCode, saveBody);
        var definitionId = JsonSerializer.Deserialize<Guid>(saveBody);
        Assert.NotEqual(Guid.Empty, definitionId);

        var editorResponse = await host.Client.GetAsync($"/api/processes/definitions/{definitionId:D}");
        var editorBody = await editorResponse.Content.ReadAsStringAsync();
        Assert.True(editorResponse.IsSuccessStatusCode, editorBody);
        var editor = JsonSerializer.Deserialize<ProcessDefinitionEditorModel>(editorBody, JsonSerializerOptions.Web)
            ?? throw new InvalidOperationException("The process definition editor response was empty.");
        Assert.Equal(ProcessDefinitionContractMode.Strict, editor.ContractMode);
        var savedStep = Assert.Single(editor.Steps);
        Assert.Equal(
            [
                ProcessStepOperation.ReadProcessContext,
                ProcessStepOperation.WriteManagedProcessArtifacts,
                ProcessStepOperation.RunValidation
            ],
            savedStep.AllowedOperations);
        Assert.Equal(ProcessStepTargetScope.ManagedProcessArtifactsOnly, savedStep.OperationTargetScope);
        var savedExpectation = Assert.Single(savedStep.ArtifactExpectations);
        Assert.Equal("qa-report-json", savedExpectation.WorkflowOutputId);
        Assert.Equal("QA report", savedExpectation.WorkflowOutputName);
        Assert.Equal(WorkflowArtifactKind.Json, savedExpectation.WorkflowOutputKind);
        Assert.Equal(subprocessChildArtifactExpectationId, savedExpectation.SubprocessChildArtifactExpectationId);

        var exportResponse = await host.Client.GetAsync($"/api/processes/definitions/{definitionId:D}/export");
        var exportBody = await exportResponse.Content.ReadAsStringAsync();
        Assert.True(exportResponse.IsSuccessStatusCode, exportBody);
        var envelope = JsonSerializer.Deserialize<ProcessImportExportEnvelope>(exportBody, JsonSerializerOptions.Web)
            ?? throw new InvalidOperationException("The process definition export response was empty.");
        Assert.Equal(ProcessDefinitionContractMode.Strict, envelope.Definition.ContractMode);
        var exportedStep = Assert.Single(envelope.Definition.Steps);
        Assert.Equal(savedStep.AllowedOperations, exportedStep.AllowedOperations);
        Assert.Equal(ProcessStepTargetScope.ManagedProcessArtifactsOnly, exportedStep.OperationTargetScope);
        var exportedExpectation = Assert.Single(exportedStep.ArtifactExpectations);
        Assert.Equal("qa-report-json", exportedExpectation.WorkflowOutputId);
        Assert.Equal("QA report", exportedExpectation.WorkflowOutputName);
        Assert.Equal(WorkflowArtifactKind.Json, exportedExpectation.WorkflowOutputKind);
        Assert.Equal(subprocessChildArtifactExpectationId, exportedExpectation.SubprocessChildArtifactExpectationId);

        envelope.Definition.Id = null;
        envelope.Definition.WorkingVersionId = null;
        envelope.Definition.DefinitionConcurrencyToken = null;
        envelope.Definition.WorkingVersionConcurrencyToken = null;
        envelope.Definition.Name = "Imported API contract parity definition";
        var importResponse = await host.Client.PostAsJsonAsync("/api/processes/definitions/import", envelope);
        var importBody = await importResponse.Content.ReadAsStringAsync();
        Assert.True(importResponse.IsSuccessStatusCode, importBody);
        var importedDefinitionId = JsonSerializer.Deserialize<Guid>(importBody);
        Assert.NotEqual(Guid.Empty, importedDefinitionId);

        var importedEditorResponse = await host.Client.GetAsync($"/api/processes/definitions/{importedDefinitionId:D}");
        var importedEditorBody = await importedEditorResponse.Content.ReadAsStringAsync();
        Assert.True(importedEditorResponse.IsSuccessStatusCode, importedEditorBody);
        var importedEditor = JsonSerializer.Deserialize<ProcessDefinitionEditorModel>(importedEditorBody, JsonSerializerOptions.Web)
            ?? throw new InvalidOperationException("The imported process definition editor response was empty.");
        Assert.Equal(ProcessDefinitionContractMode.Strict, importedEditor.ContractMode);
        var importedStep = Assert.Single(importedEditor.Steps);
        Assert.Equal(savedStep.AllowedOperations, importedStep.AllowedOperations);
        Assert.Equal(ProcessStepTargetScope.ManagedProcessArtifactsOnly, importedStep.OperationTargetScope);
        var importedExpectation = Assert.Single(importedStep.ArtifactExpectations);
        Assert.Equal("qa-report-json", importedExpectation.WorkflowOutputId);
        Assert.Equal("QA report", importedExpectation.WorkflowOutputName);
        Assert.Equal(WorkflowArtifactKind.Json, importedExpectation.WorkflowOutputKind);
        Assert.Equal(subprocessChildArtifactExpectationId, importedExpectation.SubprocessChildArtifactExpectationId);
    }

    [Fact]
    public async Task Api_manages_agent_team_details_and_membership_routes()
    {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);

        Guid builderId;
        Guid reviewerId;
        await using (var scope = host.App.Services.CreateAsyncScope())
        {
            var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
            var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
            builderId = await CreateApiAgentAsync(workspaceService, "API Team Builder");
            reviewerId = await CreateApiAgentAsync(workspaceService, "API Team Reviewer");
        }

        var createResponse = await host.Client.PostAsJsonAsync(
            "/api/agents/teams",
            new AgentTeamEditorModel
            {
                Name = "API Delivery Team",
                Description = "Created through the HTTP API.",
                AgentIds = [builderId]
            });
        var createBody = await createResponse.Content.ReadAsStringAsync();
        Assert.True(createResponse.IsSuccessStatusCode, createBody);
        var teamId = JsonSerializer.Deserialize<Guid>(createBody);
        Assert.NotEqual(Guid.Empty, teamId);

        var getResponse = await host.Client.GetAsync($"/api/agents/teams/{teamId:D}");
        var getBody = await getResponse.Content.ReadAsStringAsync();
        Assert.True(getResponse.IsSuccessStatusCode, getBody);
        using (var teamPayload = JsonDocument.Parse(getBody))
        {
            Assert.Equal("API Delivery Team", teamPayload.RootElement.GetProperty("name").GetString());
            Assert.Equal(builderId, Assert.Single(teamPayload.RootElement.GetProperty("agentIds").EnumerateArray()).GetGuid());
        }

        var updateResponse = await host.Client.PutAsJsonAsync(
            $"/api/agents/teams/{teamId:D}",
            new AgentTeamEditorModel
            {
                Name = "API Delivery Team Updated",
                Description = "Updated through the explicit route.",
                AgentIds = [builderId, reviewerId]
            });
        var updateBody = await updateResponse.Content.ReadAsStringAsync();
        Assert.True(updateResponse.IsSuccessStatusCode, updateBody);
        Assert.Equal(teamId, JsonSerializer.Deserialize<Guid>(updateBody));

        var membersResponse = await host.Client.PutAsJsonAsync(
            $"/api/agents/teams/{teamId:D}/members",
            new { AgentIds = new[] { reviewerId } });
        var membersBody = await membersResponse.Content.ReadAsStringAsync();
        Assert.True(membersResponse.IsSuccessStatusCode, membersBody);

        var teamAgentsResponse = await host.Client.GetAsync($"/api/agents/teams/{teamId:D}/agents?includeTemplates=false");
        var teamAgentsBody = await teamAgentsResponse.Content.ReadAsStringAsync();
        Assert.True(teamAgentsResponse.IsSuccessStatusCode, teamAgentsBody);
        using (var teamAgentsPayload = JsonDocument.Parse(teamAgentsBody))
        {
            var names = teamAgentsPayload.RootElement
                .EnumerateArray()
                .Select(item => item.GetProperty("name").GetString())
                .ToList();
            Assert.DoesNotContain("API Team Builder", names);
            Assert.Contains("API Team Reviewer", names);
        }

        var missingTeamResponse = await host.Client.GetAsync($"/api/agents/teams/{Guid.NewGuid():D}");
        Assert.Equal(HttpStatusCode.NotFound, missingTeamResponse.StatusCode);
    }

    [Fact]
    public async Task Api_openapi_exposes_focused_control_plane_routes()
    {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);

        using var payload = JsonDocument.Parse(await host.Client.GetStringAsync("/openapi/v1.json"));
        var paths = payload.RootElement.GetProperty("paths");

        Assert.True(paths.TryGetProperty("/api/project-structure/projects/{projectId}/nodes/{nodeId}/type", out _));
        Assert.True(paths.TryGetProperty("/api/project-structure/projects/{projectId}/nodes/{nodeId}/process/start", out _));
        Assert.True(paths.TryGetProperty("/api/project-structure/projects/{projectId}/nodes/{nodeId}/workflow-add-options", out _));
        Assert.True(paths.TryGetProperty("/api/project-structure/projects/{projectId}/nodes/{nodeId}/workflow-definition", out _));
        Assert.True(paths.TryGetProperty("/api/project-structure/projects/{projectId}/nodes/{nodeId}/workflow/start", out _));
        Assert.True(paths.TryGetProperty("/api/project-structure/projects/{projectId}/nodes/{nodeId}/workflow/status", out _));
        Assert.True(paths.TryGetProperty("/api/project-structure/projects/{projectId}/assets/{nodeId}/content", out _));
        Assert.True(paths.TryGetProperty("/api/processes/templates/{processKey}/detail", out _));
        Assert.True(paths.TryGetProperty("/api/processes/templates/{processKey}/envelope", out _));
        Assert.True(paths.TryGetProperty("/api/processes/templates/{processKey}/mermaid", out _));
        Assert.True(paths.TryGetProperty("/api/processes/templates/{processKey}/import", out _));
        Assert.True(paths.TryGetProperty("/api/processes/templates/baseline-scenarios", out _));
        Assert.True(paths.TryGetProperty("/api/processes/templates/live-run-profiles", out _));
        Assert.True(paths.TryGetProperty("/api/processes/runs/{runId}/steps/{stepRunId}/artifacts", out _));
        Assert.True(paths.TryGetProperty("/api/processes/runs/{runId}/manager-directives", out _));
        Assert.True(paths.TryGetProperty("/api/agents/teams/{teamId}", out _));
        Assert.True(paths.TryGetProperty("/api/agents/teams/{teamId}/agents", out _));
        Assert.True(paths.TryGetProperty("/api/agents/teams/{teamId}/members", out _));
        Assert.True(paths.TryGetProperty("/api/agents/{agentId}/execution-runs/{executionRunId}/artifacts", out _));
        Assert.True(paths.TryGetProperty("/api/agents/{agentId}/execution-runs/{executionRunId}/log", out _));
        Assert.True(paths.TryGetProperty("/api/cognitive-memory/contract", out _));
        Assert.True(paths.TryGetProperty("/api/cognitive-memory/status", out _));
        Assert.True(paths.TryGetProperty("/api/cognitive-memory/database/selection", out _));
        Assert.True(paths.TryGetProperty("/api/cognitive-memory/database/profiles", out _));
        Assert.True(paths.TryGetProperty("/api/cognitive-memory/database/transfer/sources/{targetProfileId}", out _));
        Assert.True(paths.TryGetProperty("/api/cognitive-memory/database/transfer/preview", out _));
        Assert.True(paths.TryGetProperty("/api/cognitive-memory/database/transfer", out _));
        Assert.True(paths.TryGetProperty("/api/cognitive-memory/database/profiles/postgresql", out _));
        Assert.True(paths.TryGetProperty("/api/cognitive-memory/database/switch/{profileId}", out _));
        Assert.True(paths.TryGetProperty("/api/cognitive-memory/settings", out _));
        Assert.True(paths.TryGetProperty("/api/cognitive-memory/ingestion/project-structure", out _));
        Assert.True(paths.TryGetProperty("/api/cognitive-memory/ingestion/processes", out _));
        Assert.True(paths.TryGetProperty("/api/cognitive-memory/external-sources/files", out _));
        Assert.True(paths.TryGetProperty("/api/cognitive-memory/external-sources/web-links", out _));
        Assert.True(paths.TryGetProperty("/api/cognitive-memory/external-sources/ingestions/{operationId}", out _));
        Assert.True(paths.TryGetProperty("/api/cognitive-memory/snapshot", out _));
        Assert.True(paths.TryGetProperty("/api/cognitive-memory/sources/ingest", out _));
        Assert.True(paths.TryGetProperty("/api/cognitive-memory/consolidation/runs", out _));
        Assert.True(paths.TryGetProperty("/api/cognitive-memory/projections/rebuild", out _));
        Assert.True(paths.TryGetProperty("/api/cognitive-memory/automation/run", out _));
        Assert.True(paths.TryGetProperty("/api/cognitive-memory/retention/cleanup", out _));
        Assert.True(paths.TryGetProperty("/api/cognitive-memory/recall", out _));
        Assert.True(paths.TryGetProperty("/api/cognitive-memory/review-items/{reviewItemId}/decisions", out _));
        Assert.True(paths.TryGetProperty("/api/cognitive-memory/probes/sessions", out _));
        Assert.True(paths.TryGetProperty("/api/cognitive-memory/probes/sessions/{sessionId}/turns", out _));
        Assert.True(paths.TryGetProperty("/api/cognitive-memory/probes/turns/{turnId}/feedback", out _));
        Assert.True(paths.TryGetProperty("/api/cognitive-memory/self-regulation/assessments", out _));
        Assert.True(paths.TryGetProperty("/api/cognitive-memory/answer-gate/decisions", out _));
        Assert.True(paths.TryGetProperty("/api/cognitive-memory/professor-reviews", out _));
        Assert.True(paths.TryGetProperty("/api/cognitive-memory/professor-reviews/{reviewId}/complete", out _));
        Assert.True(paths.TryGetProperty("/api/cognitive-memory/epistemic-drive/scans", out _));
        Assert.True(paths.TryGetProperty("/api/cognitive-memory/epistemic-drive/proposals/{proposalId}/decisions", out _));
        Assert.True(paths.TryGetProperty("/api/cognitive-memory/cross-project/promotions", out _));
        Assert.True(paths.TryGetProperty("/api/cognitive-memory/distributed/workers", out _));
        Assert.True(paths.TryGetProperty("/api/cognitive-memory/distributed/jobs", out _));
        Assert.True(paths.TryGetProperty("/api/cognitive-memory/distributed/jobs/claim", out _));
        Assert.True(paths.TryGetProperty("/api/cognitive-memory/distributed/jobs/{jobId}/results", out _));

        Assert.True(paths.TryGetProperty("/api/cognitive-memory/v1/contract", out _));
        Assert.True(paths.TryGetProperty("/api/cognitive-memory/v1/status", out _));
        Assert.True(paths.TryGetProperty("/api/cognitive-memory/v1/projections/rebuild", out _));
        Assert.True(paths.TryGetProperty("/api/cognitive-memory/v1/automation/run", out _));
        Assert.True(paths.TryGetProperty("/api/cognitive-memory/v1/retention/cleanup", out _));
    }

    [Fact]
    public async Task Api_live_run_profiles_expose_fresh_run_policy_contract()
    {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);

        var profiles = await host.Client.GetFromJsonAsync<List<ProcessTemplateLiveRunProfileSummary>>("/api/processes/templates/live-run-profiles");
        Assert.NotNull(profiles);
        var profile = Assert.Single(
            profiles,
            item => string.Equals(item.Key, "generic-blazor-wasm-pwa-app", StringComparison.Ordinal));

        Assert.Equal("blazor-app-delivery", profile.ProcessTemplateKey);
        Assert.True(profile.FreshRunPolicy.RequiresFreshRun);
        Assert.False(profile.FreshRunPolicy.AllowsSeededTransitions);
        Assert.False(profile.FreshRunPolicy.AllowsSeededArtifacts);
        Assert.NotEmpty(profile.FreshRunPolicy.RequiredPreDispatchChecks);
        Assert.NotEmpty(profile.FreshRunPolicy.RequiredEvidenceChecks);
        Assert.Contains(
            profile.FreshRunPolicy.RequiredPreDispatchChecks,
            check => check.Contains("no baseline scenario transitions", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            profile.FreshRunPolicy.RequiredEvidenceChecks,
            check => check.Contains("current-run evidence", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            "current-run managed output",
            profile.FreshRunPolicy.ProjectStructureWritebackGuidance,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CognitiveMemoryStatus_reports_database_projection_and_host_diagnostics()
    {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);

        using var payload = JsonDocument.Parse(await host.Client.GetStringAsync("/api/cognitive-memory/status"));
        var root = payload.RootElement;

        Assert.True(root.GetProperty("isPostgreSql").ValueKind is JsonValueKind.True or JsonValueKind.False);
        Assert.True(root.TryGetProperty("database", out var database));
        Assert.False(string.IsNullOrWhiteSpace(database.GetProperty("resolutionSourceName").GetString()));
        Assert.True(database.GetProperty("isRuntimeLocked").ValueKind is JsonValueKind.True or JsonValueKind.False);

        Assert.True(root.TryGetProperty("projectionDefaults", out var projectionDefaults));
        Assert.True(projectionDefaults.GetProperty("canProjectMissingRecords").ValueKind is JsonValueKind.True or JsonValueKind.False);
        Assert.False(string.IsNullOrWhiteSpace(projectionDefaults.GetProperty("projectionStoreKindName").GetString()));

        Assert.True(root.TryGetProperty("hostDiagnostics", out var hostDiagnostics));
        Assert.False(string.IsNullOrWhiteSpace(hostDiagnostics.GetProperty("contentRootPath").GetString()));
        Assert.True(hostDiagnostics.GetProperty("blazorWebJsExists").ValueKind is JsonValueKind.True or JsonValueKind.False);
    }

    private static async Task<Guid> CreateApiAgentAsync(
        IAgentFrameworkWorkspaceService workspaceService,
        string name)
    {
        var editor = await workspaceService.GetAgentEditorAsync();
        editor.Name = name;
        editor.RoleTitle = "API team specialist";
        editor.Summary = "Participates in HTTP API team route tests.";
        editor.Instructions = "Keep the test catalog deterministic.";
        editor.Status = AgentLifecycleStatus.Active;
        editor.IsTemplate = false;
        editor.TemplateKey = string.Empty;
        return await workspaceService.SaveAgentAsync(editor);
    }

    private static ProcessDefinitionEditorModel BuildFilterTestDefinition()
    {
        var roleId = Guid.NewGuid();
        var stepId = Guid.NewGuid();
        return new ProcessDefinitionEditorModel
        {
            Name = "API filter definition",
            Summary = "Small definition for API filtering validation.",
            ValueStatement = "Expose focused process run evidence without loading unrelated slices.",
            CustomerName = "Engineering",
            OwnerName = "Integration tests",
            GovernancePolicySummary = "Generated only for local test coverage.",
            ChangeSummary = "Initial test definition.",
            ConstitutionRuleSummary = "Keep API filtering deterministic.",
            OperatingModeSummary = "Assisted local validation.",
            SimulationReadinessSummary = "Safe for integration tests.",
            Roles =
            [
                new ProcessRoleEditorModel
                {
                    Id = roleId,
                    Key = "api-operator",
                    DisplayName = "API operator",
                    Purpose = "Own the focused API filtering test.",
                    StaffingIntent = "A deterministic local role for process runtime validation.",
                    PreferredExecutorKind = "person",
                    SnapshotSummary = "API integration test role."
                }
            ],
            Steps =
            [
                new ProcessStepEditorModel
                {
                    Id = stepId,
                    Key = "evidence",
                    Title = "Capture focused artifact",
                    StepKind = ProcessStepKind.Work,
                    InputContractSummary = "None.",
                    OutputContractSummary = "One focused artifact.",
                    EvidenceContractSummary = "The API filter should return exactly this artifact.",
                    DecisionRightsSummary = "Integration test controls the step.",
                    ExceptionPolicySummary = "Fail the test on unexpected runtime errors.",
                    TargetLeadHours = 1,
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel
                        {
                            RoleRequirementId = roleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Responsible,
                            RebindPolicySummary = "Keep the local API operator on the step."
                        }
                    ],
                    ArtifactExpectations =
                    [
                        new ProcessArtifactExpectationEditorModel
                        {
                            Id = Guid.NewGuid(),
                            ArtifactKind = ProcessArtifactKind.Evidence,
                            Title = "Focused API artifact",
                            ValidationRequirementSummary = "Must be addressable by artifact id."
                        }
                    ]
                }
            ]
        };
    }
}

internal sealed class ApiTestHost : IAsyncDisposable
{
    private ApiTestHost(
        CanDoItAllTestEnvironment testEnvironment,
        TestDatabaseProfile activeProfile,
        WebApplication app,
        HttpClient client)
    {
        TestEnvironment = testEnvironment;
        ActiveProfile = activeProfile;
        RootPath = testEnvironment.RootPath;
        App = app;
        Client = client;
    }

    public string RootPath { get; }

    public CanDoItAllTestEnvironment TestEnvironment { get; }

    public TestDatabaseProfile ActiveProfile { get; }

    public WebApplication App { get; }

    public HttpClient Client { get; }

    public static async Task<ApiTestHost> CreateAsync(
        bool jwtEnabled,
        Action<IServiceCollection>? configureServices = null)
    {
        var testEnvironment = CanDoItAllTestEnvironment.Create("candoitall-api-tests");
        var activeProfile = testEnvironment.CreatePostgreSqlProfile("api-host");
        var configurationOverrides = new Dictionary<string, string?>
        {
            ["DevelopmentManager:TuningModeEnabled"] = "false",
            [LocalRuntimeHostedWorkerPolicy.LaneKindConfigurationKey] = LocalRuntimeHostedWorkerPolicy.McpToolHostLaneKind,
            ["Api:Enabled"] = "true",
            ["Api:OpenApiEnabled"] = "true",
            ["Api:Authorization:Enabled"] = jwtEnabled.ToString(),
            ["Api:Authorization:Issuer"] = "CanDoItAll.Api.Tests",
            ["Api:Authorization:Audience"] = "CanDoItAll.Api.Tests",
            ["Api:Authorization:SigningKey"] = "api-test-signing-key-32-bytes-minimum",
            ["Api:Authorization:DefaultTokenLifetimeMinutes"] = "30",
            ["Api:Authorization:MaxTokenLifetimeMinutes"] = "120"
        };

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = testEnvironment.RootPath,
            EnvironmentName = Environments.Development,
            ApplicationName = "CanDoItAll.Tests.Integration"
        });
        builder.Configuration.AddInMemoryCollection(activeProfile.CreateConfigurationValues(configurationOverrides));

        TestApplicationBootstrap.ConfigureDefaultServices(
            builder.Services,
            builder.Configuration,
            builder.Environment,
            registerTestHostApplicationLifetime: false);
        builder.Services.AddCanDoItAllApi(builder.Configuration);
        configureServices?.Invoke(builder.Services);

        var app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");

        var options = app.Services.GetRequiredService<IOptions<ApiAccessOptions>>().Value;
        if (options.Authorization.Enabled)
        {
            app.UseAuthentication();
            app.UseAuthorization();
        }

        if (options.OpenApiEnabled)
        {
            var openApiEndpoint = app.MapOpenApi();
            var swaggerEndpoint = app.MapOpenApi("/swagger/{documentName}/swagger.json");
            if (options.Authorization.Enabled)
            {
                openApiEndpoint.RequireAuthorization();
                swaggerEndpoint.RequireAuthorization();
            }
        }

        app.MapProjectStructureAgentApi();
        app.MapCanDoItAllApi();

        await TestApplicationBootstrap.InitializeSchemaAsync(app.Services, TestSchemaBootstrapModules.Full);
        await app.StartAsync();

        var client = CreateClient(app);
        return new ApiTestHost(testEnvironment, activeProfile, app, client);
    }

    public HttpClient CreateClient()
    {
        return CreateClient(App);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await App.StopAsync();
        await App.DisposeAsync();
        await TestEnvironment.DisposeAsync();
    }

    private static HttpClient CreateClient(WebApplication app)
    {
        var server = app.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses
            ?? throw new InvalidOperationException("The API test host did not expose any server addresses.");
        return new HttpClient
        {
            BaseAddress = new Uri(addresses.Single()),
            Timeout = TimeSpan.FromSeconds(30)
        };
    }
}
