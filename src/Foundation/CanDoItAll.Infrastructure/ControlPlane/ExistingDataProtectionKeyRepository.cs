using System.Xml;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.Repositories;

namespace CanDoItAll.Infrastructure.ControlPlane;

internal sealed class ExistingDataProtectionKeyRepository(string root) : IXmlRepository {
    private const int MaximumKeyCount = 128;
    private const int MaximumKeyBytes = 64 * 1024;

    public IReadOnlyCollection<XElement> GetAllElements() {
        ExistingPrivateFileReader.RequireDirectory(root, root);
        var elements = new List<XElement>();
        foreach (string path in Directory.EnumerateFiles(root, "*.xml", SearchOption.TopDirectoryOnly)) {
            if (elements.Count == MaximumKeyCount) {
                throw new InvalidDataException("The existing key repository exceeds its inspection bound.");
            }
            using var stream = new MemoryStream(ExistingPrivateFileReader.Read(root, path, MaximumKeyBytes), writable: false);
            using var reader = XmlReader.Create(stream, new XmlReaderSettings {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = MaximumKeyBytes
            });
            elements.Add(XElement.Load(reader, LoadOptions.PreserveWhitespace));
        }
        return elements;
    }

    public void StoreElement(XElement element, string friendlyName)
        => throw new NotSupportedException("Read-only recovery cannot create or update Data Protection keys.");
}
