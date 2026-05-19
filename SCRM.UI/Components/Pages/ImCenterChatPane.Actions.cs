using Radzen;
using System.Text.Json;
using System.Text.RegularExpressions;
using SCRM.API.Models.Entities;
using SCRM.UI.Components.Dialogs;

namespace SCRM.UI.Components.Pages;

public partial class ImCenterChatPane
{
    #region 截图、语音、撤回、工具栏与联系人动作

    private async Task CaptureScreen()
    {
        if (Store.SelectedDevice == null) return;
        var confirm = await ConfirmCurrentExecutionAsync("截图", "将向当前执行设备下发微信截图任务。");
        if (confirm != true)
        {
            return;
        }

        var result = await Store.RequestScreenShotAsync(Store.SelectedDevice.uuid);
        if (result.success)
        {
            NoticeService.Notify(NotificationSeverity.Success, "截图完成", result.message ?? "截图任务已完成");
        }
        else
        {
            NoticeService.Notify(NotificationSeverity.Error, "截图失败", result.message ?? "截图任务执行失败");
        }
    }

    /// <summary>
    /// 对指定语音消息下发语音转文字任务。
    /// </summary>
    private async Task VoiceTransTextMessage(Message msg)
    {
        if (Store.SelectedDevice == null)
        {
            NoticeService.Notify(NotificationSeverity.Warning, "请先选择设备", "语音转文字需要在线设备执行。");
            return;
        }

        var msgSvrId = msg.msgSvrId.GetValueOrDefault();
        if (msgSvrId == 0)
        {
            NoticeService.Notify(NotificationSeverity.Warning, "无法转文字", "当前语音消息没有 MsgSvrId。");
            return;
        }

        var friendId = Store.SelectedConversation?.conversationWxid
            ?? Store.SelectedContact?.wxid
            ?? ResolvePeerWxidFromMessage(msg);
        if (string.IsNullOrWhiteSpace(friendId))
        {
            NoticeService.Notify(NotificationSeverity.Warning, "无法转文字", "未能确定语音消息所属会话。");
            return;
        }

        var result = await Store.VoiceTransTextAsync(friendId, msgSvrId, Store.SelectedDevice.uuid);
        if (result.success)
        {
            NoticeService.Notify(NotificationSeverity.Success, "语音转文字完成", result.message ?? "识别结果已返回");
        }
        else
        {
            NoticeService.Notify(NotificationSeverity.Error, "语音转文字失败", result.message ?? "任务下发失败");
        }
    }

    /// <summary>
    /// 判断表情消息是否可以发起消息级补图。
    /// </summary>
    private static bool CanPullEmojiInfoForMessage(Message msg, ChatMessageDisplay display)
    {
        return msg != null
            && display != null
            && !msg.isDeleted
            && !msg.isRevoked
            && msg.msgSvrId.GetValueOrDefault() != 0
            && display.Kind == ChatMessageKind.Emoji
            && string.IsNullOrWhiteSpace(display.MediaUrl)
            && !string.IsNullOrWhiteSpace(ResolveEmojiMd5(msg, display));
    }

    /// <summary>
    /// 对指定聊天表情消息拉取 EmojiInfo 并衔接 CDN 回填。
    /// </summary>
    private async Task PullEmojiMediaForMessage(Message msg, ChatMessageDisplay display)
    {
        if (Store.SelectedDevice == null)
        {
            NoticeService.Notify(NotificationSeverity.Warning, "请先选择设备", "表情补图需要在线设备执行。");
            return;
        }

        var md5 = ResolveEmojiMd5(msg, display);
        var msgSvrId = msg.msgSvrId.GetValueOrDefault();
        if (string.IsNullOrWhiteSpace(md5) || msgSvrId == 0)
        {
            NoticeService.Notify(NotificationSeverity.Warning, "无法补表情", "当前表情消息缺少 MD5 或 MsgSvrId。");
            return;
        }

        var friendId = Store.SelectedConversation?.conversationWxid
            ?? Store.SelectedContact?.wxid
            ?? ResolvePeerWxidFromMessage(msg);

        var result = await Store.PullEmojiInfoForMessageAsync(md5, msgSvrId, friendId, Store.SelectedDevice.uuid);
        if (result.success)
        {
            NoticeService.Notify(NotificationSeverity.Success, "表情补图已下发", result.message ?? "等待 CDN 回填后刷新显示");
        }
        else
        {
            NoticeService.Notify(NotificationSeverity.Error, "表情补图失败", result.message ?? "任务下发失败");
        }
    }

    /// <summary>
    /// 从消息结构、高级解析和原始 XML 中提取表情 MD5。
    /// </summary>
    private static string ResolveEmojiMd5(Message msg, ChatMessageDisplay display)
    {
        var fromAdvanced = display.Advanced?.Md5;
        if (IsHexMd5(fromAdvanced))
        {
            return fromAdvanced!.Trim();
        }

        foreach (var media in display.MediaAttachments)
        {
            if (IsHexMd5(media.mediaHash))
            {
                return media.mediaHash.Trim();
            }
        }

        foreach (var extension in display.MessageExtensions)
        {
            var fromExtension = ExtractMd5FromExtensionValue(extension.extensionValue);
            if (!string.IsNullOrWhiteSpace(fromExtension))
            {
                return fromExtension;
            }
        }

        return FirstNonEmpty(
            ExtractMd5FromText(msg.contentXml),
            ExtractMd5FromText(msg.content),
            ExtractMd5FromText(display.Body));
    }

    private static string ExtractMd5FromExtensionValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (TryExtractJsonObjectText(value, out var jsonText))
        {
            try
            {
                using var doc = JsonDocument.Parse(jsonText);
                var root = doc.RootElement;
                var propertyValue = GetJsonString(root, "Md5", "md5", "MD5", "emoticonmd5", "EmojiMd5", "emojiMd5", "mediaHash", "MediaHash", "FileId", "fileId");
                if (IsHexMd5(propertyValue))
                {
                    return propertyValue.Trim();
                }
            }
            catch
            {
                // 扩展 JSON 损坏时继续按普通文本扫描，避免影响消息操作按钮渲染。
            }
        }

        return ExtractMd5FromText(value);
    }

    private static string ExtractMd5FromText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        foreach (Match match in EmojiMd5Regex.Matches(text))
        {
            var value = match.Groups[1].Value;
            if (IsHexMd5(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    private static bool IsHexMd5(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.Trim();
        return text.Length == 32 && text.All(Uri.IsHexDigit);
    }

    private static readonly Regex EmojiMd5Regex = new(
        "(?i)(?:emoticonmd5|emojiMd5|md5|mediaHash|fileId)?[^a-f0-9]{0,16}([a-f0-9]{32})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// 撤回指定聊天消息。
    /// </summary>
    private async Task RevokeChatMessage(Message msg)
    {
        if (Store.SelectedDevice == null)
        {
            NoticeService.Notify(NotificationSeverity.Warning, "请先选择设备", "撤回消息需要在线设备执行。");
            return;
        }

        var msgSvrId = msg.msgSvrId.GetValueOrDefault();
        if (msgSvrId == 0)
        {
            NoticeService.Notify(NotificationSeverity.Warning, "无法撤回", "当前消息没有 MsgSvrId。");
            return;
        }

        var friendId = Store.SelectedConversation?.conversationWxid
            ?? Store.SelectedContact?.wxid
            ?? ResolvePeerWxidFromMessage(msg);
        if (string.IsNullOrWhiteSpace(friendId))
        {
            NoticeService.Notify(NotificationSeverity.Warning, "无法撤回", "未能确定消息所属会话。");
            return;
        }

        var confirm = await DialogService.Confirm(
            "确定要撤回这条消息吗？",
            "撤回消息",
            new ConfirmOptions { OkButtonText = "撤回", CancelButtonText = "取消" });
        if (confirm != true)
        {
            return;
        }

        var result = await Store.RevokeMessageAsync(friendId, msgSvrId, Store.SelectedDevice.uuid);
        if (result.success)
        {
            NoticeService.Notify(NotificationSeverity.Success, "撤回已下发", result.message ?? "等待客户端回传撤回结果");
        }
        else
        {
            NoticeService.Notify(NotificationSeverity.Error, "撤回失败", result.message ?? "任务下发失败");
        }
    }

    // 转发相关 helper 与动作已拆分到 ImCenterChatPane.Forward.cs。

    // 消息对端 wxid 推断已拆分到 ImCenterChatPane.Conversation.cs。

    #region 工具栏动作

    private Task OpenLuckyMoneyDialog()
    {
        return OpenPaymentDialog("LuckyMoney");
    }

    private Task OpenRemittanceDialog()
    {
        return OpenPaymentDialog("Remittance");
    }

    private async Task OpenFriendPermissionDialog()
    {
        if (Store.SelectedDevice == null || (Store.SelectedConversation == null && Store.SelectedContact == null))
        {
            NoticeService.Notify(NotificationSeverity.Warning, "无法执行", "请先选择在线设备和好友会话。");
            return;
        }

        var friendId = FirstNonEmpty(Store.SelectedConversation?.conversationWxid, Store.SelectedContact?.wxid);
        if (string.IsNullOrWhiteSpace(friendId) || IsChatRoomWxid(friendId))
        {
            NoticeService.Notify(NotificationSeverity.Warning, "暂不支持", "好友权限设置只支持普通好友，不支持群聊。");
            return;
        }

        await DialogService.OpenAsync<FriendPermissionDialog>(
            "设置好友权限",
            new Dictionary<string, object>
            {
                ["DeviceUuid"] = Store.SelectedDevice.uuid,
                ["FriendId"] = friendId,
                ["TargetDisplayName"] = FirstNonEmpty(Store.SelectedConversation?.displayName, Store.SelectedContact?.remarks, Store.SelectedContact?.nickname, friendId),
                ["InitialPermissionMask"] = 0
            },
            new DialogOptions
            {
                Width = "560px",
                Resizable = true,
                Draggable = true,
                CloseDialogOnOverlayClick = false
            });
    }

    private async Task OpenJielongDialog()
    {
        if (Store.SelectedDevice == null || Store.SelectedConversation == null)
        {
            NoticeService.Notify(NotificationSeverity.Warning, "无法执行", "请先选择在线设备和群聊会话。");
            return;
        }

        var chatRoomId = Store.SelectedConversation.conversationWxid;
        if (string.IsNullOrWhiteSpace(chatRoomId) || !IsCurrentConversationChatRoom())
        {
            NoticeService.Notify(NotificationSeverity.Warning, "暂不支持", "群接龙只能在微信群会话中下发。");
            return;
        }

        await DialogService.OpenAsync<JielongTaskDialog>(
            "发送群接龙",
            new Dictionary<string, object>
            {
                ["DeviceUuid"] = Store.SelectedDevice.uuid,
                ["ChatRoomId"] = chatRoomId,
                ["TargetDisplayName"] = FirstNonEmpty(Store.SelectedConversation.displayName, chatRoomId)
            },
            new DialogOptions
            {
                Width = "620px",
                Resizable = true,
                Draggable = true,
                CloseDialogOnOverlayClick = false
            });
    }

    /// <summary>
    /// 打开发红包/转账强确认弹窗。
    /// <para>金额在弹窗内按“元”录入，提交前转换为协议使用的“分”；支付密码不保存在组件字段之外。</para>
    /// </summary>
    private async Task OpenPaymentDialog(string mode)
    {
        if (Store.SelectedDevice == null || (Store.SelectedConversation == null && Store.SelectedContact == null))
        {
            NoticeService.Notify(NotificationSeverity.Warning, "无法执行", "请先选择在线设备和会话。");
            return;
        }

        var targetId = FirstNonEmpty(Store.SelectedConversation?.conversationWxid, Store.SelectedContact?.wxid);
        if (string.IsNullOrWhiteSpace(targetId))
        {
            NoticeService.Notify(NotificationSeverity.Warning, "无法执行", "当前会话目标为空。");
            return;
        }

        await DialogService.OpenAsync<PaymentTaskDialog>(
            string.Equals(mode, "LuckyMoney", StringComparison.OrdinalIgnoreCase) ? "发送微信红包" : "执行微信转账",
            new Dictionary<string, object>
            {
                ["Mode"] = mode,
                ["DeviceUuid"] = Store.SelectedDevice.uuid,
                ["TargetId"] = targetId,
                ["TargetDisplayName"] = FirstNonEmpty(Store.SelectedConversation?.displayName, Store.SelectedContact?.remarks, Store.SelectedContact?.nickname, targetId),
                ["IsGroupConversation"] = IsCurrentConversationChatRoom()
            },
            new DialogOptions
            {
                Width = "560px",
                Resizable = true,
                Draggable = true,
                CloseDialogOnOverlayClick = false
            });
    }

    #endregion

    private async Task StartChatWithContact()
    {
        if (Store.SelectedContact == null || Store.SelectedDevice == null) return;
    
        var contact = Store.SelectedContact;
    
        ActiveTab = "chats";
        await ActiveTabChanged.InvokeAsync("chats");
    
        var conv = Store.Conversations.FirstOrDefault(c => c.conversationWxid == contact.wxid);
    
        if (conv == null)
        {
            conv = new Conversation
            {
                wechatAccountId = Store.SelectedDevice.weChatId ?? string.Empty,
                conversationWxid = contact.wxid,
                displayName = string.IsNullOrEmpty(contact.remarks) ? contact.nickname ?? contact.wxid : contact.remarks,
                displayAvatar = string.IsNullOrEmpty(contact.avatar) ? "images/default-avatar.png" : contact.avatar,
                conversationType = 1,
                lastMessageTime = DateTime.UtcNow,
                lastMessageContent = string.Empty
            };
    
            Store.Conversations.Insert(0, conv);
        }

        await Store.SelectConversationAsync(conv);
    }

    private void GoAddFriend()
    {
        NavManager.NavigateTo("/features/add-friend");
    }

    private async Task DeleteSelectedContact()
    {
        if (Store.SelectedDevice == null || Store.SelectedContact == null)
        {
            NoticeService.Notify(NotificationSeverity.Warning, "无法删除", "请先选择设备和联系人。");
            return;
        }

        var displayName = string.IsNullOrWhiteSpace(Store.SelectedContact.remarks)
            ? (string.IsNullOrWhiteSpace(Store.SelectedContact.nickname) ? Store.SelectedContact.wxid : Store.SelectedContact.nickname)
            : Store.SelectedContact.remarks;
        var confirm = await DialogService.Confirm(
            $"确定要通过【{BuildCurrentExecutionLabel()}】删除好友 {displayName} ({Store.SelectedContact.wxid}) 吗？",
            "删除好友",
            new ConfirmOptions { OkButtonText = "确定", CancelButtonText = "取消" });
        if (confirm == true)
        {
            await Store.DeleteFriendAsync(Store.SelectedDevice.uuid, Store.SelectedContact.wxid);
        }
    }

    private async Task DeleteSelectedConversationFriend()
    {
        if (Store.SelectedDevice == null || Store.SelectedConversation == null)
        {
            NoticeService.Notify(NotificationSeverity.Warning, "无法删除", "请先选择设备和会话。");
            return;
        }

        var friendId = Store.SelectedConversation.conversationWxid;
        if (string.IsNullOrWhiteSpace(friendId) || friendId.EndsWith("@chatroom", StringComparison.OrdinalIgnoreCase))
        {
            NoticeService.Notify(NotificationSeverity.Warning, "暂不支持", "当前会话不是普通好友会话，不能走删除好友任务。");
            return;
        }

        var confirm = await DialogService.Confirm(
            $"确定要通过【{BuildCurrentExecutionLabel()}】删除好友 {Store.SelectedConversation.displayName} ({friendId}) 吗？",
            "删除好友",
            new ConfirmOptions { OkButtonText = "确定", CancelButtonText = "取消" });
        if (confirm == true)
        {
            await Store.DeleteFriendAsync(Store.SelectedDevice.uuid, friendId);
        }
    }

    private bool HasSelectedDeviceScreenShotState()
    {
        return Store.SelectedDevice != null
            && !string.IsNullOrWhiteSpace(Store.LastScreenShotDeviceUuid)
            && string.Equals(Store.LastScreenShotDeviceUuid, Store.SelectedDevice.uuid, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 构建当前微信/设备执行标签，用于危险或重媒体操作确认。
    /// </summary>
    private string BuildCurrentExecutionLabel()
    {
        return Store.CurrentExecutionLabel;
    }

    /// <summary>
    /// 对会下发到安卓端的关键操作进行执行对象确认。
    /// </summary>
    private Task<bool?> ConfirmCurrentExecutionAsync(string title, string detail)
    {
        return DialogService.Confirm(
            $"{detail}\n\n执行对象：{BuildCurrentExecutionLabel()}",
            title,
            new ConfirmOptions { OkButtonText = "确定执行", CancelButtonText = "取消" });
    }

    private string GetScreenShotStatusStyle()
    {
        var color = Store.LastScreenShotSuccess == false ? "var(--rz-danger)" : "var(--rz-success)";
        return $"color:{color}";
    }

    #endregion
}