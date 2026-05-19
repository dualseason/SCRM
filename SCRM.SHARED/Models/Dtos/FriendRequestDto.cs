using System;

namespace SCRM.SHARED.Models.Dtos
{
    /// <summary>
    /// 好友请求展示 DTO。
    /// <para>用于 Web 好友请求页展示 FriendRequests 表中的待处理/已处理记录。</para>
    /// </summary>
    public class FriendRequestDto
    {
        /// <summary>好友请求自增 ID。</summary>
        public int id { get; set; }

        /// <summary>所属微信账号 wxid。</summary>
        public string ownerWxid { get; set; } = string.Empty;

        /// <summary>所属微信账号数值键。</summary>
        public long wechatAccountId { get; set; }

        /// <summary>请求人 wxid。</summary>
        public string requestWxid { get; set; } = string.Empty;

        /// <summary>请求人昵称。</summary>
        public string nickname { get; set; } = string.Empty;

        /// <summary>头像地址。</summary>
        public string avatar { get; set; } = string.Empty;

        /// <summary>性别。</summary>
        public int? gender { get; set; }

        /// <summary>地区。</summary>
        public string region { get; set; } = string.Empty;

        /// <summary>请求来源。</summary>
        public string source { get; set; } = string.Empty;

        /// <summary>请求留言。</summary>
        public string requestMessage { get; set; } = string.Empty;

        /// <summary>状态：0 待处理，1 已通过，2 已拒绝，3 已忽略。</summary>
        public int status { get; set; }

        /// <summary>请求时间。</summary>
        public DateTime requestTime { get; set; }

        /// <summary>响应时间。</summary>
        public DateTime responseTime { get; set; }

        /// <summary>响应留言。</summary>
        public string responseMessage { get; set; } = string.Empty;

        /// <summary>创建时间。</summary>
        public DateTime createdAt { get; set; }

        /// <summary>更新时间。</summary>
        public DateTime updatedAt { get; set; }
    }
}
