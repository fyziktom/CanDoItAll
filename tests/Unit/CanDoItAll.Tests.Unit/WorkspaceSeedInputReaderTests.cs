using System.Text;
using CanDoItAll.AgentFramework.Persistence;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class WorkspaceSeedInputReaderTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReadText_HashesTheDecodedBytesIncludingBom(bool unicode) {
        var encoding = unicode ? Encoding.Unicode : Encoding.UTF8;
        var payload = encoding.GetPreamble().Concat(encoding.GetBytes("Template Ž")).ToArray();
        var reader = new WorkspaceSeedInputReader(() => new("capabilities", "agents"), _ => new MemoryStream(payload));

        Assert.Equal("Template Ž", reader.ReadText(Path.GetFullPath("template")));
        Assert.True(reader.IsCurrent());
        payload[^1] ^= 1;
        Assert.False(reader.IsCurrent());
    }

    [Fact]
    public void IsCurrent_RejectsChangedRootsEvenWhenOldFilesStillMatch() {
        var roots = new WorkspaceSeedRoots("first", "agents");
        var reader = new WorkspaceSeedInputReader(() => roots, _ => new MemoryStream("same"u8.ToArray()));
        reader.ReadText(Path.GetFullPath("template"));
        Assert.True(reader.IsCurrent());
        roots = roots with { Capabilities = "second" };
        Assert.False(reader.IsCurrent());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IsCurrent_UnavailableInputCannotAuthorizeReuseAndRepairCanBeRead(bool denied) {
        var unavailable = false;
        var reader = new WorkspaceSeedInputReader(() => new("capabilities", "agents"), _ => {
            if (unavailable) {
                throw denied ? new UnauthorizedAccessException() : new FileNotFoundException();
            }
            return new MemoryStream("same"u8.ToArray());
        });
        var path = Path.GetFullPath("template");
        reader.ReadText(path);
        unavailable = true;
        Assert.False(reader.IsCurrent());
        Assert.ThrowsAny<Exception>(() => reader.ReadText(path));
        unavailable = false;
        Assert.Equal("same", reader.ReadText(path));
        Assert.True(reader.IsCurrent());
    }

    [Theory]
    [InlineData(1, 100)]
    [InlineData(10, 5)]
    public void ReadText_OversizedWitnessStillReadsButNeverReusesPartialInputs(int fileLimit, long byteLimit) {
        var reader = new WorkspaceSeedInputReader(() => new("capabilities", "agents"),
            _ => new MemoryStream("same"u8.ToArray()), fileLimit, byteLimit);
        Assert.Equal("same", reader.ReadText(Path.GetFullPath("first")));
        Assert.Equal("same", reader.ReadText(Path.GetFullPath("second")));
        Assert.False(reader.IsCurrent());
    }

    [Fact]
    public void ReadText_ChangingDuplicateInputCannotProduceReusableMixedBuild() {
        var text = "first";
        var reader = new WorkspaceSeedInputReader(() => new("capabilities", "agents"),
            _ => new MemoryStream(Encoding.UTF8.GetBytes(text)));
        var path = Path.GetFullPath("template");
        reader.ReadText(path);
        text = "other";
        reader.ReadText(path);
        Assert.False(reader.IsCurrent());
    }
}
