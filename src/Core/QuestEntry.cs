using System.Text.Json.Serialization;

namespace FragmentOfJapanese.Core;

/// <summary>Phần thưởng khi hoàn thành nhiệm vụ.</summary>
public class QuestReward
{
    [JsonPropertyName("exp")]        public int    Exp       { get; set; }
    [JsonPropertyName("gold")]       public int    Gold      { get; set; }
    [JsonPropertyName("ma_thach")]   public int    MaThach   { get; set; }
    [JsonPropertyName("item_id")]    public string ItemId    { get; set; } = "";
    [JsonPropertyName("item_count")] public int    ItemCount { get; set; }
}

/// <summary>
/// Định nghĩa một nhiệm vụ. period = daily|weekly|once; type = loại mục tiêu
/// (learn/battle/voice/quiz...) do hệ thống khác báo qua QuestManager.Report(type, key).
/// target_key rỗng = áp dụng cho mọi key (vd mọi loại quái); có giá trị = chỉ tính đúng key đó.
/// </summary>
public class QuestEntry
{
    [JsonPropertyName("id")]             public string Id            { get; set; } = "";
    [JsonPropertyName("name_vi")]        public string NameVi        { get; set; } = "";
    [JsonPropertyName("name_en")]        public string NameEn        { get; set; } = "";
    [JsonPropertyName("name_ja")]        public string NameJa        { get; set; } = "";
    [JsonPropertyName("description_vi")] public string DescriptionVi { get; set; } = "";
    [JsonPropertyName("period")]         public string Period        { get; set; } = "daily";
    [JsonPropertyName("type")]           public string Type          { get; set; } = "";
    [JsonPropertyName("target")]         public int    Target        { get; set; } = 1;
    [JsonPropertyName("target_key")]     public string TargetKey     { get; set; } = "";
    [JsonPropertyName("reward")]         public QuestReward Reward    { get; set; } = new();
}
