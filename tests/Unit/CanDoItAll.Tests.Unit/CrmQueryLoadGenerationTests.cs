using CanDoItAll.Modules.CrmHr.Pages;

namespace CanDoItAll.Tests.Unit;

public sealed class CrmQueryLoadGenerationTests
{
    [Fact]
    public void New_load_supersedes_older_load_and_rejects_a_changed_route()
    {
        var generation = new CrmQueryLoadGeneration();
        var accountA = generation.Begin("account-a:none");
        var accountB = generation.Begin("account-b:none");

        Assert.False(generation.IsCurrent(accountA, "account-a:none"));
        Assert.True(generation.IsCurrent(accountB, "account-b:none"));
        Assert.False(generation.IsCurrent(accountB, "account-c:none"));

        generation.Invalidate();

        Assert.False(generation.IsCurrent(accountB, "account-b:none"));
    }

    [Fact]
    public void Begin_rejects_empty_query_key()
    {
        var generation = new CrmQueryLoadGeneration();

        Assert.Throws<ArgumentException>(() => generation.Begin(" "));
    }
}
