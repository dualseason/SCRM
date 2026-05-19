using System;
using System.Collections.Generic;

namespace SCRM.SHARED.Models.Dtos
{
    /// <summary>
    /// 朋友圈发布任务请求。
    /// <para>对齐 62203/SmRun 已支持的 PostSNSNewsTask 高级协议；旧的正文+图片入口会转换为该模型。</para>
    /// </summary>
    public sealed class MomentPostRequestDto
    {
        /// <summary>
        /// 前端请求级幂等标识。
        /// <para>服务端会随发圈任务上下文保留该值，用于超时、迟到回包、重试和页面状态对账。</para>
        /// </summary>
        public string clientRequestId { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>
        /// 指定微信账号；为空时 Android 端按当前登录账号处理。
        /// </summary>
        public string weChatId { get; set; } = string.Empty;

        /// <summary>
        /// 朋友圈正文。
        /// </summary>
        public string content { get; set; } = string.Empty;

        /// <summary>
        /// 附件配置。
        /// </summary>
        public MomentPostAttachmentDto attachment { get; set; } = new();

        /// <summary>
        /// 首条自动评论。
        /// </summary>
        public string comment { get; set; } = string.Empty;

        /// <summary>
        /// 慢速发布实验开关。
        /// <para>62203/SmRun 会透传该字段；实际微信端消费语义仍需实机验证。</para>
        /// </summary>
        public bool sendSlow { get; set; }

        /// <summary>
        /// 可见范围配置。
        /// </summary>
        public MomentPostVisibleDto visible { get; set; } = new();

        /// <summary>
        /// 地理位置配置。
        /// </summary>
        public MomentPostPoiDto poi { get; set; } = new();

        /// <summary>
        /// 额外自动评论列表。
        /// </summary>
        public List<string> extComment { get; set; } = new();

        /// <summary>
        /// 提醒谁看，通常为 wxid/username 列表。
        /// </summary>
        public List<string> notiUsers { get; set; } = new();

        /// <summary>
        /// 从旧的正文+图片 URL 入参构造高级协议请求。
        /// </summary>
        public static MomentPostRequestDto FromLegacy(string content, IEnumerable<string>? imageUrls)
        {
            var request = new MomentPostRequestDto
            {
                clientRequestId = Guid.NewGuid().ToString("N"),
                content = content ?? string.Empty,
                attachment = new MomentPostAttachmentDto
                {
                    type = MomentPostAttachmentType.Picture,
                    content = NormalizeList(imageUrls)
                }
            };

            return request;
        }

        /// <summary>
        /// 归一化字符串列表，去空白、去重并保留原始顺序。
        /// </summary>
        internal static List<string> NormalizeList(IEnumerable<string>? values)
        {
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (values == null)
            {
                return result;
            }

            foreach (var value in values)
            {
                var normalized = value?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(normalized) || !seen.Add(normalized))
                {
                    continue;
                }

                result.Add(normalized);
            }

            return result;
        }
    }

    /// <summary>
    /// 朋友圈附件类型，数值与 PostSNSNewsTask.AttachmentMessage.EnumAttachType 保持一致。
    /// </summary>
    public enum MomentPostAttachmentType
    {
        Link = 0,
        Picture = 2,
        ShortVideo = 3,
        LongVideo = 4,
        ShiPinHao = 5,
        ExtLink = 6,
        FinderLive = 7
    }

    /// <summary>
    /// 朋友圈附件配置。
    /// </summary>
    public sealed class MomentPostAttachmentDto
    {
        /// <summary>
        /// 附件类型。
        /// </summary>
        public MomentPostAttachmentType type { get; set; } = MomentPostAttachmentType.Picture;

        /// <summary>
        /// 附件内容列表。
        /// <para>图片/视频通常是路径或 URL；链接/视频号/直播卡片可按 Android 侧约定传结构化内容。</para>
        /// </summary>
        public List<string> content { get; set; } = new();
    }

    /// <summary>
    /// 朋友圈可见范围类型，数值与 PostSNSNewsTask.VisibleMessage.EnumVisibleType 保持一致。
    /// </summary>
    public enum MomentPostVisibleType
    {
        Public = 0,
        Private = 1,
        WhoVisible = 2,
        WhoInvisible = 3
    }

    /// <summary>
    /// 朋友圈可见范围配置。
    /// </summary>
    public sealed class MomentPostVisibleDto
    {
        /// <summary>
        /// 可见范围类型。
        /// </summary>
        public MomentPostVisibleType type { get; set; } = MomentPostVisibleType.Public;

        /// <summary>
        /// 标签名称或标签 ID 列表，最终按逗号拼接进入 proto。
        /// </summary>
        public List<string> labels { get; set; } = new();

        /// <summary>
        /// 好友 wxid/username 列表，最终按逗号拼接进入 proto。
        /// </summary>
        public List<string> friends { get; set; } = new();
    }

    /// <summary>
    /// 朋友圈 POI 地理位置配置。
    /// </summary>
    public sealed class MomentPostPoiDto
    {
        public string city { get; set; } = string.Empty;
        public string name { get; set; } = string.Empty;
        public string address { get; set; } = string.Empty;
        public float lat { get; set; }
        public float lng { get; set; }
        public string poiId { get; set; } = string.Empty;
    }
}
