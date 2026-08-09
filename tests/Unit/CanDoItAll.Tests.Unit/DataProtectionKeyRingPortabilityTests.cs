using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.ControlPlane;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CanDoItAll.Tests.Unit;

public sealed class DataProtectionKeyRingPortabilityTests
{
    private const string Sentinel = "dataprotection-sentinel-a04-188b2a";

    [Fact]
    [Trait("Category", "SecretPortability")]
    public void Production_rejects_unprotected_key_ring()
    {
        string root = CreateTempPath();
        try
        {
            IConfiguration configuration = CreateConfiguration("UnprotectedDevelopment");
            var services = new ServiceCollection();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                DataProtectionKeyRingProtection.Configure(
                    services.AddDataProtection(),
                    configuration,
                    isDevelopment: false,
                    root,
                    root,
                    CreateWriter()));

            Assert.Contains("Development", exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(Sentinel, exception.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            DeleteIfExists(root);
        }
    }

    [Fact]
    [Trait("Category", "SecretPortability")]
    public void Development_key_ring_restarts_and_uses_private_repository_modes()
    {
        string root = CreateTempPath();
        try
        {
            IConfiguration configuration = CreateConfiguration("UnprotectedDevelopment");
            string payload;
            using (ServiceProvider provider = CreateProvider(root, configuration, isDevelopment: true))
            {
                payload = provider.GetRequiredService<IDataProtectionProvider>()
                    .CreateProtector("A04.Tests")
                    .Protect(Sentinel);
            }

            using (ServiceProvider restarted = CreateProvider(root, configuration, isDevelopment: true))
            {
                Assert.Equal(
                    Sentinel,
                    restarted.GetRequiredService<IDataProtectionProvider>()
                        .CreateProtector("A04.Tests")
                        .Unprotect(payload));
            }

            Assert.DoesNotContain(
                Sentinel,
                string.Join('\n', Directory.EnumerateFiles(root, "*.xml").Select(File.ReadAllText)),
                StringComparison.Ordinal);
            if (!OperatingSystem.IsWindows())
            {
                Assert.Equal(
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
                    File.GetUnixFileMode(root));
                foreach (string path in Directory.EnumerateFiles(root, "*.xml"))
                {
                    Assert.Equal(
                        UnixFileMode.UserRead | UnixFileMode.UserWrite,
                        File.GetUnixFileMode(path));
                }
            }
        }
        finally
        {
            DeleteIfExists(root);
        }
    }

    [Fact]
    [Trait("Category", "SecretPortability")]
    public void Certificate_protector_reads_legacy_ring_and_encrypts_new_generation()
    {
        string root = CreateTempPath();
        string keyRingPath = Path.Combine(root, "keys");
        string certificatePath = Path.Combine(root, "protector.pfx");
        const string password = "a04-test-certificate-password";
        try
        {
            Directory.CreateDirectory(root);
            WriteTestCertificate(certificatePath, password);
            IConfiguration legacyConfiguration = CreateConfiguration("UnprotectedDevelopment");
            string legacyPayload;
            using (ServiceProvider provider = CreateProvider(
                       keyRingPath,
                       legacyConfiguration,
                       isDevelopment: true))
            {
                legacyPayload = provider.GetRequiredService<IDataProtectionProvider>()
                    .CreateProtector("A04.Legacy")
                    .Protect(Sentinel);
            }

            IConfiguration protectedConfiguration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["DataProtection:KeyProtection:Provider"] = "Certificate",
                    ["DataProtection:KeyProtection:CertificatePath"] = certificatePath,
                    ["DataProtection:KeyProtection:CertificatePasswordEnvironmentVariable"] = "A04_CERT_PASSWORD"
                })
                .Build();
            using ServiceProvider protectedProvider = CreateProvider(
                keyRingPath,
                protectedConfiguration,
                isDevelopment: false,
                variable => variable == "A04_CERT_PASSWORD" ? password : null);

            Assert.Equal(
                Sentinel,
                protectedProvider.GetRequiredService<IDataProtectionProvider>()
                    .CreateProtector("A04.Legacy")
                    .Unprotect(legacyPayload));

            DateTimeOffset now = DateTimeOffset.UtcNow;
            protectedProvider.GetRequiredService<IKeyManager>()
                .CreateNewKey(now - TimeSpan.FromMinutes(1), now + TimeSpan.FromDays(90));
            _ = protectedProvider.GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("A04.Protected")
                .Protect("rotated-value");

            Assert.Contains(
                Directory.EnumerateFiles(keyRingPath, "*.xml").Select(File.ReadAllText),
                xml => xml.Contains("encryptedSecret", StringComparison.Ordinal));
        }
        finally
        {
            DeleteIfExists(root);
        }
    }

    [Fact]
    [Trait("Category", "SecretPortability")]
    public void Windows_auto_protects_key_ring_with_dpapi()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string root = CreateTempPath();
        try
        {
            using ServiceProvider provider = CreateProvider(
                root,
                new ConfigurationBuilder().Build(),
                isDevelopment: false);
            _ = provider.GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("A04.Windows")
                .Protect(Sentinel);

            Assert.All(
                Directory.EnumerateFiles(root, "*.xml"),
                path => Assert.Contains("encryptedSecret", File.ReadAllText(path), StringComparison.Ordinal));
        }
        finally
        {
            DeleteIfExists(root);
        }
    }

    [Fact]
    [Trait("Category", "SecretPortability")]
    public void Private_repository_refuses_to_clobber_existing_generation()
    {
        string root = CreateTempPath();
        try
        {
            var repository = new PrivateDataProtectionKeyRepository(root, CreateWriter());
            repository.StoreElement(new XElement("key", new XAttribute("id", "first")), "same-key");

            Assert.Throws<IOException>(() =>
                repository.StoreElement(new XElement("key", new XAttribute("id", "second")), "same-key"));
            Assert.Equal(
                "first",
                repository.GetAllElements().Single().Attribute("id")?.Value);
        }
        finally
        {
            DeleteIfExists(root);
        }
    }

    [Fact]
    [Trait("Category", "SecretPortability")]
    public void Private_repository_rejects_unsafe_key_names_without_random_fallback()
    {
        string root = CreateTempPath();
        try
        {
            var repository = new PrivateDataProtectionKeyRepository(root, CreateWriter());

            Assert.Throws<InvalidOperationException>(() =>
                repository.StoreElement(new XElement("key"), "../unsafe-key"));
            Assert.False(Directory.Exists(root));
        }
        finally
        {
            DeleteIfExists(root);
        }
    }

    [Fact]
    [Trait("Category", "SecretPortability")]
    public void Private_repository_rejects_foreign_host_and_relative_roots()
    {
        string foreignRoot = OperatingSystem.IsWindows()
            ? "/var/lib/candoitall/dataprotection"
            : @"C:\ProgramData\CanDoItAll\dataprotection";

        Assert.Throws<InvalidOperationException>(() =>
            new PrivateDataProtectionKeyRepository(foreignRoot, CreateWriter()));
        Assert.Throws<ArgumentException>(() =>
            new PrivateDataProtectionKeyRepository("relative/key-ring", CreateWriter()));
    }

    private static ServiceProvider CreateProvider(
        string keyRingPath,
        IConfiguration configuration,
        bool isDevelopment,
        Func<string, string?>? environmentVariableResolver = null)
    {
        var services = new ServiceCollection();
        DataProtectionKeyRingProtection.Configure(
            services.AddDataProtection(),
            configuration,
            isDevelopment,
            Directory.GetCurrentDirectory(),
            keyRingPath,
            CreateWriter(),
            environmentVariableResolver);
        return services.BuildServiceProvider();
    }

    private static IConfiguration CreateConfiguration(string provider)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataProtection:KeyProtection:Provider"] = provider
            })
            .Build();

    private static void WriteTestCertificate(string path, string password)
    {
        using RSA rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=CanDoItAll A04 Test",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        using X509Certificate2 certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow - TimeSpan.FromMinutes(5),
            DateTimeOffset.UtcNow + TimeSpan.FromDays(2));
        File.WriteAllBytes(path, certificate.Export(X509ContentType.Pkcs12, password));
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    private static DurableFileWriter CreateWriter()
        => new(TestWorkspaceServices.PhysicalPathPolicyFactory);

    private static string CreateTempPath()
        => Path.Combine(Path.GetTempPath(), "candoitall-a04-key-ring-tests", Guid.NewGuid().ToString("N"));

    private static void DeleteIfExists(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
