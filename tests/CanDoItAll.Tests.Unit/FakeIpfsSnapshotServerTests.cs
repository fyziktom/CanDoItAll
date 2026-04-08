using System.Net.Http.Json;
using System.Text;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit;

public sealed class FakeIpfsSnapshotServerTests
{
    [Fact]
    public async Task Fake_server_accepts_add_pin_and_download_flows()
    {
        await using var server = await FakeIpfsTestServer.StartAsync();
        using var client = new HttpClient
        {
            BaseAddress = server.BaseUri
        };

        using var payload = new MultipartFormDataContent();
        payload.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("snapshot-alpha")), "file", "snapshot-alpha.txt");

        using var addResponse = await client.PostAsync("api/v0/add", payload);
        addResponse.EnsureSuccessStatusCode();

        var added = await addResponse.Content.ReadFromJsonAsync<FakeIpfsAddResponse>();
        Assert.NotNull(added);
        Assert.False(string.IsNullOrWhiteSpace(added.Hash));

        var catPayload = await client.GetStringAsync(server.CreateCatUri(added.Hash));
        Assert.Equal("snapshot-alpha", catPayload);

        using var pinResponse = await client.PostAsync(server.CreatePinUri(added.Hash), content: null);
        pinResponse.EnsureSuccessStatusCode();

        var gatewayPayload = await client.GetStringAsync(server.CreateGatewayUri(added.Hash));
        Assert.Equal("snapshot-alpha", gatewayPayload);
        Assert.Contains(added.Hash, server.StoredCids);
        Assert.Contains(added.Hash, server.PinnedCids);
    }

    private sealed record FakeIpfsAddResponse(string Hash, string Name, string Size);
}
