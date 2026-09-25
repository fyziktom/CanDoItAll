using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.AI;
using Npgsql;
using NpgsqlTypes;
using OllamaSharp;
using OpenAI;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "LiveProcess")]
[Trait("Category", "HostPlatform")]
public sealed class PostgreSqlProtocolRestoreTests {
    [Fact]
    public async Task Logical_restore_preserves_legacy_and_compressed_protocol_and_a_256_batch_journal() {
        await using var source = PostgresTestDatabaseLease.Create("protocol-dump");
        await using var target = PostgresTestDatabaseLease.Create("protocol-restore");
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, new string('x', 128 * 1024)));
        var compressed = MafToolProtocolCodec.Encode(response);
        var legacy = AgentToolProtocolEnvelope.Create(MafToolProtocolCodec.Format, 1, JsonSerializer.Serialize(new {
            Packages = string.Join("|", typeof(ChatResponse).Assembly.GetName().Version,
                typeof(OpenAIClient).Assembly.GetName().Version, typeof(OllamaApiClient).Assembly.GetName().Version),
            Shape = typeof(ChatResponse).FullName,
            Value = JsonSerializer.SerializeToElement(response, MafToolProtocolCodec.SerializationOptions)
        }));
        var journal = fixture.NewJournal();
        await using (var lease = await journal.AcquireRunAsync(fixture.Session, default)) {
            using var current = lease.Bind();
            await journal.BeginSegmentAsync(lease, AgentToolAdmissionJournalFixture.Envelope(), null, default);
            for (var turn = 0; turn < 256; turn++) {
                await journal.AdmitBatchAsync(lease, MafToolProtocolCodec.Digest(turn), compressed, [], default);
            }
        }
        var record = (await fixture.NewStore().GetExecutionRunAsync(fixture.Session.ExecutionRunId))!.ToolAdmission!;
        var corrupt = AgentToolProtocolEnvelope.Create(MafToolProtocolCodec.Format,
            MafToolProtocolCodec.CompressedResponseVersion, JsonSerializer.Serialize(new { Brotli = new byte[] { 1, 2, 3 } }));
        var checkpoint = new PreservedCheckpoint(fixture.Session.ExecutionRunId, legacy, compressed, corrupt, record);
        var expected = JsonSerializer.Serialize(checkpoint);
        await using (var connection = new NpgsqlConnection(source.ConnectionString)) {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "create table protocol_restore_fixture (id uuid primary key, payload jsonb not null); insert into protocol_restore_fixture values (@id, @payload);";
            command.Parameters.AddWithValue("id", checkpoint.Id);
            command.Parameters.AddWithValue("payload", NpgsqlDbType.Jsonb, expected);
            await command.ExecuteNonQueryAsync();
        }

        var archive = Path.Combine(Path.GetTempPath(), $"candoitall-protocol-{Guid.NewGuid():N}.dump");
        try {
            await RunClientAsync("pg_dump", source.ConnectionString, "--format=custom", "--file", archive);
            await RunClientAsync("pg_restore", target.ConnectionString, "--exit-on-error", "--single-transaction", archive);
            await using var connection = new NpgsqlConnection(target.ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "select payload::text from protocol_restore_fixture where id = @id;";
            command.Parameters.AddWithValue("id", checkpoint.Id);
            var restored = JsonSerializer.Deserialize<PreservedCheckpoint>((string)(await command.ExecuteScalarAsync())!)!;
            Assert.Equal(expected, JsonSerializer.Serialize(restored));
            Assert.Equal(response.Text, MafToolProtocolCodec.Decode<ChatResponse>(restored.Legacy).Text);
            Assert.Equal(response.Text, MafToolProtocolCodec.Decode<ChatResponse>(restored.Compressed).Text);
            restored.Journal.Validate();
            Assert.Equal(256, restored.Journal.Batches.Length);
            Assert.All(restored.Journal.Batches,
                batch => Assert.Equal(response.Text, MafToolProtocolCodec.Decode<ChatResponse>(batch.Response).Text));
            Assert.Throws<AgentToolAdmissionException>(() => MafToolProtocolCodec.Decode<ChatResponse>(restored.Corrupt));
        } finally {
            File.Delete(archive);
        }
    }

    private static async Task RunClientAsync(string executable, string connectionString, params string[] arguments) {
        var version = new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        version.ArgumentList.Add("--version");
        var reported = await RunAsync(version);
        Assert.Contains("(PostgreSQL) 18.", reported, StringComparison.Ordinal);

        var connection = new NpgsqlConnectionStringBuilder(connectionString);
        var start = new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.Environment["PGHOST"] = connection.Host;
        start.Environment["PGPORT"] = connection.Port.ToString(CultureInfo.InvariantCulture);
        start.Environment["PGUSER"] = connection.Username;
        start.Environment["PGPASSWORD"] = connection.Password;
        start.Environment["PGCONNECT_TIMEOUT"] = "10";
        start.ArgumentList.Add("--no-password");
        start.ArgumentList.Add("--dbname");
        start.ArgumentList.Add(connection.Database!);
        foreach (var argument in arguments) {
            start.ArgumentList.Add(argument);
        }
        await RunAsync(start);
    }

    private static async Task<string> RunAsync(ProcessStartInfo start) {
        using var process = Process.Start(start) ?? throw new InvalidOperationException($"Could not start {start.FileName}.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try {
            await process.WaitForExitAsync(timeout.Token);
        } catch (OperationCanceledException) {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            throw;
        }
        await Task.WhenAll(output, error);
        Assert.True(process.ExitCode == 0, $"{start.FileName} exited with {process.ExitCode}. Client diagnostics suppressed to protect connection details.");
        return await output;
    }

    private sealed record PreservedCheckpoint(Guid Id, AgentToolProtocolEnvelope Legacy,
        AgentToolProtocolEnvelope Compressed, AgentToolProtocolEnvelope Corrupt, AgentToolJournalRecord Journal);
}
