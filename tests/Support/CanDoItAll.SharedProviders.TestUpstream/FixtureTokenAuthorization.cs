using System.Security.Cryptography;
using System.Text;

namespace CanDoItAll.SharedProviders.TestUpstream;

internal sealed class FixtureAuthenticationOptions
{
    private const int MaximumTokenBytes = 4096;
    private const string DataTokenFileKey = "Fixture:Authentication:DataTokenFile";
    private const string ControlTokenFileKey = "Fixture:Authentication:ControlTokenFile";

    private FixtureAuthenticationOptions(byte[] dataToken, byte[] controlToken)
    {
        DataToken = dataToken;
        ControlToken = controlToken;
    }

    public ReadOnlyMemory<byte> DataToken { get; }

    public ReadOnlyMemory<byte> ControlToken { get; }

    public static FixtureAuthenticationOptions Load(
        IConfiguration configuration,
        string contentRootPath)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRootPath);

        var dataToken = ReadTokenFile(
            configuration[DataTokenFileKey],
            contentRootPath,
            "data token");
        var controlToken = ReadTokenFile(
            configuration[ControlTokenFileKey],
            contentRootPath,
            "control token");
        if (CryptographicOperations.FixedTimeEquals(dataToken, controlToken))
        {
            throw new InvalidOperationException(
                "The deterministic upstream data and control tokens must be distinct.");
        }

        return new FixtureAuthenticationOptions(dataToken, controlToken);
    }

    private static byte[] ReadTokenFile(
        string? configuredPath,
        string contentRootPath,
        string description)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            throw new InvalidOperationException(
                $"A deterministic upstream {description} file is required.");
        }

        var path = Path.IsPathRooted(configuredPath)
            ? Path.GetFullPath(configuredPath)
            : Path.GetFullPath(configuredPath, contentRootPath);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"The deterministic upstream {description} file was not found.");
        }

        var fileInfo = new FileInfo(path);
        if ((fileInfo.Attributes &
                (FileAttributes.Directory | FileAttributes.ReparsePoint | FileAttributes.Device)) != 0 ||
            fileInfo.LinkTarget is not null ||
            fileInfo.Length is <= 0 or > MaximumTokenBytes)
        {
            throw new InvalidOperationException(
                $"The deterministic upstream {description} file is invalid.");
        }

        byte[] bytes = File.ReadAllBytes(path);
        if (bytes.Length is <= 0 or > MaximumTokenBytes || bytes.Contains((byte)0))
        {
            throw new InvalidOperationException(
                $"The deterministic upstream {description} file is invalid.");
        }

        int length = bytes.Length;
        while (length > 0 && bytes[length - 1] is (byte)'\r' or (byte)'\n')
        {
            length--;
        }

        if (length == 0)
        {
            throw new InvalidOperationException(
                $"The deterministic upstream {description} file is empty.");
        }

        return bytes.AsSpan(0, length).ToArray();
    }
}

internal sealed class FixtureTokenAuthorizationMiddleware(RequestDelegate next)
{
    private const int BearerPrefixLength = 7;

    public async Task InvokeAsync(
        HttpContext context,
        FixtureAuthenticationOptions authentication)
    {
        if (context.Request.Path.Equals("/health") ||
            IsOllamaDataPath(context.Request.Path) ||
            IsComfyUiDataPath(context.Request.Path))
        {
            await next(context);
            return;
        }

        ReadOnlyMemory<byte> expected = context.Request.Path.StartsWithSegments("/_test")
            ? authentication.ControlToken
            : authentication.DataToken;
        if (!TryReadBearerToken(context.Request, out var supplied) ||
            !CryptographicOperations.FixedTimeEquals(supplied, expected.Span))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(
                new FixtureErrorEnvelope(new FixtureError(
                    "The deterministic upstream credential is invalid.",
                    "fixture_unauthorized",
                    "fixture_unauthorized")),
                FixtureJson.Options,
                context.RequestAborted);
            return;
        }

        await next(context);
    }

    private static bool IsComfyUiDataPath(PathString path)
        => path.Equals("/system_stats") ||
           path.Equals("/prompt") ||
           path.Equals("/view") ||
           path.StartsWithSegments("/history");

    private static bool IsOllamaDataPath(PathString path)
        => path.Equals("/api/tags") ||
           path.Equals("/api/show") ||
           path.Equals("/api/chat");

    private static bool TryReadBearerToken(HttpRequest request, out byte[] token)
    {
        token = [];
        if (!request.Headers.TryGetValue("Authorization", out var values) ||
            values.Count != 1)
        {
            return false;
        }

        string? value = values[0];
        if (string.IsNullOrEmpty(value) ||
            !value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        token = Encoding.UTF8.GetBytes(value[BearerPrefixLength..]);
        return token.Length > 0;
    }
}
