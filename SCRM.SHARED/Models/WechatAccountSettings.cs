using System;

namespace SCRM.SHARED.Models
{
    public class WechatAccountSettings
    {
        /// <summary>
        /// 是否自动抢红包
        /// </summary>
        public bool AutoAcceptLuckyMoney { get; set; } = false;

        /// <summary>
        /// 是否自动通过好友请求
        /// </summary>
        public bool AutoAcceptFriendRequest { get; set; } = false;

        /// <summary>
        /// 自动回复消息（为空则不回复）
        /// </summary>
        public string? AutoReplyContent { get; set; }

        /// <summary>
        /// 是否开启朋友圈自动点赞
        /// </summary>
        public bool AutoLikeMoments { get; set; } = false;
    }
}
