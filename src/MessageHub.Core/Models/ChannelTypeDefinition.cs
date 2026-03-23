namespace MessageHub.Core.Models;

/// <summary>
/// 頻道類型定義 — 描述單一頻道類型的識別碼、顯示名稱及所需設定欄位。
/// </summary>
/// <remarks>
/// 此記錄由 <see cref="ChannelConfigFieldDefinition"/> 組成，用於前端動態渲染頻道設定表單。
/// </remarks>
/// <param name="Type">頻道類型的唯一識別字串，例如 <c>telegram</c>、<c>line</c>。</param>
/// <param name="DisplayName">頻道的人類可讀顯示名稱，用於 UI 呈現。</param>
/// <param name="Fields">該頻道類型所需的設定欄位定義清單，參見 <see cref="ChannelConfigFieldDefinition"/>。</param>
public sealed record ChannelTypeDefinition(
    string Type,
    string DisplayName,
    IReadOnlyList<ChannelConfigFieldDefinition> Fields);
