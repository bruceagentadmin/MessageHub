namespace MessageHub.Domain.Models;

/// <summary>
/// 聯絡人實體 — 記錄曾與系統互動過的使用者資訊。
/// 每個聯絡人以 (Channel, PlatformUserId) 為唯一識別。
/// </summary>
public sealed record Contact(
    Guid Id,
    string Channel,
    string PlatformUserId,
    string? DisplayName,
    string? ChatId,
    DateTimeOffset FirstSeenAt,
    DateTimeOffset LastSeenAt,
    int MessageCount);
