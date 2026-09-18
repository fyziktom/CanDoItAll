using System.Security.Cryptography;
using System.Text;

namespace CanDoItAll.AgentFramework.Core;

public static class WorkflowMappedProcessInputFingerprint {
    public static string Compute(string inputJson) => "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        WorkflowLaunchIdempotencyRequestFactory.CanonicalizeInputJson(inputJson)))).ToLowerInvariant();
}
