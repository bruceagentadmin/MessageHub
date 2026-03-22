namespace MessageHub.Core.Models;

public sealed record RecentTargetInfo(
    string Channel,
    string TargetId,
    string? DisplayName,
    DateTimeOffset UpdatedAt);
