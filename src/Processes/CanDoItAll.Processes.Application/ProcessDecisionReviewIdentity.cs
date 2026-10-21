using System.Security.Cryptography;
using System.Text.Json;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

internal static class ProcessDecisionReviewIdentity {
    internal static RuntimeCommandId Derive(ProcessDecisionReviewRequest request, ProcessAcceptedExecutionInput input,
        ProcessDecisionChildMaterialBinding original, ProcessEventActor actor) {
        var payload = JsonSerializer.SerializeToUtf8Bytes(new {
            Domain = "process-decision-review-completion/v1",
            request, input.InputFingerprint, OriginalChild = ProcessDecisionChildMaterialBindingCodec.Serialize(original), Actor = actor
        });
        return new(new Guid(SHA256.HashData(payload).AsSpan(0, 16), bigEndian: true));
    }
}
