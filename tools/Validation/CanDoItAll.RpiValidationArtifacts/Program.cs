using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: CanDoItAll.RpiValidationArtifacts <deploy-root> <ip-address>");
    return 1;
}

var deployRoot = Path.GetFullPath(args[0]);
var ipAddress = IPAddress.Parse(args[1]);
var certificateDirectory = Path.Combine(deployRoot, "certs");
var ipfsDirectory = Path.Combine(deployRoot, "ipfs");

Directory.CreateDirectory(certificateDirectory);
Directory.CreateDirectory(ipfsDirectory);

using var rsa = RSA.Create(2048);
var request = new CertificateRequest(
    $"CN={ipAddress}",
    rsa,
    HashAlgorithmName.SHA256,
    RSASignaturePadding.Pkcs1);

var subjectAlternativeNames = new SubjectAlternativeNameBuilder();
subjectAlternativeNames.AddIpAddress(ipAddress);
request.CertificateExtensions.Add(subjectAlternativeNames.Build());
request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
request.CertificateExtensions.Add(new X509KeyUsageExtension(
    X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
    critical: false));

var enhancedKeyUsages = new OidCollection
{
    new("1.3.6.1.5.5.7.3.1")
};

request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(enhancedKeyUsages, critical: false));

using var certificate = request.CreateSelfSigned(
    DateTimeOffset.UtcNow.AddDays(-1),
    DateTimeOffset.UtcNow.AddYears(2));

await File.WriteAllTextAsync(
    Path.Combine(certificateDirectory, "candoitall-rpi.crt.pem"),
    certificate.ExportCertificatePem());

await File.WriteAllTextAsync(
    Path.Combine(certificateDirectory, "candoitall-rpi.key.pem"),
    rsa.ExportPkcs8PrivateKeyPem());

var swarmKey = $"""
/key/swarm/psk/1.0.0/
/base16/
{Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant()}
""";

await File.WriteAllTextAsync(
    Path.Combine(ipfsDirectory, "swarm.key"),
    swarmKey + Environment.NewLine);

Console.WriteLine($"Artifacts written to {deployRoot}");
return 0;
