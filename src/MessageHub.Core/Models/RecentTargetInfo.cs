namespace MessageHub.Core;

public sealed record RecentTargetInfo(
    string Channel,
    string TargetId,
    string? DisplayName,
    DateTimeOffset UpdatedAt);
