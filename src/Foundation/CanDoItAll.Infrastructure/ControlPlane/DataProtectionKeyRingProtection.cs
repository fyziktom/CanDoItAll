using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using CanDoItAll.Infrastructure.Configuration;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CanDoItAll.Infrastructure.ControlPlane;

public static class DataProtectionKeyRingProtection
{
    public const string ApplicationName = "CanDoItAll";

    public static IDataProtectionBuilder Configure(
        IDataProtectionBuilder builder,
        IConfiguration configuration,
        IHostEnvironment environment,
        string keysPath,
        DurableFileWriter durableFileWriter,
        Func<string, string?>? environmentVariableResolver = null)
    {
        ArgumentNullException.ThrowIfNull(environment);
        return Configure(
            builder,
            configuration,
            environment.IsDevelopment(),
            environment.ContentRootPath,
            keysPath,
            durableFileWriter,
            environmentVariableResolver);
    }

    public static IDataProtectionBuilder Configure(
        IDataProtectionBuilder builder,
        IConfiguration configuration,
        bool isDevelopment,
        string contentRootPath,
        string keysPath,
        DurableFileWriter durableFileWriter,
        Func<string, string?>? environmentVariableResolver = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(keysPath);
        ArgumentNullException.ThrowIfNull(durableFileWriter);

        durableFileWriter.EnsureDirectory(keysPath, keysPath, requirePrivateUnixMode: true);
        builder.SetApplicationName(ApplicationName);
        builder.Services.AddOptions<KeyManagementOptions>().Configure(options =>
            options.XmlRepository = new PrivateDataProtectionKeyRepository(keysPath, durableFileWriter));

        DataProtectionKeyProtectionOptions options =
            configuration.GetSection(DataProtectionKeyProtectionOptions.SectionName)
                .Get<DataProtectionKeyProtectionOptions>() ?? new DataProtectionKeyProtectionOptions();
        DataProtectionKeyProtectionProvider provider = options.Provider == DataProtectionKeyProtectionProvider.Auto
            ? ResolveAutomaticProvider()
            : options.Provider;

        return provider switch
        {
            DataProtectionKeyProtectionProvider.Dpapi => ConfigureDpapi(builder),
            DataProtectionKeyProtectionProvider.Certificate => ConfigureCertificate(
                builder,
                options,
                contentRootPath,
                environmentVariableResolver ?? Environment.GetEnvironmentVariable),
            DataProtectionKeyProtectionProvider.UnprotectedDevelopment => isDevelopment
                ? builder
                : throw new InvalidOperationException(
                    "Unprotected Data Protection keys are allowed only in an explicitly configured Development environment."),
            _ => throw new InvalidOperationException(
                $"Data Protection key protection provider '{provider}' is not supported.")
        };
    }

    private static DataProtectionKeyProtectionProvider ResolveAutomaticProvider()
    {
        if (OperatingSystem.IsWindows())
        {
            return DataProtectionKeyProtectionProvider.Dpapi;
        }

        throw new InvalidOperationException(
            "Unix startup requires an explicit certificate-backed Data Protection key protector. " +
            "Development may explicitly select UnprotectedDevelopment.");
    }

    private static IDataProtectionBuilder ConfigureDpapi(IDataProtectionBuilder builder)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("DPAPI key-ring protection is supported only on Windows.");
        }

        return builder.ProtectKeysWithDpapi();
    }

    private static IDataProtectionBuilder ConfigureCertificate(
        IDataProtectionBuilder builder,
        DataProtectionKeyProtectionOptions options,
        string contentRootPath,
        Func<string, string?> environmentVariableResolver)
    {
        if (string.IsNullOrWhiteSpace(options.CertificatePath))
        {
            throw new InvalidOperationException(
                "Certificate Data Protection requires DataProtection:KeyProtection:CertificatePath.");
        }

        string certificatePath = ControlPlanePathDefaults.ResolveConfiguredPath(
            contentRootPath,
            options.CertificatePath);
        string? password = ResolveCertificatePassword(options, environmentVariableResolver);
        X509Certificate2 currentCertificate = LoadCertificate(certificatePath, password);
        if (!currentCertificate.HasPrivateKey)
        {
            currentCertificate.Dispose();
            throw new InvalidOperationException(
                "The configured Data Protection certificate does not contain a private key.");
        }

        builder.ProtectKeysWithCertificate(currentCertificate);
        X509Certificate2[] previousCertificates = options.PreviousCertificatePaths
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(path => LoadCertificate(
                ControlPlanePathDefaults.ResolveConfiguredPath(contentRootPath, path),
                password))
            .ToArray();
        if (previousCertificates.Any(static certificate => !certificate.HasPrivateKey))
        {
            foreach (X509Certificate2 certificate in previousCertificates)
            {
                certificate.Dispose();
            }

            throw new InvalidOperationException(
                "Every previous Data Protection certificate must contain a private key.");
        }

        if (previousCertificates.Length > 0)
        {
            builder.UnprotectKeysWithAnyCertificate(previousCertificates);
        }

        return builder;
    }

    private static string? ResolveCertificatePassword(
        DataProtectionKeyProtectionOptions options,
        Func<string, string?> environmentVariableResolver)
    {
        if (string.IsNullOrWhiteSpace(options.CertificatePasswordEnvironmentVariable))
        {
            return null;
        }

        string variableName = options.CertificatePasswordEnvironmentVariable.Trim();
        if (!Regex.IsMatch(
                variableName,
                "^[a-zA-Z_][a-zA-Z0-9_]*$",
                RegexOptions.CultureInvariant | RegexOptions.NonBacktracking))
        {
            throw new InvalidOperationException(
                "The Data Protection certificate password environment-variable name is invalid.");
        }

        string? password = environmentVariableResolver(variableName);
        if (string.IsNullOrEmpty(password))
        {
            throw new InvalidOperationException(
                "The configured Data Protection certificate password was not supplied by the startup environment.");
        }

        return password;
    }

    private static X509Certificate2 LoadCertificate(string path, string? password)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                "The configured Data Protection certificate file was not found.",
                path);
        }

        if (!OperatingSystem.IsWindows())
        {
            const UnixFileMode sharedAccess =
                UnixFileMode.GroupRead |
                UnixFileMode.GroupWrite |
                UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead |
                UnixFileMode.OtherWrite |
                UnixFileMode.OtherExecute;
            if ((File.GetUnixFileMode(path) & sharedAccess) != 0)
            {
                throw new InvalidOperationException(
                    "The Data Protection certificate file must not grant group or other access.");
            }
        }

        return X509CertificateLoader.LoadPkcs12FromFile(
            path,
            password,
            X509KeyStorageFlags.EphemeralKeySet);
    }
}

public sealed class PrivateDataProtectionKeyRepository : IXmlRepository
{
    private static readonly Regex SafeNamePattern = new(
        "^[a-zA-Z0-9][a-zA-Z0-9._-]{0,127}$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private readonly string rootPath;
    private readonly DurableFileWriter durableFileWriter;

    public PrivateDataProtectionKeyRepository(
        string rootPath,
        DurableFileWriter durableFileWriter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        PhysicalPathSyntaxPolicy.EnsureNativeOrRelative(rootPath, "Data Protection key-ring root");
        if (!Path.IsPathRooted(rootPath))
        {
            throw new ArgumentException(
                "The Data Protection key-ring root must be an absolute native-host path.",
                nameof(rootPath));
        }

        this.rootPath = Path.GetFullPath(rootPath);
        this.durableFileWriter = durableFileWriter ?? throw new ArgumentNullException(nameof(durableFileWriter));
    }

    public IReadOnlyCollection<XElement> GetAllElements()
    {
        durableFileWriter.EnsureDirectory(rootPath, rootPath, requirePrivateUnixMode: true);
        var elements = new List<XElement>();
        foreach (string path in Directory.EnumerateFiles(rootPath, "*.xml", SearchOption.TopDirectoryOnly)
                     .OrderBy(static path => path, StringComparer.Ordinal))
        {
            durableFileWriter.HardenPrivateFile(rootPath, path);
            using FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            elements.Add(XElement.Load(stream, LoadOptions.PreserveWhitespace));
        }

        return elements;
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        ArgumentNullException.ThrowIfNull(element);
        string candidateName = friendlyName ?? string.Empty;
        if (!SafeNamePattern.IsMatch(candidateName))
        {
            throw new InvalidOperationException(
                "The Data Protection key name is not safe for private file persistence.");
        }

        durableFileWriter.WriteText(
            rootPath,
            Path.Combine(rootPath, candidateName + ".xml"),
            element.ToString(SaveOptions.DisableFormatting),
            DurableFileWriteOptions.Private with
            {
                CommitMode = DurableFileCommitMode.CreateNew
            });
    }
}
