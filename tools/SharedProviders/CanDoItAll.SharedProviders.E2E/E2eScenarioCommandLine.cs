namespace CanDoItAll.SharedProviders.E2E;

internal enum E2eScenarioPhase
{
    Normal,
    Unpublished,
    Republished,
    IdentityMismatch,
    IdentityRestored,
    Outage,
    Recovery
}

internal sealed record E2eScenarioOptions(
    E2eScenarioPhase Phase,
    string ArtifactRootPath,
    Uri CentralBaseUri,
    Uri ClientABaseUri,
    Uri ClientBBaseUri,
    Uri UpstreamControlBaseUri,
    Uri PersonalUpstreamControlBaseUri,
    string UpstreamControlTokenFilePath,
    string PersonalUpstreamControlTokenFilePath,
    string CentralDatabaseConnectionStringFilePath,
    string ClientADatabaseConnectionStringFilePath,
    string ClientBDatabaseConnectionStringFilePath);

internal static class E2eScenarioCommandLine
{
    private const string PhaseOption = "--phase";
    private const string ArtifactRootOption = "--artifact-root";
    private const string CentralUriOption = "--central-uri";
    private const string ClientAUriOption = "--client-a-uri";
    private const string ClientBUriOption = "--client-b-uri";
    private const string UpstreamControlUriOption = "--upstream-control-uri";
    private const string PersonalUpstreamControlUriOption = "--personal-upstream-control-uri";
    private const string UpstreamControlTokenFileOption = "--upstream-control-token-file";
    private const string PersonalUpstreamControlTokenFileOption = "--personal-upstream-control-token-file";
    private const string CentralDatabaseConnectionStringFileOption = "--central-connection-string-file";
    private const string ClientADatabaseConnectionStringFileOption = "--client-a-connection-string-file";
    private const string ClientBDatabaseConnectionStringFileOption = "--client-b-connection-string-file";

    private const string ArtifactRootEnvironment = "SHARED_PROVIDERS_E2E_ROOT";
    private const string CentralUriEnvironment = "SHARED_PROVIDERS_E2E_CENTRAL_URI";
    private const string ClientAUriEnvironment = "SHARED_PROVIDERS_E2E_CLIENT_A_URI";
    private const string ClientBUriEnvironment = "SHARED_PROVIDERS_E2E_CLIENT_B_URI";
    private const string UpstreamControlUriEnvironment = "SHARED_PROVIDERS_E2E_UPSTREAM_CONTROL_URI";
    private const string PersonalUpstreamControlUriEnvironment =
        "SHARED_PROVIDERS_E2E_PERSONAL_UPSTREAM_CONTROL_URI";
    private const string UpstreamControlTokenFileEnvironment =
        "SHARED_PROVIDERS_E2E_UPSTREAM_CONTROL_TOKEN_FILE";
    private const string PersonalUpstreamControlTokenFileEnvironment =
        "SHARED_PROVIDERS_E2E_PERSONAL_UPSTREAM_CONTROL_TOKEN_FILE";
    private const string CentralDatabaseConnectionStringFileEnvironment =
        "SHARED_PROVIDERS_E2E_CENTRAL_DATABASE_CONNECTION_STRING_FILE";
    private const string ClientADatabaseConnectionStringFileEnvironment =
        "SHARED_PROVIDERS_E2E_CLIENT_A_DATABASE_CONNECTION_STRING_FILE";
    private const string ClientBDatabaseConnectionStringFileEnvironment =
        "SHARED_PROVIDERS_E2E_CLIENT_B_DATABASE_CONNECTION_STRING_FILE";

    private static readonly IReadOnlySet<string> KnownOptions = new HashSet<string>(
    [
        PhaseOption,
        ArtifactRootOption,
        CentralUriOption,
        ClientAUriOption,
        ClientBUriOption,
        UpstreamControlUriOption,
        PersonalUpstreamControlUriOption,
        UpstreamControlTokenFileOption,
        PersonalUpstreamControlTokenFileOption,
        CentralDatabaseConnectionStringFileOption,
        ClientADatabaseConnectionStringFileOption,
        ClientBDatabaseConnectionStringFileOption
    ], StringComparer.Ordinal);

    public static bool IsScenarioCommand(string[] args)
        => args.Length > 0 && string.Equals(args[0], "run-scenarios", StringComparison.Ordinal);

    public static E2eScenarioOptions Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var values = ParseOptions(args);
        return new E2eScenarioOptions(
            ResolvePhase(GetRequired(values, PhaseOption, environmentVariable: null)),
            ResolveRequiredPath(values, ArtifactRootOption, ArtifactRootEnvironment, requireFile: false),
            ResolveUri(values, CentralUriOption, CentralUriEnvironment),
            ResolveUri(values, ClientAUriOption, ClientAUriEnvironment),
            ResolveUri(values, ClientBUriOption, ClientBUriEnvironment),
            ResolveUri(values, UpstreamControlUriOption, UpstreamControlUriEnvironment),
            ResolveUri(values, PersonalUpstreamControlUriOption, PersonalUpstreamControlUriEnvironment),
            ResolveRequiredPath(
                values,
                UpstreamControlTokenFileOption,
                UpstreamControlTokenFileEnvironment,
                requireFile: true),
            ResolveRequiredPath(
                values,
                PersonalUpstreamControlTokenFileOption,
                PersonalUpstreamControlTokenFileEnvironment,
                requireFile: true),
            ResolveRequiredPath(
                values,
                CentralDatabaseConnectionStringFileOption,
                CentralDatabaseConnectionStringFileEnvironment,
                requireFile: true),
            ResolveRequiredPath(
                values,
                ClientADatabaseConnectionStringFileOption,
                ClientADatabaseConnectionStringFileEnvironment,
                requireFile: true),
            ResolveRequiredPath(
                values,
                ClientBDatabaseConnectionStringFileOption,
                ClientBDatabaseConnectionStringFileEnvironment,
                requireFile: true));
    }

    public static string ToToken(E2eScenarioPhase phase) => phase switch
    {
        E2eScenarioPhase.Normal => "normal",
        E2eScenarioPhase.Unpublished => "unpublished",
        E2eScenarioPhase.Republished => "republished",
        E2eScenarioPhase.IdentityMismatch => "identity-mismatch",
        E2eScenarioPhase.IdentityRestored => "identity-restored",
        E2eScenarioPhase.Outage => "outage",
        E2eScenarioPhase.Recovery => "recovery",
        _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, null)
    };

    private static E2eScenarioPhase ResolvePhase(string value) => value switch
    {
        "normal" => E2eScenarioPhase.Normal,
        "unpublished" => E2eScenarioPhase.Unpublished,
        "republished" => E2eScenarioPhase.Republished,
        "identity-mismatch" => E2eScenarioPhase.IdentityMismatch,
        "identity-restored" => E2eScenarioPhase.IdentityRestored,
        "outage" => E2eScenarioPhase.Outage,
        "recovery" => E2eScenarioPhase.Recovery,
        _ => throw new E2eSafeException(
            "The scenario phase must be normal, unpublished, republished, identity-mismatch, identity-restored, outage, or recovery.")
    };

    private static Dictionary<string, string> ParseOptions(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 1; index < args.Length; index += 2)
        {
            var option = args[index];
            if (!KnownOptions.Contains(option))
            {
                throw new E2eSafeException("The run-scenarios command contains an unsupported option.");
            }

            if (index == args.Length - 1 || string.IsNullOrWhiteSpace(args[index + 1]))
            {
                throw new E2eSafeException($"Option '{option}' requires a value.");
            }

            if (!values.TryAdd(option, args[index + 1]))
            {
                throw new E2eSafeException($"Option '{option}' cannot be specified more than once.");
            }
        }

        return values;
    }

    private static string ResolveRequiredPath(
        IReadOnlyDictionary<string, string> values,
        string option,
        string environmentVariable,
        bool requireFile)
    {
        var value = GetRequired(values, option, environmentVariable);
        var fullPath = Path.GetFullPath(value);
        if (string.Equals(fullPath, Path.GetPathRoot(fullPath), StringComparison.OrdinalIgnoreCase))
        {
            throw new E2eSafeException($"Option '{option}' cannot target a filesystem root.");
        }

        if (requireFile && !File.Exists(fullPath))
        {
            throw new E2eSafeException($"The file configured by '{option}' does not exist.");
        }

        if (!requireFile && !Directory.Exists(fullPath))
        {
            throw new E2eSafeException($"The directory configured by '{option}' does not exist.");
        }

        return fullPath;
    }

    private static Uri ResolveUri(
        IReadOnlyDictionary<string, string> values,
        string option,
        string environmentVariable)
    {
        var value = GetRequired(values, option, environmentVariable);
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new E2eSafeException(
                $"Option '{option}' must be an HTTP base URI without credentials, query, or fragment.");
        }

        return new Uri(uri.AbsoluteUri.TrimEnd('/') + "/", UriKind.Absolute);
    }

    private static string GetRequired(
        IReadOnlyDictionary<string, string> values,
        string option,
        string? environmentVariable)
    {
        if (values.TryGetValue(option, out var configured) && !string.IsNullOrWhiteSpace(configured))
        {
            return configured.Trim();
        }

        var environmentValue = environmentVariable is null
            ? null
            : Environment.GetEnvironmentVariable(environmentVariable);
        if (string.IsNullOrWhiteSpace(environmentValue))
        {
            throw new E2eSafeException(
                environmentVariable is null
                    ? $"Option '{option}' is required."
                    : $"Option '{option}' or its environment equivalent is required.");
        }

        return environmentValue.Trim();
    }
}
