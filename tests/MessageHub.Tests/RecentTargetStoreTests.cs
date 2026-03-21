using MessageHub.Infrastructure;

namespace MessageHub.Tests;

public class RecentTargetStoreTests
{
    [Fact]
    public async Task SetLastTargetAsync_AndGetLastTargetAsync_ShouldStorePerChannel()
    {
        var store = new RecentTargetStore();

        await store.SetLastTargetAsync("telegram", "chat-1", "Bruce");
        await store.SetLastTargetAsync("line", "user-2", "Boss");

        var telegram = await store.GetLastTargetAsync("telegram");
        var line = await store.GetLastTargetAsync("line");
        var missing = await store.GetLastTargetAsync("email");

        Assert.Equal("chat-1", telegram?.TargetId);
        Assert.Equal("Bruce", telegram?.DisplayName);
        Assert.Equal("user-2", line?.TargetId);
        Assert.Null(missing);
    }
}
