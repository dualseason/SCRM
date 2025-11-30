namespace SCRM.API.Models.Entities;

/// <summary>
/// 灏忕▼搴忚处鍙疯〃
/// </summary>
public class MiniprogramAccount
{
    public int Id { get; set; }
    public int WechatAccountId { get; set; }
    public string AppId { get; set; }
    public string AppName { get; set; }
    public string Avatar { get; set; }
    public string Description { get; set; }
    public int AccountType { get; set; }
    public int Status { get; set; }
    public int AccessCount { get; set; }
    public DateTime LastAccessTime { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
}

