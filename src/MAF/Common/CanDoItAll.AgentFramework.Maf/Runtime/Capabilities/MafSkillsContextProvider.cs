using Microsoft.Agents.AI;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class MafSkillsContextProvider(AgentSkillsProvider inner, MafContextToolRegistration registration)
    : AIContextProvider, IDisposable {
    public override IReadOnlyList<string> StateKeys => inner.StateKeys;

    protected override async ValueTask<AIContext> InvokingCoreAsync(InvokingContext context,
        CancellationToken cancellationToken = default) {
        var inherited = context.AIContext.Tools?.ToArray();
        if (inherited is not null) {
            context.AIContext.Tools = inherited;
        }
        using var source = (registration.SourcePreparation as MafSkillSourcePreparation)?.Begin();
        if (source is not null) {
            await source.CaptureAsync(cancellationToken);
        }
        var result = await inner.InvokingAsync(context, cancellationToken);
        if (source is not null) {
            await source.CompleteAsync(cancellationToken);
        }
        var merged = result.Tools?.ToArray();
        var tools = registration.RequireContribution(inherited ?? [], merged ?? []);
        if (merged is not null) {
            result.Tools = merged;
        }
        MafToolRunContext.Current?.RegisterContextTools(registration, tools);
        return result;
    }

    protected override ValueTask InvokedCoreAsync(InvokedContext context, CancellationToken cancellationToken = default)
        => inner.InvokedAsync(context, cancellationToken);

    public override object? GetService(Type serviceType, object? serviceKey = null)
        => serviceKey is null && serviceType.IsInstanceOfType(this) ? this : inner.GetService(serviceType, serviceKey);

    public void Dispose() => inner.Dispose();
}
