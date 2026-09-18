using System.Diagnostics;
using CanDoItAll.Modules.CrmHr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.CrmHr;

// Controlled interception of the CRM / HR owner calls a routed host makes around a commit.
//
// The owner keeps its real persistence. A write can be held open before it reaches the database, the context's own
// SavedChanges event tells this control that the commit happened, and the read the host issues afterwards can be held
// open or made to fail. Nothing is faked: every held or failed call is the real owner call. Each one is named by the
// owner method that issues it, so a second owner read inside the write itself (a search document upsert, an activity
// record) is never mistaken for the host's read-back, and a host whose call shape changes fails its test instead of
// silently proving nothing.
internal sealed class CrmHrOwnerReadBackControl : IDbContextFactory<CrmHrDbContext>
{
    private readonly TaskCompletionSource reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private IDbContextFactory<CrmHrDbContext>? inner;
    private string[]? heldWrite;
    private string[]? heldRead;
    private string[]? failedRead;
    private int commits;
    private int interceptions;

    // Commits the owner performed through this factory, and the reads this control actually intercepted. A test that
    // expects an interception asserts both, so a renamed or restructured owner read cannot pass unnoticed.
    public int Commits => Volatile.Read(ref commits);

    public int Interceptions => Volatile.Read(ref interceptions);

    // Completes when the held owner call has been reached and is waiting for Release.
    public Task HoldReached => reached.Task;

    // Replaces the CRM / HR context factory with this control, keeping the registered factory as its owner.
    public static CrmHrOwnerReadBackControl Install(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var descriptor = services.Last(item => item.ServiceType == typeof(IDbContextFactory<CrmHrDbContext>));
        services.Remove(descriptor);
        var control = new CrmHrOwnerReadBackControl();
        services.Add(new ServiceDescriptor(
            typeof(IDbContextFactory<CrmHrDbContext>),
            provider => control.Bind(BuildInner(provider, descriptor)),
            descriptor.Lifetime));
        services.AddSingleton(control);
        return control;
    }

    // Holds the first read after a commit whose call stack carries all of the named frames, until Release is called.
    // Naming the host method as well as the owner read keeps an unrelated read of the same owner method, issued by a
    // load that is still in flight, from taking the hold. Arming restarts the commit count, so Commits is what the
    // operation under test wrote and not what setting up the fixture wrote.
    public void HoldReadIn(params string[] frames)
    {
        Interlocked.Exchange(ref commits, 0);
        Volatile.Write(ref heldRead, frames);
    }

    // Holds the named owner write before it reaches the database, so the operator can keep editing while it is in
    // flight. Unlike a held read this needs no earlier commit: nothing has been written yet.
    public void HoldWriteIn(params string[] frames)
    {
        Interlocked.Exchange(ref commits, 0);
        Volatile.Write(ref heldWrite, frames);
    }

    // Fails the first read after a commit whose call stack carries all of the named frames.
    public void FailReadIn(params string[] frames)
    {
        Interlocked.Exchange(ref commits, 0);
        Volatile.Write(ref failedRead, frames);
    }

    // Restarts the commit count without arming anything, so a test can count the writes of one operation alone.
    public void ResetCommits() => Interlocked.Exchange(ref commits, 0);

    public void Release() => released.TrySetResult();

    public CrmHrDbContext CreateDbContext()
    {
        if (TryTake(ref failedRead, requireCommit: true))
        {
            throw new InvalidOperationException("The CRM / HR owner could not be reached for the read after the commit.");
        }

        return Observe(Inner.CreateDbContext());
    }

    public async Task<CrmHrDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        if (TryTake(ref heldWrite, requireCommit: false) || TryTake(ref heldRead, requireCommit: true))
        {
            reached.TrySetResult();
            // The watchdog keeps a test that never reaches its Release from hanging the test host: the hold ends and
            // the assertions of that test fail instead.
            try
            {
                await released.Task.WaitAsync(TimeSpan.FromSeconds(60)).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
            }
        }

        if (TryTake(ref failedRead, requireCommit: true))
        {
            throw new InvalidOperationException("The CRM / HR owner could not be reached for the read after the commit.");
        }

        return Observe(await Inner.CreateDbContextAsync(cancellationToken).ConfigureAwait(false));
    }

    private IDbContextFactory<CrmHrDbContext> Inner =>
        inner ?? throw new InvalidOperationException("The CRM / HR context factory was not resolved.");

    private CrmHrOwnerReadBackControl Bind(IDbContextFactory<CrmHrDbContext> factory)
    {
        inner = factory;
        return this;
    }

    private CrmHrDbContext Observe(CrmHrDbContext context)
    {
        context.SavedChanges += (_, _) => Interlocked.Increment(ref commits);
        return context;
    }

    private bool TryTake(ref string[]? armed, bool requireCommit)
    {
        var frames = Volatile.Read(ref armed);
        if (frames is null || (requireCommit && Commits == 0) || !StackMentions(frames))
        {
            return false;
        }

        Volatile.Write(ref armed, null);
        Interlocked.Increment(ref interceptions);
        return true;
    }

    // True when every named frame is on the current stack. An async method runs synchronously until its first await,
    // so the owner read and the host method that called it are both still there when the context is created.
    private static bool StackMentions(string[] frames)
    {
        if (frames.Length == 0)
        {
            return false;
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        var trace = new StackTrace(fNeedFileInfo: false);
        for (var index = 0; index < trace.FrameCount; index++)
        {
            var method = trace.GetFrame(index)?.GetMethod();
            if (method is null)
            {
                continue;
            }

            names.Add(method.Name);
            // An async method runs in a compiler generated state machine named after the method it came from.
            if (method.DeclaringType?.Name is { Length: > 2 } declaring && declaring[0] == '<')
            {
                var end = declaring.IndexOf('>');
                if (end > 1)
                {
                    names.Add(declaring[1..end]);
                }
            }
        }

        return frames.All(names.Contains);
    }

    private static IDbContextFactory<CrmHrDbContext> BuildInner(IServiceProvider provider, ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationInstance is IDbContextFactory<CrmHrDbContext> instance)
        {
            return instance;
        }

        if (descriptor.ImplementationFactory is { } factory)
        {
            return (IDbContextFactory<CrmHrDbContext>)factory(provider);
        }

        return (IDbContextFactory<CrmHrDbContext>)ActivatorUtilities.CreateInstance(
            provider,
            descriptor.ImplementationType ?? throw new InvalidOperationException(
                "The CRM / HR context factory registration cannot be rebuilt for the test control."));
    }
}
