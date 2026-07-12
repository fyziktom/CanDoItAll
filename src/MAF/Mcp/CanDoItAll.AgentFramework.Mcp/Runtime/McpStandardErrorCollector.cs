using System.Text;

namespace CanDoItAll.AgentFramework.Mcp;

internal sealed class McpStandardErrorCollector
{
    private const int MaximumDiagnosticCharacters = 8192;

    private readonly StringBuilder buffer = new();
    private readonly object gate = new();
    private Task? pumpTask;

    public void Start(StreamReader reader)
    {
        pumpTask = PumpAsync(reader);
    }

    public async Task WaitForCompletionAsync()
    {
        if (pumpTask is not null)
        {
            await Task.WhenAny(
                pumpTask,
                Task.Delay(TimeSpan.FromMilliseconds(250), CancellationToken.None));
        }
    }

    public string BuildDiagnosticSuffix()
    {
        lock (gate)
        {
            return buffer.Length == 0
                ? string.Empty
                : $" Stderr: {buffer}";
        }
    }

    private async Task PumpAsync(StreamReader reader)
    {
        var chunk = new char[1024];
        while (true)
        {
            var read = await reader.ReadAsync(chunk);
            if (read == 0)
            {
                return;
            }

            lock (gate)
            {
                buffer.Append(chunk, 0, read);
                if (buffer.Length > MaximumDiagnosticCharacters)
                {
                    buffer.Remove(0, buffer.Length - MaximumDiagnosticCharacters);
                }
            }
        }
    }
}
