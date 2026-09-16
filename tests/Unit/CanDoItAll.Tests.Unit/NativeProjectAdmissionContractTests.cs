using System.Text.Json;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit.Projects;

public sealed class NativeProjectAdmissionContractTests {
    [Fact]
    public void Runtime_admission_preserves_saved_native_request_payloads_and_is_not_deserialized_from_clients() {
        var admission = new ProjectWriteAdmission(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var original = new ProjectObjectCreateRequest(ProjectObjectType.ProjectBlock, "Saved request", "", "", $"project:{admission.ProjectId}");
        var captured = original with { ExpectedProjectAdmission = admission };
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(captured));
        var supplied = JsonSerializer.Serialize(original).TrimEnd('}') + ",\"ExpectedProjectAdmission\":" + JsonSerializer.Serialize(admission) + "}";
        Assert.Null(JsonSerializer.Deserialize<ProjectObjectCreateRequest>(supplied)!.ExpectedProjectAdmission);
        var edit = new ProjectObjectEditRequest("Edited", "", "", null, null, "{}");
        Assert.Equal(JsonSerializer.Serialize(edit), JsonSerializer.Serialize(edit with { ExpectedProjectAdmission = admission }));
        var reclassification = new ProjectObjectReclassificationRequest(ProjectObjectType.ProjectBlock, "feature", "Changed", "", "");
        Assert.Equal(JsonSerializer.Serialize(reclassification), JsonSerializer.Serialize(reclassification with { ExpectedProjectAdmission = admission }));
    }
}
