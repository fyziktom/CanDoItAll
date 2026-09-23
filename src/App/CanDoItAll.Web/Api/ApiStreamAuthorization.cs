using System.Globalization;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Web.Api;

internal static class ApiStreamAuthorization {
    public static async Task<T> WaitAsync<T>(HttpContext context, Task<T> read) {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        while (!read.IsCompleted) {
            var tick = timer.WaitForNextTickAsync(context.RequestAborted).AsTask();
            if (await Task.WhenAny(read, tick) == read) {
                break;
            }
            await tick;
            await EnsureActiveAsync(context, context.RequestAborted);
        }
        await EnsureActiveAsync(context, context.RequestAborted);
        return await read;
    }

    public static async Task EnsureActiveAsync(HttpContext context, CancellationToken cancellationToken) {
        if (!context.RequestServices.GetRequiredService<IOptions<ApiAccessOptions>>().Value.Authorization.Enabled) {
            return;
        }
        try {
            if (await IsActiveAsync(context, cancellationToken)) {
                return;
            }
        } catch (Exception exception) when (exception is not OperationCanceledException) {
            context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(ApiStreamAuthorization))
                .LogError("Stream credential validation failed: {ErrorType}.", exception.GetType().Name);
        }
        context.Abort();
        throw new OperationCanceledException("The stream credential is no longer authorized.", context.RequestAborted);
    }

    private static async Task<bool> IsActiveAsync(HttpContext context, CancellationToken cancellationToken) {
        var clock = context.RequestServices.GetRequiredService<IClock>();
        if (context.User.Identity?.IsAuthenticated != true) {
            return false;
        }
        if (context.Features.Get<ValidatedApiCredential>() is not { } credential) {
            return long.TryParse(context.User.FindFirst("exp")?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var expiry) &&
                clock.GetUtcNow().ToUnixTimeSeconds() < expiry + 30;
        }
        var record = await context.RequestServices.GetRequiredService<IApiTokenRegistry>().FindAsync(credential.Id, cancellationToken);
        if (record is null || record.Kind != credential.Kind || record.GetStatus(clock.GetUtcNow()) != ApiTokenStatus.Active) {
            return false;
        }
        if (record.Kind == ApiCredentialKind.Machine) {
            var issuedScopes = ApiAuthorizationPolicies.ScopeValues(context.User)
                .SelectMany(value => value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
            return issuedScopes.SequenceEqual(record.Scopes.Order(StringComparer.Ordinal), StringComparer.Ordinal);
        }
        return await context.RequestServices.GetRequiredService<ApiSessionService>().ResolveIdentityAsync(record, cancellationToken) is not null;
    }
}
