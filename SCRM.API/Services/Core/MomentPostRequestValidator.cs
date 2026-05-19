using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using SCRM.SHARED.Models.Dtos;

namespace SCRM.API.Services.Core
{
    /// <summary>
    /// 朋友圈发布请求归一化、账号绑定与协议级预校验。
    /// <para>该类只处理 62203 PostSNSNewsTask 语义，不承担用户权限判断；权限仍由 Hub/Service 的 Guard 负责。</para>
    /// </summary>
    public static class MomentPostRequestValidator
    {
        /// <summary>
        /// 标准化请求，避免 SignalR/HTTP JSON 传入 null 集合导致任务构造异常。
        /// </summary>
        public static MomentPostRequestDto Normalize(MomentPostRequestDto? request)
        {
            request ??= new MomentPostRequestDto();
            return new MomentPostRequestDto
            {
                clientRequestId = string.IsNullOrWhiteSpace(request.clientRequestId)
                    ? Guid.NewGuid().ToString("N")
                    : request.clientRequestId.Trim(),
                weChatId = request.weChatId?.Trim() ?? string.Empty,
                content = request.content?.Trim() ?? string.Empty,
                comment = request.comment?.Trim() ?? string.Empty,
                sendSlow = request.sendSlow,
                attachment = new MomentPostAttachmentDto
                {
                    type = request.attachment?.type ?? MomentPostAttachmentType.Picture,
                    content = NormalizeStringList(request.attachment?.content)
                },
                visible = new MomentPostVisibleDto
                {
                    type = request.visible?.type ?? MomentPostVisibleType.Public,
                    labels = NormalizeStringList(request.visible?.labels),
                    friends = NormalizeStringList(request.visible?.friends)
                },
                poi = new MomentPostPoiDto
                {
                    city = request.poi?.city?.Trim() ?? string.Empty,
                    name = request.poi?.name?.Trim() ?? string.Empty,
                    address = request.poi?.address?.Trim() ?? string.Empty,
                    lat = request.poi?.lat ?? 0,
                    lng = request.poi?.lng ?? 0,
                    poiId = request.poi?.poiId?.Trim() ?? string.Empty
                },
                extComment = NormalizeStringList(request.extComment),
                notiUsers = NormalizeStringList(request.notiUsers)
            };
        }

        /// <summary>
        /// 将请求微信号强绑定到设备当前在线微信号。
        /// <para>62203 Android 不会根据 PostSNSNewsTask.WeChatId 切号，真正执行账号是当前在线微信；因此服务端必须提前绑定。</para>
        /// </summary>
        public static MomentPostValidationResult BindAndValidate(MomentPostRequestDto? request, string? currentWeChatId)
        {
            var normalized = Normalize(request);
            var result = new MomentPostValidationResult(normalized);

            var current = currentWeChatId?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(current))
            {
                result.Errors.Add("设备当前没有可用微信账号");
                return result;
            }

            var requested = normalized.weChatId?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(requested))
            {
                normalized.weChatId = current;
            }
            else if (!string.Equals(requested, current, StringComparison.OrdinalIgnoreCase))
            {
                result.Errors.Add("请求微信号与当前设备在线微信号不一致");
            }

            ValidateContent(normalized, result);
            ValidateVisible(normalized.visible, result);
            ValidatePoi(normalized.poi, result);
            ValidateAttachment(normalized.attachment, result);

            return result;
        }

        /// <summary>
        /// 协议级预校验；用于最后一层发送保护。
        /// </summary>
        public static MomentPostValidationResult Validate(MomentPostRequestDto? request)
        {
            var normalized = Normalize(request);
            var result = new MomentPostValidationResult(normalized);

            ValidateContent(normalized, result);
            ValidateVisible(normalized.visible, result);
            ValidatePoi(normalized.poi, result);
            ValidateAttachment(normalized.attachment, result);

            return result;
        }

        /// <summary>
        /// 判断回包账号是否与预期账号一致。
        /// </summary>
        public static bool IsSameWechatId(string? expectedWeChatId, string? actualWeChatId)
        {
            var expected = expectedWeChatId?.Trim() ?? string.Empty;
            var actual = actualWeChatId?.Trim() ?? string.Empty;
            return string.IsNullOrWhiteSpace(expected)
                || string.IsNullOrWhiteSpace(actual)
                || string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 构造朋友圈发布审计 metadata。
        /// <para>只返回数量、类型、布尔值和固定口径 SHA256 摘要，不返回正文、评论、附件地址、好友 wxid、标签名或 POI 原文。</para>
        /// </summary>
        public static Dictionary<string, object?> BuildSafeAuditMetadata(MomentPostRequestDto? request)
        {
            var normalized = Normalize(request);
            return new Dictionary<string, object?>
            {
                ["clientRequestId"] = normalized.clientRequestId,
                ["attachmentType"] = normalized.attachment.type.ToString(),
                ["attachmentCount"] = normalized.attachment.content.Count,
                ["visibleType"] = normalized.visible.type.ToString(),
                ["labelCount"] = normalized.visible.labels.Count,
                ["friendCount"] = normalized.visible.friends.Count,
                ["notiUserCount"] = normalized.notiUsers.Count,
                ["extCommentCount"] = normalized.extComment.Count,
                ["hasComment"] = !string.IsNullOrWhiteSpace(normalized.comment),
                ["hasPoi"] = HasPoi(normalized.poi),
                ["sendSlow"] = normalized.sendSlow,
                ["hasExplicitWeChatId"] = !string.IsNullOrWhiteSpace(normalized.weChatId),
                ["payloadHash"] = HashAuditValue(BuildPayloadFingerprintInput(normalized)),
                ["contentHash"] = HashAuditParts(BuildContentFingerprintParts(normalized)),
                ["attachmentHash"] = HashAuditParts(normalized.attachment.content),
                ["visibleTargetsHash"] = HashAuditParts(
                    normalized.visible.labels.Select(label => "label:" + label)
                        .Concat(normalized.visible.friends.Select(friend => "friend:" + friend))),
                ["notiUsersHash"] = HashAuditParts(normalized.notiUsers),
                ["effectiveWeChatIdHash"] = HashAuditValue(normalized.weChatId),
                ["requestedWeChatIdHash"] = HashAuditValue(normalized.weChatId),
                ["currentWeChatIdHash"] = HashAuditValue(normalized.weChatId),
                ["weChatIdBound"] = !string.IsNullOrWhiteSpace(normalized.weChatId),
                ["validationWarningCount"] = 0
            };
        }

        /// <summary>
        /// 对单个审计值生成固定格式 SHA256 摘要。
        /// </summary>
        public static string HashAuditValue(string? value)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value?.Trim() ?? string.Empty));
            return "sha256:" + Convert.ToHexString(bytes).ToLowerInvariant();
        }

        /// <summary>
        /// 对列表审计值生成固定格式 SHA256 摘要；列表会去空、去重并保留首次出现顺序。
        /// </summary>
        public static string HashAuditParts(IEnumerable<string>? values)
        {
            return HashAuditValue(BuildListFingerprintInput(values));
        }

        private static void ValidateContent(MomentPostRequestDto request, MomentPostValidationResult result)
        {
            if (string.IsNullOrWhiteSpace(request.content)
                && string.IsNullOrWhiteSpace(request.comment)
                && request.extComment.Count == 0
                && request.attachment.content.Count == 0)
            {
                result.Errors.Add("请至少填写正文、附件、首评或追加评论之一");
            }
        }

        private static void ValidateVisible(MomentPostVisibleDto visible, MomentPostValidationResult result)
        {
            var targetCount = visible.labels.Count + visible.friends.Count;
            if ((visible.type == MomentPostVisibleType.WhoVisible || visible.type == MomentPostVisibleType.WhoInvisible)
                && targetCount == 0)
            {
                result.Errors.Add("部分可见/不给谁看必须选择至少一个标签或好友");
            }

            if ((visible.type == MomentPostVisibleType.Public || visible.type == MomentPostVisibleType.Private)
                && targetCount > 0)
            {
                result.Errors.Add("公开/私密朋友圈不能同时携带标签或好友范围");
            }

            if (visible.labels.Any(label => label.All(char.IsDigit)))
            {
                result.Errors.Add("朋友圈可见标签需要传标签名，不要直接传 LabelId");
            }
        }

        private static void ValidatePoi(MomentPostPoiDto poi, MomentPostValidationResult result)
        {
            if (!HasPoi(poi))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(poi.city) && string.IsNullOrWhiteSpace(poi.name))
            {
                result.Errors.Add("POI 至少需要填写城市或地点名");
            }

            var hasLat = Math.Abs(poi.lat) > 0.000001f;
            var hasLng = Math.Abs(poi.lng) > 0.000001f;
            if (hasLat != hasLng)
            {
                result.Errors.Add("POI 经纬度需要同时填写");
            }
        }

        private static void ValidateAttachment(MomentPostAttachmentDto attachment, MomentPostValidationResult result)
        {
            if (attachment.type == MomentPostAttachmentType.Picture && attachment.content.Count > 9)
            {
                result.Errors.Add("朋友圈图片最多 9 张");
            }

            if (attachment.type != MomentPostAttachmentType.Picture && attachment.content.Count == 0)
            {
                result.Errors.Add("当前附件类型必须至少填写一条附件内容");
            }
        }

        private static bool HasPoi(MomentPostPoiDto poi)
        {
            return !string.IsNullOrWhiteSpace(poi.city)
                || !string.IsNullOrWhiteSpace(poi.name)
                || !string.IsNullOrWhiteSpace(poi.address)
                || !string.IsNullOrWhiteSpace(poi.poiId)
                || Math.Abs(poi.lat) > 0.000001f
                || Math.Abs(poi.lng) > 0.000001f;
        }

        private static IEnumerable<string> BuildContentFingerprintParts(MomentPostRequestDto request)
        {
            yield return request.content;
            yield return request.comment;
            yield return request.poi.city;
            yield return request.poi.name;
            yield return request.poi.address;
            yield return request.poi.poiId;
            yield return request.poi.lat.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            yield return request.poi.lng.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

            foreach (var comment in request.extComment)
            {
                yield return comment;
            }
        }

        private static string BuildPayloadFingerprintInput(MomentPostRequestDto request)
        {
            var builder = new StringBuilder();
            AppendFingerprintPart(builder, "clientRequestId", request.clientRequestId);
            AppendFingerprintPart(builder, "weChatId", request.weChatId);
            AppendFingerprintPart(builder, "content", request.content);
            AppendFingerprintPart(builder, "comment", request.comment);
            AppendFingerprintPart(builder, "sendSlow", request.sendSlow ? "1" : "0");
            AppendFingerprintPart(builder, "attachmentType", request.attachment.type.ToString());
            AppendFingerprintPart(builder, "attachmentContent", BuildListFingerprintInput(request.attachment.content));
            AppendFingerprintPart(builder, "visibleType", request.visible.type.ToString());
            AppendFingerprintPart(builder, "visibleLabels", BuildListFingerprintInput(request.visible.labels));
            AppendFingerprintPart(builder, "visibleFriends", BuildListFingerprintInput(request.visible.friends));
            AppendFingerprintPart(builder, "poiCity", request.poi.city);
            AppendFingerprintPart(builder, "poiName", request.poi.name);
            AppendFingerprintPart(builder, "poiAddress", request.poi.address);
            AppendFingerprintPart(builder, "poiId", request.poi.poiId);
            AppendFingerprintPart(builder, "poiLat", request.poi.lat.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            AppendFingerprintPart(builder, "poiLng", request.poi.lng.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            AppendFingerprintPart(builder, "extComment", BuildListFingerprintInput(request.extComment));
            AppendFingerprintPart(builder, "notiUsers", BuildListFingerprintInput(request.notiUsers));
            return builder.ToString();
        }

        private static string BuildListFingerprintInput(IEnumerable<string>? values)
        {
            var builder = new StringBuilder();
            foreach (var value in NormalizeStringList(values))
            {
                AppendFingerprintPart(builder, "item", value);
            }

            return builder.ToString();
        }

        private static void AppendFingerprintPart(StringBuilder builder, string name, string? value)
        {
            var normalized = value?.Trim() ?? string.Empty;
            builder.Append(name)
                .Append(':')
                .Append(normalized.Length)
                .Append(':')
                .Append(normalized)
                .Append('\n');
        }

        private static List<string> NormalizeStringList(IEnumerable<string>? values)
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
    /// 朋友圈发布请求预校验结果。
    /// </summary>
    public sealed class MomentPostValidationResult
    {
        public MomentPostValidationResult(MomentPostRequestDto request)
        {
            Request = request;
        }

        /// <summary>归一化后的请求。</summary>
        public MomentPostRequestDto Request { get; }

        /// <summary>阻断原因。</summary>
        public List<string> Errors { get; } = new();

        /// <summary>非阻断提醒。</summary>
        public List<string> Warnings { get; } = new();

        /// <summary>是否可下发。</summary>
        public bool IsValid => Errors.Count == 0;

        /// <summary>合并后的错误信息。</summary>
        public string ErrorMessage => string.Join("；", Errors);
    }
}
