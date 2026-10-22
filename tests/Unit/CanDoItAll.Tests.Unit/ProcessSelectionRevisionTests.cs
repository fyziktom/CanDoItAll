using CanDoItAll.AgentFramework.Persistence;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class ProcessSelectionRevisionTests {
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Existing_run_preserves_only_an_initialized_unchanged_Process_identity(bool initialized, bool unchanged) {
        var original = Guid.NewGuid().ToString("D");
        var target = unchanged ? original : Guid.NewGuid().ToString("D");

        var revision = FileProcessExecutionIndex.NextSelectionRevision(10, initialized ? 4 : null, original, target);

        Assert.Equal(initialized && unchanged ? 4 : 11, revision);
    }

    [Theory]
    [InlineData(-1L, null)]
    [InlineData(3L, -1L)]
    [InlineData(3L, 4L)]
    public void Invalid_canonical_revisions_cannot_authorize_reuse(long revision, long? selectionRevision) {
        Assert.Throws<InvalidDataException>(() => FileProcessExecutionIndex.NextSelectionRevision(revision, selectionRevision, null, null));
    }

    [Fact]
    public void Exhausted_revision_cannot_wrap_into_an_old_selection_stamp() {
        Assert.Throws<OverflowException>(() => FileProcessExecutionIndex.NextSelectionRevision(long.MaxValue, null, null, null));
        Assert.Throws<OverflowException>(() => FileProcessExecutionIndex.NextSelectionRevision(long.MaxValue, 1, null, null));
    }
}
