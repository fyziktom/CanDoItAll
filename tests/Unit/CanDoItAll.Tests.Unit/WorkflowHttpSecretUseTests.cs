using System.Text.Json;
using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Network;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class WorkflowHttpSecretUseTests {
    [Theory]
    [InlineData("original-token", "original-token")]
    [InlineData("original-token", "Bearer original-token")]
    [InlineData("a\"b", "a\\u0022b")]
    [InlineData("a\"b", "a\\\"b")]
    public void CredentialEchoIsRemovedWithoutLosingUnrelatedResponse(string secret, string echo) {
        using var use = new WorkflowHttpSecretUse(secret, "X-Private-Key", $"Bearer {secret}");
        var safe = use.Redact($"before {echo} after");
        Assert.Equal("before [REDACTED] after", safe);
        Assert.Equal("{}", JsonSerializer.Serialize(use));
    }

    [Theory]
    [InlineData("a/b", """{"value":"a\/b"}""")]
    [InlineData("a\"b/c", """{"value":"\u0061\"b\/\u0063"}""")]
    [InlineData("a/b", """{"wrapped":"{\"value\":\"a\\/b\"}"}""")]
    public void SemanticJsonStringsCannotHideStandardEscapedCredential(string secret, string response) {
        using var use = new WorkflowHttpSecretUse(secret, "X-Private-Key", secret);
        var safe = use.Redact(response);
        Assert.Contains("[REDACTED]", safe, StringComparison.Ordinal);
        using var parsed = JsonDocument.Parse(safe);
        if (parsed.RootElement.TryGetProperty("wrapped", out var wrapped)) {
            using var inner = JsonDocument.Parse(wrapped.GetString()!);
            Assert.Equal("[REDACTED]", inner.RootElement.GetProperty("value").GetString());
        } else {
            Assert.Equal("[REDACTED]", parsed.RootElement.GetProperty("value").GetString());
        }
    }

    [Fact]
    public void NonsecretJsonBytesAndNumericRepresentationRemainUnchanged() {
        const string response = """{ "value" : "a\/b", "quantity": 1.2300e+02 }""";
        using var use = new WorkflowHttpSecretUse("another-credential", "X-Private-Key", "another-credential");
        Assert.Equal(response, use.Redact(response));
    }

    [Theory]
    [InlineData("""{"safe":"kept","value":"a\/""")]
    [InlineData("""{"safe":"kept","value":"\u0061\u002""")]
    [InlineData("""["kept","\u0061\/b""")]
    [InlineData("""{"safe":"kept","\u0061\/b""")]
    public void TruncatedJsonStringCannotExposeEscapedCredentialPrefix(string response) {
        using var use = new WorkflowHttpSecretUse("a/b", "X-Private-Key", "a/b");
        var safe = use.Redact(response, truncated: true);
        Assert.Contains("kept", safe, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", safe, StringComparison.Ordinal);
        Assert.DoesNotContain("a\\/", safe, StringComparison.Ordinal);
    }

    [Fact]
    public void SemanticPropertyNameRedactionPreservesOtherBytes() {
        const string response = """{ "a\/b" : 1.2300e+02, "safe": true }""";
        using var use = new WorkflowHttpSecretUse("a/b", "X-Private-Key", "a/b");
        Assert.Equal("""{ "[REDACTED]" : 1.2300e+02, "safe": true }""", use.Redact(response));
    }

    [Theory]
    [InlineData("""{"value":"\uD800","later":"a\/b","safe":"kept"}""")]
    [InlineData("""{"\uD800":"first","later":"a\/b","safe":"kept"}""")]
    public void UndecodableJsonTokenIsMaskedWithoutFailingOrSkippingLaterCredentials(string response) {
        using var use = new WorkflowHttpSecretUse("a/b", "X-Private-Key", "a/b");
        var safe = use.Redact(response);
        using var document = JsonDocument.Parse(safe);
        Assert.Equal("kept", document.RootElement.GetProperty("safe").GetString());
        Assert.Equal("[REDACTED]", document.RootElement.GetProperty("later").GetString());
        Assert.DoesNotContain("\\uD800", safe, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Authorization")]
    [InlineData("Set-Cookie")]
    [InlineData("Proxy-Authorization")]
    [InlineData("X-Api-Key")]
    [InlineData("X-Private-Key")]
    public void CredentialBearingHeaderCannotDiscloseAnotherTransportCredential(string name) {
        using var use = new WorkflowHttpSecretUse("vault-value", "X-Private-Key", "Bearer vault-value");
        Assert.Equal("[REDACTED]", use.RedactHeader(name, "new-transport-credential"));
        Assert.Equal("public-cache-info", use.RedactHeader("X-Public", "public-cache-info"));
    }

    [Fact]
    public void TruncatedBodyCannotExposeAPrefixOfTheCredential() {
        using var use = new WorkflowHttpSecretUse("original-token", "Authorization", "Bearer original-token");
        Assert.Equal("safe [REDACTED]", use.Redact("safe original-to", truncated: true));
        Assert.Equal("safe original-to", use.Redact("safe original-to"));
    }

    [Fact]
    public void DisposedUseCannotBeReusedForAnotherResponse() {
        var use = new WorkflowHttpSecretUse("vault-value", "Authorization", "Bearer vault-value");
        use.Dispose();
        Assert.Throws<ObjectDisposedException>(() => use.Redact("vault-value"));
    }
}
