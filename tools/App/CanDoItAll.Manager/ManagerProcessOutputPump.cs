using System.Text;

namespace CanDoItAll.Manager;

internal enum ManagerProcessOutputPumpCompletion
{
    ProcessExited,
    CaptureLimitReached
}

internal static class ManagerProcessOutputPump
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(25);

    // Captured output is trimmed, so the newline that ends the latest line only becomes visible when
    // more output follows. A long-running tool that prints one line and goes quiet would otherwise
    // never report it; a pending line that stays unchanged for this long is published as complete.
    internal static readonly TimeSpan IdleLineCompletionDelay = TimeSpan.FromMilliseconds(250);

    public static async Task<ManagerProcessOutputPumpCompletion> PumpAsync(
        IManagerProcessLease lease,
        Func<string, bool, CancellationToken, Task> handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(handler);
        var stdout = new ManagerProcessOutputCursor(IdleLineCompletionDelay);
        var stderr = new ManagerProcessOutputCursor(IdleLineCompletionDelay);
        while (!lease.HasExited)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = lease.CaptureOutput();
            if (snapshot.StdoutTruncated || snapshot.StderrTruncated)
            {
                await PublishAsync(snapshot, stdout, stderr, handler, flush: true, CancellationToken.None)
                    .ConfigureAwait(false);
                return ManagerProcessOutputPumpCompletion.CaptureLimitReached;
            }

            await PublishAsync(snapshot, stdout, stderr, handler, flush: false, cancellationToken)
                .ConfigureAwait(false);
            await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
        }

        await PublishAsync(lease.CaptureOutput(), stdout, stderr, handler, flush: true, CancellationToken.None)
            .ConfigureAwait(false);
        return ManagerProcessOutputPumpCompletion.ProcessExited;
    }

    private static async Task PublishAsync(
        CanDoItAll.AgentFramework.Core.WorkspaceProcessOutputSnapshot snapshot,
        ManagerProcessOutputCursor stdout,
        ManagerProcessOutputCursor stderr,
        Func<string, bool, CancellationToken, Task> handler,
        bool flush,
        CancellationToken cancellationToken)
    {
        var nowMilliseconds = Environment.TickCount64;
        foreach (var line in stdout.Read(snapshot.Stdout, flush, nowMilliseconds))
        {
            await handler(line, false, cancellationToken).ConfigureAwait(false);
        }

        foreach (var line in stderr.Read(snapshot.Stderr, flush, nowMilliseconds))
        {
            await handler(line, true, cancellationToken).ConfigureAwait(false);
        }
    }
}

internal sealed class ManagerProcessOutputCursor(TimeSpan idleLineCompletionDelay)
{
    private readonly StringBuilder pending = new();
    private readonly long idleLineCompletionMilliseconds = (long)idleLineCompletionDelay.TotalMilliseconds;
    private int offset;
    private long lastGrowthMilliseconds;
    private bool pendingLineCompletedWhileIdle;

    public IReadOnlyList<string> Read(string snapshot, bool flush, long nowMilliseconds)
    {
        if (snapshot.Length < offset)
        {
            offset = 0;
            pending.Clear();
            pendingLineCompletedWhileIdle = false;
        }

        if (snapshot.Length > offset)
        {
            var appended = snapshot.AsSpan(offset);
            offset = snapshot.Length;
            lastGrowthMilliseconds = nowMilliseconds;
            if (pendingLineCompletedWhileIdle)
            {
                appended = SkipTerminatorOfIdleCompletedLine(appended);
                pendingLineCompletedWhileIdle = false;
            }

            pending.Append(appended);
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

        if (pending.Length == 0)
        {
            return lines;
        }

        if (flush || nowMilliseconds - lastGrowthMilliseconds >= idleLineCompletionMilliseconds)
        {
            lines.Add(pending.ToString().TrimEnd('\r'));
            pending.Clear();
            pendingLineCompletedWhileIdle = !flush;
        }

        return lines;
    }

    private static ReadOnlySpan<char> SkipTerminatorOfIdleCompletedLine(ReadOnlySpan<char> appended)
    {
        var index = 0;
        while (index < appended.Length && appended[index] is ' ' or '\t' or '\r')
        {
            index++;
        }

        return index < appended.Length && appended[index] == '\n'
            ? appended[(index + 1)..]
            : appended;
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
