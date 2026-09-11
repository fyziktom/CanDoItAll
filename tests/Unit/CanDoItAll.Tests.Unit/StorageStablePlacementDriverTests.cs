using System.Net;
using System.Text;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.Tests.Unit.Storage;

public sealed class StorageStablePlacementDriverTests {
    [Theory]
    [InlineData(IpfsStableAddMode.ComputeOnly, "true")]
    [InlineData(IpfsStableAddMode.Store, "false")]
    public async Task Ipfs_stable_add_sends_explicit_identical_import_options(IpfsStableAddMode mode, string onlyHash) {
        using var handler = new CaptureHandler();
        using var http = new HttpClient(handler);
        var transport = new IpfsHttpStorageTransport(http);
        var storage = new StorageCatalogRecord { EndpointOrRoot = "https://ipfs.example.test/api/v0/" };
        var result = await transport.AddStableAsync(storage.ToDriverInput(), null, "proof.txt", "bounded content"u8.ToArray(), mode, CancellationToken.None);
        Assert.Equal("bafy-test-content", result.ContentId);
        Assert.NotNull(handler.Uri);
        Assert.Equal("/api/v0/add", handler.Uri.AbsolutePath);
        var query = handler.Uri.Query.TrimStart('?').Split('&').Select(part => part.Split('=', 2))
            .ToDictionary(pair => pair[0], pair => pair[1], StringComparer.Ordinal);
        Assert.Equal("1", query["cid-version"]);
        Assert.Equal("sha2-256", query["hash"]);
        Assert.Equal("true", query["raw-leaves"]);
        Assert.Equal("size-262144", query["chunker"]);
        Assert.Equal("false", query["trickle"]);
        Assert.Equal("false", query["wrap-with-directory"]);
        Assert.Equal("false", query["preserve-mode"]);
        Assert.Equal("false", query["preserve-mtime"]);
        Assert.Equal("false", query["pin"]);
        Assert.Equal(onlyHash, query["only-hash"]);
        Assert.Contains("bounded content", handler.Body, StringComparison.Ordinal);
        Assert.Contains("proof.txt", handler.Body, StringComparison.Ordinal);
        Assert.False(handler.HadAuthorization);
    }

    [Fact]
    public async Task Ordinary_ipfs_add_preserves_existing_endpoint_and_options() {
        using var handler = new CaptureHandler();
        using var http = new HttpClient(handler);
        var transport = new IpfsHttpStorageTransport(http);
        await transport.AddAsync((new StorageCatalogRecord() { EndpointOrRoot = "https://ipfs.example.test/" }).ToDriverInput(), null, "ordinary.txt",
            "ordinary"u8.ToArray(), CancellationToken.None);
        Assert.Equal("/api/v0/add", handler.Uri!.AbsolutePath);
        Assert.Empty(handler.Uri.Query);
        Assert.Contains("ordinary", handler.Body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Stable_filesystem_write_never_replaces_an_occupied_target(bool matchingContent) {
        var root = Path.Combine(Path.GetTempPath(), $"stable-storage-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try {
            var storage = new StorageCatalogRecord { Id = Guid.NewGuid(), EndpointOrRoot = root };
            StorageCatalogHostBindingPolicy.BindCurrent(storage, root, DateTimeOffset.UtcNow);
            var driver = (IStorageStablePlacementDriver)new FileSystemStorageDriver(new(new Paths(root)));
            var intent = new StoragePlacementIntentId(Guid.NewGuid());
            var request = new StorageWriteRequest("artifact.txt", "text/plain", "new content"u8.ToArray(),
                StorageUsagePurpose.ProjectAsset, RelativePathHint: "artifact.txt");
            var target = await driver.PrepareStableTargetAsync(storage.ToDriverInput(), intent, request, CancellationToken.None);
            var original = matchingContent ? "new content" : "human content";
            await File.WriteAllTextAsync(Path.Combine(root, "artifact.txt"), original);
            await Assert.ThrowsAnyAsync<IOException>(() => driver.WriteStableTargetAsync(storage.ToDriverInput(), target, request, CancellationToken.None));
            Assert.Equal(original, await File.ReadAllTextAsync(Path.Combine(root, "artifact.txt")));
            Assert.Equal(["artifact.txt", "artifact.txt.candoitall.lock"],
                Directory.GetFiles(root).Select(Path.GetFileName).Order(StringComparer.Ordinal));
            await Assert.ThrowsAsync<ArgumentException>(() => driver.PrepareStableTargetAsync(storage.ToDriverInput(), intent,
                request with { RelativePathHint = "../outside.txt" }, CancellationToken.None));
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Stable_reference_wire_format_preserves_intent_and_rejects_stripped_evidence_while_ordinary_v2_stays_compatible() {
        var ordinary = new StorageObjectReference(Guid.NewGuid(), StorageProviderKind.FileSystem, StorageLocatorKind.RelativePath, "artifact.txt");
        var ordinaryJson = StorageJson.SerializeReference(ordinary);
        Assert.Contains("\"formatVersion\":2", ordinaryJson, StringComparison.Ordinal);
        Assert.DoesNotContain("placementIntentId", ordinaryJson, StringComparison.Ordinal);
        var stable = ordinary with { PlacementIntentId = Guid.NewGuid() };
        var json = StorageJson.SerializeReference(stable);
        Assert.Contains("\"formatVersion\":3", json, StringComparison.Ordinal);
        var reloaded = StorageJson.ParseReference(json)!;
        Assert.Equal(stable.PlacementIntentId, reloaded.PlacementIntentId);
        Assert.Equal(StorageObjectReference.StablePlacementFormatVersion, reloaded.FormatVersion);
        Assert.True(StorageJson.TryDecodeReferenceToken(StorageJson.EncodeReferenceToken(reloaded), out var tokenReference));
        Assert.Equal(reloaded, tokenReference);
        var stripped = System.Text.Json.Nodes.JsonNode.Parse(json)!;
        stripped.AsObject().Remove("placementIntentId");
        Assert.Throws<InvalidOperationException>(() => StorageJson.ParseReference(stripped.ToJsonString()));
        Assert.Throws<InvalidOperationException>(() => StorageJson.SerializeReference(stable with { PlacementIntentId = Guid.Empty }));
        Assert.Throws<InvalidOperationException>(() => StorageJson.SerializeReference(stable with { FormatVersion = 4 }));
    }

    private sealed class CaptureHandler : HttpMessageHandler {
        public Uri? Uri { get; private set; }
        public string Body { get; private set; } = string.Empty;
        public bool HadAuthorization { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            Uri = request.RequestUri;
            HadAuthorization = request.Headers.Authorization is not null;
            Body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            return new(HttpStatusCode.OK) { Content = new StringContent("{\"Hash\":\"bafy-test-content\"}", Encoding.UTF8, "application/json") };
        }
    }

    private sealed class Paths(string root) : IWorkspacePathResolver {
        public string ResolveWorkspaceRoot() => root;
        public string ResolveManagedFilesRoot() => Path.Combine(root, "managed-files");
        public string ResolveExportsRoot() => Path.Combine(root, "exports");
        public string ResolveEvidenceRoot() => Path.Combine(root, "evidence");
        public string ResolveManagerArtifactsRoot() => Path.Combine(root, "manager-artifacts");
    }
}
