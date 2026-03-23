namespace MessageHub.Core.Models;

/// <summary>
/// 頻道設定欄位定義 — 描述頻道類型所需的單一設定欄位的中繼資料，用於前端動態渲染設定表單。
/// </summary>
/// <remarks>
/// 作為 <see cref="ChannelTypeDefinition.Fields"/> 清單的元素，
/// 前端可依據此定義自動生成對應的輸入框，包含標籤文字、佔位字元與必填驗證。
/// 若 <see cref="Secret"/> 為 <see langword="true"/>，前端應使用密碼輸入框（遮罩顯示）。
/// </remarks>
/// <param name="Key">欄位的唯一識別鍵，對應 <see cref="ChannelSettings.Parameters"/> 字典的鍵名。</param>
/// <param name="Label">欄位在 UI 上顯示的標籤文字。</param>
/// <param name="Placeholder">輸入框的佔位提示文字，引導使用者填入正確格式的值。</param>
/// <param name="Required">此欄位是否為必填；<see langword="true"/> 表示儲存設定前必須提供此值。</param>
/// <param name="Secret">此欄位是否為機密資訊（如 Token、密碼）；<see langword="true"/> 時前端應遮罩顯示。</param>
public sealed record ChannelConfigFieldDefinition(
    string Key,
    string Label,
    string Placeholder,
    bool Required,
    bool Secret = false);
