using System;
using System.Collections.Generic;

namespace SCRM.SHARED.Models.Dtos
{
    /// <summary>
    /// 视频号简表对象。
    /// </summary>
    public class FinderBriefDto
    {
        /// <summary>
        /// 视频号 FeedId。
        /// </summary>
        public long feedId { get; set; }

        /// <summary>
        /// 视频号作者用户名。
        /// </summary>
        public string userName { get; set; } = string.Empty;

        /// <summary>
        /// 视频号描述文本。
        /// </summary>
        public string desc { get; set; } = string.Empty;

        /// <summary>
        /// 视频号封面/缩略图。
        /// </summary>
        public string thumb { get; set; } = string.Empty;

        /// <summary>
        /// 视频号 NonceId。
        /// </summary>
        public string nonceId { get; set; } = string.Empty;

        /// <summary>
        /// 视频号对象类型。
        /// </summary>
        public long type { get; set; }
    }

    /// <summary>
    /// 视频号提及条目。
    /// </summary>
    public class FinderMentionItemDto
    {
        /// <summary>
        /// 关联的视频号条目。
        /// </summary>
        public FinderBriefDto sphItem { get; set; } = new();

        /// <summary>
        /// 提及事件主键。
        /// </summary>
        public long id { get; set; }

        /// <summary>
        /// 提及类型。
        /// </summary>
        public int type { get; set; }

        /// <summary>
        /// 提及业务 ID。
        /// </summary>
        public long mentionId { get; set; }

        /// <summary>
        /// 关联评论 ID。
        /// </summary>
        public long commentId { get; set; }

        /// <summary>
        /// 操作者用户名。
        /// </summary>
        public string userName { get; set; } = string.Empty;

        /// <summary>
        /// 操作者昵称。
        /// </summary>
        public string nickName { get; set; } = string.Empty;

        /// <summary>
        /// 操作者头像。
        /// </summary>
        public string avatar { get; set; } = string.Empty;

        /// <summary>
        /// 提及内容。
        /// </summary>
        public string content { get; set; } = string.Empty;

        /// <summary>
        /// 内容类型。
        /// </summary>
        public int contentType { get; set; }

        /// <summary>
        /// 创建时间（Unix 秒）。
        /// </summary>
        public int createTime { get; set; }

        /// <summary>
        /// 被回复用户名。
        /// </summary>
        public string replayUsername { get; set; } = string.Empty;

        /// <summary>
        /// 被回复昵称。
        /// </summary>
        public string replayNickname { get; set; } = string.Empty;

        /// <summary>
        /// 根评论 ID。
        /// </summary>
        public long rootCommentId { get; set; }

        /// <summary>
        /// 引用内容。
        /// </summary>
        public string refContent { get; set; } = string.Empty;

        /// <summary>
        /// 粉丝关系 ID。
        /// </summary>
        public long fansId { get; set; }

        /// <summary>
        /// 关注关系 ID。
        /// </summary>
        public long followId { get; set; }

        /// <summary>
        /// 关系类型。
        /// </summary>
        public int relationType { get; set; }
    }

    /// <summary>
    /// 视频号提及结果通知。
    /// </summary>
    public class FinderMentionNoticeDto
    {
        /// <summary>
        /// 对应设备 UUID。
        /// </summary>
        public string deviceUuid { get; set; } = string.Empty;

        /// <summary>
        /// 对应微信号。
        /// </summary>
        public string weChatId { get; set; } = string.Empty;

        /// <summary>
        /// 对应视频号用户名。
        /// </summary>
        public string sphUserName { get; set; } = string.Empty;

        /// <summary>
        /// 任务是否成功。
        /// </summary>
        public bool success { get; set; }

        /// <summary>
        /// 错误信息。
        /// </summary>
        public string errMsg { get; set; } = string.Empty;

        /// <summary>
        /// 任务 ID。
        /// </summary>
        public long taskId { get; set; }

        /// <summary>
        /// 回包接收时间。
        /// </summary>
        public DateTimeOffset receivedAt { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// 点赞提及列表。
        /// </summary>
        public List<FinderMentionItemDto> likeList { get; set; } = new();

        /// <summary>
        /// 评论提及列表。
        /// </summary>
        public List<FinderMentionItemDto> commentList { get; set; } = new();

        /// <summary>
        /// 关注提及列表。
        /// </summary>
        public List<FinderMentionItemDto> followList { get; set; } = new();
    }

    /// <summary>
    /// 视频号用户页条目。
    /// </summary>
    public class FinderUserPageItemDto
    {
        /// <summary>
        /// 视频号 FeedId。
        /// </summary>
        public long feedId { get; set; }

        /// <summary>
        /// 视频号作者用户名。
        /// </summary>
        public string userName { get; set; } = string.Empty;

        /// <summary>
        /// 文案描述。
        /// </summary>
        public string desc { get; set; } = string.Empty;

        /// <summary>
        /// 封面图。
        /// </summary>
        public string thumb { get; set; } = string.Empty;

        /// <summary>
        /// 视频号 NonceId。
        /// </summary>
        public string nonceId { get; set; } = string.Empty;

        /// <summary>
        /// 对象类型。
        /// </summary>
        public long type { get; set; }

        /// <summary>
        /// 发布时间戳（Unix 秒）。
        /// </summary>
        public long timestamp { get; set; }

        /// <summary>
        /// 阅读数。
        /// </summary>
        public int readCnt { get; set; }

        /// <summary>
        /// 点赞数。
        /// </summary>
        public int likeCnt { get; set; }

        /// <summary>
        /// 评论数。
        /// </summary>
        public int commentCnt { get; set; }

        /// <summary>
        /// 收藏数。
        /// </summary>
        public int favCnt { get; set; }

        /// <summary>
        /// 转发数。
        /// </summary>
        public int forwardCnt { get; set; }
    }

    /// <summary>
    /// 视频号用户页结果。
    /// </summary>
    public class FinderUserPageDto
    {
        /// <summary>
        /// 对应设备 UUID。
        /// </summary>
        public string deviceUuid { get; set; } = string.Empty;

        /// <summary>
        /// 对应微信号。
        /// </summary>
        public string weChatId { get; set; } = string.Empty;

        /// <summary>
        /// 视频号用户名。
        /// </summary>
        public string userName { get; set; } = string.Empty;

        /// <summary>
        /// 视频号昵称。
        /// </summary>
        public string nickName { get; set; } = string.Empty;

        /// <summary>
        /// 头像。
        /// </summary>
        public string avatar { get; set; } = string.Empty;

        /// <summary>
        /// 个性签名。
        /// </summary>
        public string signature { get; set; } = string.Empty;

        /// <summary>
        /// 性别枚举值。
        /// </summary>
        public int gender { get; set; }

        /// <summary>
        /// 省份。
        /// </summary>
        public string province { get; set; } = string.Empty;

        /// <summary>
        /// 城市。
        /// </summary>
        public string city { get; set; } = string.Empty;

        /// <summary>
        /// 拉取是否成功。
        /// </summary>
        public bool success { get; set; }

        /// <summary>
        /// 错误信息。
        /// </summary>
        public string errMsg { get; set; } = string.Empty;

        /// <summary>
        /// 任务 ID。
        /// </summary>
        public long taskId { get; set; }

        /// <summary>
        /// 回包接收时间。
        /// </summary>
        public DateTimeOffset receivedAt { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// 用户页作品列表。
        /// </summary>
        public List<FinderUserPageItemDto> sphList { get; set; } = new();
    }

    /// <summary>
    /// 视频号评论条目。
    /// </summary>
    public class FinderCommentItemDto
    {
        /// <summary>
        /// 评论 ID。
        /// </summary>
        public long commentId { get; set; }

        /// <summary>
        /// 评论类型。
        /// </summary>
        public int type { get; set; }

        /// <summary>
        /// 视频号 FeedId。
        /// </summary>
        public long feedId { get; set; }

        /// <summary>
        /// 评论用户名。
        /// </summary>
        public string userName { get; set; } = string.Empty;

        /// <summary>
        /// 评论昵称。
        /// </summary>
        public string nickName { get; set; } = string.Empty;

        /// <summary>
        /// 评论头像。
        /// </summary>
        public string avatar { get; set; } = string.Empty;

        /// <summary>
        /// 评论内容。
        /// </summary>
        public string content { get; set; } = string.Empty;

        /// <summary>
        /// 评论内容类型。
        /// </summary>
        public int contentType { get; set; }

        /// <summary>
        /// 评论创建时间（Unix 秒）。
        /// </summary>
        public long createTime { get; set; }

        /// <summary>
        /// 点赞数。
        /// </summary>
        public int likeCount { get; set; }

        /// <summary>
        /// 回复评论 ID。
        /// </summary>
        public long replyId { get; set; }
    }

    /// <summary>
    /// 视频号评论列表结果。
    /// </summary>
    public class FinderCommentListDto
    {
        /// <summary>
        /// 对应设备 UUID。
        /// </summary>
        public string deviceUuid { get; set; } = string.Empty;

        /// <summary>
        /// 对应微信号。
        /// </summary>
        public string weChatId { get; set; } = string.Empty;

        /// <summary>
        /// 对应视频号用户名。
        /// </summary>
        public string sphUserName { get; set; } = string.Empty;

        /// <summary>
        /// 任务是否成功。
        /// </summary>
        public bool success { get; set; }

        /// <summary>
        /// 错误信息。
        /// </summary>
        public string errMsg { get; set; } = string.Empty;

        /// <summary>
        /// 任务 ID。
        /// </summary>
        public long taskId { get; set; }

        /// <summary>
        /// 回包接收时间。
        /// </summary>
        public DateTimeOffset receivedAt { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// 评论列表。
        /// </summary>
        public List<FinderCommentItemDto> commentList { get; set; } = new();
    }
}
