using System.Collections.Generic;

namespace SCRM.SHARED.Models.Dtos
{
    /// <summary>
    /// 朋友圈时间轴传输对象
    /// </summary>
    public class MomentsTimelineDto
    {
        /// <summary>
        /// 设备 UUID，用于前端按设备过滤实时回推。
        /// </summary>
        public string deviceUuid { get; set; } = string.Empty;

        /// <summary>
        /// 朋友圈 ID
        /// </summary>
        public long snsId { get; set; }

        /// <summary>
        /// 发布者用户名 (Wxid)
        /// </summary>
        public string userName { get; set; } = string.Empty;

        /// <summary>
        /// 发布者昵称
        /// </summary>
        public string nickName { get; set; } = string.Empty;

        /// <summary>
        /// 朋友圈内容
        /// </summary>
        public string content { get; set; } = string.Empty;

        /// <summary>
        /// 朋友圈原始扩展 XML。
        /// <para>部分微信版本会把正文或卡片摘要放在 Ext XML 中，前端可用它做展示兜底。</para>
        /// </summary>
        public string xmlContent { get; set; } = string.Empty;

        /// <summary>
        /// 创建时间戳
        /// </summary>
        public long createTime { get; set; }

        /// <summary>
        /// 格式化时间字符串
        /// </summary>
        public string stringTime { get; set; } = string.Empty;

        /// <summary>
        /// 朋友圈类型 (1=文本, 2=图片, 3=视频, 4=链接)
        /// </summary>
        public int type { get; set; }

        /// <summary>
        /// 图片 URL 列表
        /// </summary>
        public List<string> images { get; set; } = new List<string>();

        /// <summary>
        /// 视频 URL
        /// </summary>
        public string videoUrl { get; set; } = string.Empty;

        /// <summary>
        /// 链接详情
        /// </summary>
        public MomentLinkDto link { get; set; } = new();

        /// <summary>
        /// 评论列表
        /// </summary>
        public List<MomentCommentDto> comments { get; set; } = new List<MomentCommentDto>();

        /// <summary>
        /// 点赞列表
        /// </summary>
        public List<MomentLikeDto> likes { get; set; } = new List<MomentLikeDto>();
    }

    /// <summary>
    /// 朋友圈链接详情传输对象
    /// </summary>
    public class MomentLinkDto
    {
        /// <summary>
        /// 链接标题
        /// </summary>
        public string title { get; set; } = string.Empty;

        /// <summary>
        /// 链接 URL
        /// </summary>
        public string url { get; set; } = string.Empty;

        /// <summary>
        /// 链接缩略图 URL
        /// </summary>
        public string thumb { get; set; } = string.Empty;
    }

    /// <summary>
    /// 朋友圈评论传输对象
    /// </summary>
    public class MomentCommentDto
    {
        /// <summary>
        /// 评论 ID
        /// </summary>
        public long commentId { get; set; }

        /// <summary>
        /// 评论者用户名 (Wxid)
        /// </summary>
        public string userName { get; set; } = string.Empty;

        /// <summary>
        /// 评论者昵称
        /// </summary>
        public string nickName { get; set; } = string.Empty;

        /// <summary>
        /// 评论内容
        /// </summary>
        public string content { get; set; } = string.Empty;

        /// <summary>
        /// 评论时间戳
        /// </summary>
        public long createTime { get; set; }

        /// <summary>
        /// 被回复者用户名
        /// </summary>
        public string replyUserName { get; set; } = string.Empty;

        /// <summary>
        /// 被回复者昵称
        /// </summary>
        public string replyNickName { get; set; } = string.Empty;

        /// <summary>
        /// 发布者名称
        /// </summary>
        public string authorName { get; set; } = string.Empty;
    }

    /// <summary>
    /// 朋友圈点赞传输对象
    /// </summary>
    public class MomentLikeDto
    {
        /// <summary>
        /// 点赞者用户名 (Wxid)
        /// </summary>
        public string userName { get; set; } = string.Empty;

        /// <summary>
        /// 点赞者昵称
        /// </summary>
        public string nickName { get; set; } = string.Empty;

        /// <summary>
        /// 点赞时间戳
        /// </summary>
        public long createTime { get; set; }
    }
}
