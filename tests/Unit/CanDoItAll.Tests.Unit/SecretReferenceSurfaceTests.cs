using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit;

public sealed class SecretReferenceSurfaceTests
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [Fact]
    public void AgentSecretReferences_serialize_reference_metadata_only()
    {
        var secretId = Guid.NewGuid();
        var policy = AgentPermissionsPolicy.Default with
        {
            AllowedSecrets =
            [
                new AgentAllowedSecretReference(
                    secretId,
                    "OpenAI API",
                    AgentSecretPurposes.GeneralAgentRequest)
            ]
        };

        var json = JsonSerializer.Serialize(policy, JsonOptions);
        var roundTrip = JsonSerializer.Deserialize<AgentPermissionsPolicy>(json, JsonOptions);

        Assert.NotNull(roundTrip);
        var reference = Assert.Single(roundTrip.NormalizedAllowedSecrets);
        Assert.Equal(secretId, reference.SecretId);
        Assert.Equal("OpenAI API", reference.NameSnapshot);
        Assert.DoesNotContain("secret-token", json, StringComparison.Ordinal);
    }

    [Fact]
    public void WorkflowHttpSecretHeader_serializes_reference_metadata_only()
    {
        var secretId = Guid.NewGuid();
        var settings = new WorkflowHttpExecutorSettings
        {
            Url = "https://api.example.test/items",
            SecretHeader = new WorkflowHttpSecretHeaderBinding
            {
                SecretId = secretId,
                SecretNameSnapshot = "Example API",
                HeaderName = "Authorization",
                ValueFormat = WorkflowHttpSecretValueFormat.Bearer
            }
        };

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        var roundTrip = JsonSerializer.Deserialize<WorkflowHttpExecutorSettings>(json, JsonOptions);

        Assert.NotNull(roundTrip);
        Assert.Equal(secretId, roundTrip.SecretHeader.SecretId);
        Assert.Equal("Example API", roundTrip.SecretHeader.SecretNameSnapshot);
        Assert.Equal(WorkflowHttpSecretValueFormat.Bearer, roundTrip.SecretHeader.ValueFormat);
        Assert.DoesNotContain("secret-token", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ProjectStructureSecretReferenceMetadata_validates_for_secret_reference_nodes()
    {
        var secretId = Guid.NewGuid();
        var metadata = new ProjectObjectMetadataEnvelope
        {
            SecretReference = new ProjectSecretReferenceMetadata
            {
                SecretId = secretId,
                SecretNameSnapshot = "Outlook token",
                Purpose = "project-structure-reference",
                ExternalReference = "Used by email workflow"
            }
        };

        var json = ProjectObjectMetadataSerializer.Serialize(metadata);
        var parsed = ProjectObjectMetadataSerializer.Parse(json);

        ProjectObjectMetadataSerializer.Validate(ProjectObjectType.SecretReference, string.Empty, parsed);
        Assert.Equal(secretId, parsed.SecretReference?.SecretId);
        Assert.Equal("Outlook token", parsed.SecretReference?.SecretNameSnapshot);
        Assert.DoesNotContain("secret-token", json, StringComparison.Ordinal);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}
