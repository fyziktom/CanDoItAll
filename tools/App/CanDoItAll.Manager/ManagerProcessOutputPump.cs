using System.Text;

namespace CanDoItAll.Manager;

internal static class ManagerProcessOutputPump
{
    public static async Task PumpAsync(
        IManagerProcessLease lease,
        Func<string, bool, CancellationToken, Task> handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(handler);
        var stdout = new OutputCursor();
        var stderr = new OutputCursor();
        while (!lease.HasExited && !cancellationToken.IsCancellationRequested)
        {
            await PublishAsync(lease.CaptureOutput(), stdout, stderr, handler, flush: false, cancellationToken)
                .ConfigureAwait(false);
            await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken).ConfigureAwait(false);
        }

        await PublishAsync(lease.CaptureOutput(), stdout, stderr, handler, flush: true, CancellationToken.None)
            .ConfigureAwait(false);
    }

    private static async Task PublishAsync(
        CanDoItAll.AgentFramework.Core.WorkspaceProcessOutputSnapshot snapshot,
        OutputCursor stdout,
        OutputCursor stderr,
        Func<string, bool, CancellationToken, Task> handler,
        bool flush,
        CancellationToken cancellationToken)
    {
        foreach (var line in stdout.Read(snapshot.Stdout, flush))
        {
            await handler(line, false, cancellationToken).ConfigureAwait(false);
        }

        foreach (var line in stderr.Read(snapshot.Stderr, flush))
        {
            await handler(line, true, cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed class OutputCursor
    {
        private readonly StringBuilder pending = new();
        private int offset;

        public IReadOnlyList<string> Read(string snapshot, bool flush)
        {
            if (snapshot.Length < offset)
            {
                offset = 0;
                pending.Clear();
            }

            if (snapshot.Length > offset)
            {
                pending.Append(snapshot.AsSpan(offset));
                offset = snapshot.Length;
            }

            var lines = new List<string>();
            while (true)
            {
                var newline = IndexOfNewline(pending);
                if (newline < 0)
                {
                    break;
                }

                var length = newline > 0 && pending[newline - 1] == '\r'
                    ? newline - 1
                    : newline;
                lines.Add(pending.ToString(0, length));
                pending.Remove(0, newline + 1);
            }

            if (flush && pending.Length > 0)
            {
                lines.Add(pending.ToString().TrimEnd('\r'));
                pending.Clear();
            }

            return lines;
        }

        private static int IndexOfNewline(StringBuilder value)
        {
            for (var index = 0; index < value.Length; index++)
            {
                if (value[index] == '\n')
                {
                    return index;
                }
            }

            return -1;
        }
    }
}
