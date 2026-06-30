using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace CanDoItAll.Tests.Support;

public sealed class FakeIpfsTestServer : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, byte[]> _blocks;
    private readonly ConcurrentDictionary<string, byte> _pinnedCids;
    private readonly WebApplication _application;

    private FakeIpfsTestServer(
        WebApplication application,
        ConcurrentDictionary<string, byte[]> blocks,
        ConcurrentDictionary<string, byte> pinnedCids,
        Uri baseUri)
    {
        _application = application;
        _blocks = blocks;
        _pinnedCids = pinnedCids;
        BaseUri = baseUri;
    }

    public Uri BaseUri { get; }

    public Uri ApiBaseUri => new(BaseUri, "api/v0/");

    public Uri GatewayBaseUri => new(BaseUri, "ipfs/");

    public IReadOnlyCollection<string> StoredCids => _blocks.Keys.OrderBy(value => value, StringComparer.Ordinal).ToArray();

    public IReadOnlyCollection<string> PinnedCids => _pinnedCids.Keys.OrderBy(value => value, StringComparer.Ordinal).ToArray();

    public static async Task<FakeIpfsTestServer> StartAsync(CancellationToken cancellationToken = default)
    {
        var port = GetFreePort();
        var baseUri = new Uri($"http://127.0.0.1:{port}/");
        var blocks = new ConcurrentDictionary<string, byte[]>(StringComparer.Ordinal);
        var pinnedCids = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls(baseUri.ToString());

        var application = builder.Build();

        application.MapPost("/api/v0/add", (Delegate)((HttpContext context) => HandleAddAsync(context, blocks)));
        application.MapMethods("/api/v0/version", ["GET", "POST"], () => TypedResults.Json(new
        {
            Version = "0.0.0-test",
            Commit = "fake",
            Repo = "test"
        }));
        application.MapMethods("/api/v0/cat", ["GET", "POST"], (Delegate)((HttpContext context) => HandleCat(context, blocks)));
        application.MapPost("/api/v0/pin/add", (Delegate)((HttpContext context) => HandlePinAdd(context, blocks, pinnedCids)));
        application.MapGet("/ipfs/{cid}", (string cid) => ResolveBytesResult(cid, blocks));

        await application.StartAsync(cancellationToken);

        return new FakeIpfsTestServer(application, blocks, pinnedCids, baseUri);
    }

    public Uri CreateCatUri(string cid) => new(BaseUri, $"api/v0/cat?arg={Uri.EscapeDataString(cid)}");

    public Uri CreatePinUri(string cid) => new(BaseUri, $"api/v0/pin/add?arg={Uri.EscapeDataString(cid)}");

    public Uri CreateGatewayUri(string cid) => new(BaseUri, $"ipfs/{Uri.EscapeDataString(cid)}");

    public Task<string> StoreTextAsync(string content)
    {
        var cid = StoreBlock(_blocks, Encoding.UTF8.GetBytes(content));
        return Task.FromResult(cid);
    }

    public async ValueTask DisposeAsync()
    {
        await _application.StopAsync();
        await _application.DisposeAsync();
    }

    private static async Task<IResult> HandleAddAsync(
        HttpContext context,
        ConcurrentDictionary<string, byte[]> blocks)
    {
        var payload = await ReadPayloadAsync(context.Request, context.RequestAborted);
        if (payload is null || payload.Content.Length == 0)
        {
            return TypedResults.BadRequest(new { Message = "Missing IPFS payload." });
        }

        var cid = StoreBlock(blocks, payload.Content);
        return TypedResults.Json(new
        {
            Name = payload.FileName,
            Hash = cid,
            Size = payload.Content.Length.ToString(CultureInfo.InvariantCulture)
        });
    }

    private static IResult HandleCat(
        HttpContext context,
        ConcurrentDictionary<string, byte[]> blocks)
    {
        var cid = context.Request.Query["arg"].ToString();
        return ResolveBytesResult(cid, blocks);
    }

    private static IResult HandlePinAdd(
        HttpContext context,
        ConcurrentDictionary<string, byte[]> blocks,
        ConcurrentDictionary<string, byte> pinnedCids)
    {
        var cid = context.Request.Query["arg"].ToString();
        if (string.IsNullOrWhiteSpace(cid) || !blocks.ContainsKey(cid))
        {
            return TypedResults.NotFound();
        }

        pinnedCids[cid] = 0;
        return TypedResults.Json(new { Pins = new[] { cid } });
    }

    private static async Task<UploadedPayload?> ReadPayloadAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (request.HasFormContentType)
        {
            var form = await request.ReadFormAsync(cancellationToken);
            var file = form.Files.FirstOrDefault();
            if (file is not null)
            {
                await using var fileStream = file.OpenReadStream();
                using var buffer = new MemoryStream();
                await fileStream.CopyToAsync(buffer, cancellationToken);
                return new UploadedPayload(buffer.ToArray(), file.FileName);
            }
        }

        using var body = new MemoryStream();
        await request.Body.CopyToAsync(body, cancellationToken);
        if (body.Length == 0)
        {
            return null;
        }

        var fileName = request.Headers["X-Test-File-Name"].ToString();
        return new UploadedPayload(body.ToArray(), string.IsNullOrWhiteSpace(fileName) ? "payload.bin" : fileName);
    }

    private static IResult ResolveBytesResult(
        string cid,
        ConcurrentDictionary<string, byte[]> blocks)
    {
        return string.IsNullOrWhiteSpace(cid) || !blocks.TryGetValue(cid, out var payload)
            ? TypedResults.NotFound()
            : TypedResults.Bytes(payload, "application/octet-stream");
    }

    private static string StoreBlock(ConcurrentDictionary<string, byte[]> blocks, byte[] content)
    {
        var cid = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        blocks[cid] = content;
        return cid;
    }

    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private sealed record UploadedPayload(byte[] Content, string FileName);
}
