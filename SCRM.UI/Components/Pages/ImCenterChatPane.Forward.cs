using System.Text.RegularExpressions;
using Radzen;
using SCRM.API.Models.Entities;
using SCRM.SHARED.Models.Dtos;
using SCRM.UI.Components.Dialogs;

namespace SCRM.UI.Components.Pages;

public partial class ImCenterChatPane
{
    #region 转发动作与状态

    /// <summary>
    /// 切换转发设置面板。
    /// </summary>
    private void ToggleForwardSettings()
    {
        _showForwardSettings = !_showForwardSettings;
    }

    /// <summary>
    /// 判断是否展示转发设置面板。
    /// <para>存在已填写目标/附言或处于多选转发时自动保持展开，避免用户误以为设置丢失。</para>
    /// </summary>
    private bool ShouldShowForwardSettingsPanel()
    {
        return _showForwardSettings
            || _multiForwardMode
            || !string.IsNullOrWhiteSpace(_forwardTargetInput)
            || !string.IsNullOrWhiteSpace(_forwardExtMsg);
    }

    /// <summary>
    /// 切换多选转发模式。
    /// </summary>
    private void ToggleMultiForwardMode()
    {
        _multiForwardMode = !_multiForwardMode;
        if (!_multiForwardMode)
        {
            _selectedForwardMsgSvrIds.Clear();
        }
        else
        {
            _showForwardSettings = true;
            _lastForwardContextKey = BuildForwardContextKey();
        }
    }

    /// <summary>
    /// 选择或取消选择一条可按原消息转发的消息。
    /// </summary>
    private void ToggleForwardMessageSelection(Message msg)
    {
        var msgSvrId = msg.msgSvrId.GetValueOrDefault();
        if (msgSvrId <= 0)
        {
            return;
        }

        if (!_selectedForwardMsgSvrIds.Add(msgSvrId))
        {
            _selectedForwardMsgSvrIds.Remove(msgSvrId);
        }
    }

    private bool IsForwardSelected(Message msg)
    {
        var msgSvrId = msg.msgSvrId.GetValueOrDefault();
        return msgSvrId > 0 && _selectedForwardMsgSvrIds.Contains(msgSvrId);
    }

    private bool CanForwardExistingMessage(Message msg)
    {
        return !msg.isRevoked
            && !msg.isDeleted
            && CanForwardMessageType(msg.messageType)
            && msg.msgSvrId.GetValueOrDefault() > 0;
    }

    private bool CanForwardMessage(Message msg)
    {
        return !msg.isRevoked
            && !msg.isDeleted
            && CanForwardMessageType(msg.messageType)
            && (msg.msgSvrId.GetValueOrDefault() > 0 || !string.IsNullOrWhiteSpace(ResolveForwardContent(msg)))
            && msg.messageType > 0;
    }

    private static bool CanForwardMessageType(int messageType)
    {
        return SupportedForwardMessageTypes.Contains(messageType);
    }

    /// <summary>
    /// 从当前联系人、群聊和最近会话中选择转发目标，避免手工复制 wxid。
    /// </summary>
    private async Task PickForwardTargets()
    {
        var result = await DialogService.OpenAsync<ForwardTargetPickerDialog>(
            "选择转发目标",
            new Dictionary<string, object>
            {
                ["InitialTargets"] = _forwardTargetInput
            },
            new DialogOptions
            {
                Width = "620px",
                Resizable = true,
                Draggable = true,
                CloseDialogOnOverlayClick = true
            });

        if (result is string targets && !string.IsNullOrWhiteSpace(targets))
        {
            _forwardTargetInput = NormalizeForwardTargetIds(targets);
            _showForwardSettings = true;
        }
    }

    /// <summary>
    /// 转发单条消息。有 MsgSvrId 时走 ForwardMessageTask；缺 MsgSvrId 时降级走 ForwardMessageByContentTask。
    /// </summary>
    private async Task ForwardChatMessage(Message msg)
    {
        if (Store.SelectedDevice == null)
        {
            NoticeService.Notify(NotificationSeverity.Warning, "请先选择设备", "转发消息需要在线设备执行。");
            return;
        }

        var targets = NormalizeForwardTargetIds(_forwardTargetInput);
        if (string.IsNullOrWhiteSpace(targets))
        {
            _showForwardSettings = true;
            NoticeService.Notify(NotificationSeverity.Warning, "缺少转发目标", "请先在转发设置区输入目标 wxid 或群 ID，多个目标用逗号分隔。");
            return;
        }

        var msgSvrId = msg.msgSvrId.GetValueOrDefault();
        if (msgSvrId == 0 && CountForwardTargetIds(targets) != 1)
        {
            NoticeService.Notify(
                NotificationSeverity.Warning,
                "按内容转发仅支持单目标",
                "这条消息缺少 MsgSvrId，只能按内容兜底转发，请选择一位好友或群聊。");
            return;
        }

        var talker = ResolveCurrentTalker(msg);
        if (string.IsNullOrWhiteSpace(talker) && msgSvrId > 0)
        {
            NoticeService.Notify(NotificationSeverity.Warning, "缺少原会话", "无法确定原消息所在会话。");
            return;
        }

        var confirm = await DialogService.Confirm(
            $"确认将这条消息转发到：{targets}？\n\n消息摘要：{BuildForwardMessageSummary(msg)}",
            "单条消息转发",
            new ConfirmOptions { OkButtonText = "转发", CancelButtonText = "取消" });
        if (confirm != true)
        {
            return;
        }

        _sending = true;
        try
        {
            TaskResult result;
            if (msgSvrId > 0)
            {
                result = await Store.ForwardMessageAsync(talker, msgSvrId, targets, _forwardExtMsg, Store.SelectedDevice.uuid);
            }
            else
            {
                result = await Store.ForwardMessageByContentAsync(
                    targets,
                    0,
                    msg.messageType,
                    ResolveForwardContent(msg),
                    ResolveForwardThumb(msg),
                    _forwardExtMsg,
                    Store.SelectedDevice.uuid);
            }

            NotifyForwardResult(result, "单条消息转发");
        }
        finally
        {
            _sending = false;
        }
    }

    /// <summary>
    /// 批量转发已选择的原消息。
    /// </summary>
    private async Task ForwardSelectedMessages()
    {
        if (Store.SelectedDevice == null)
        {
            NoticeService.Notify(NotificationSeverity.Warning, "请先选择设备", "批量转发需要在线设备执行。");
            return;
        }

        var targets = NormalizeForwardTargetIds(_forwardTargetInput);
        if (string.IsNullOrWhiteSpace(targets))
        {
            _showForwardSettings = true;
            NoticeService.Notify(NotificationSeverity.Warning, "缺少转发目标", "请先在转发设置区输入目标 wxid 或群 ID，多个目标用逗号分隔。");
            return;
        }

        var talker = ResolveCurrentTalker();
        if (string.IsNullOrWhiteSpace(talker))
        {
            NoticeService.Notify(NotificationSeverity.Warning, "缺少原会话", "无法确定当前会话。");
            return;
        }

        var msgIds = _selectedForwardMsgSvrIds.Where(id => id > 0).Distinct().ToArray();
        if (msgIds.Length == 0)
        {
            NoticeService.Notify(NotificationSeverity.Warning, "未选择消息", "请先选择至少一条带 MsgSvrId 的消息。");
            return;
        }

        var confirm = await DialogService.Confirm(
            $"确认将 {msgIds.Length} 条消息转发到：{targets}？",
            "批量转发消息",
            new ConfirmOptions { OkButtonText = "转发", CancelButtonText = "取消" });
        if (confirm != true)
        {
            return;
        }

        _sending = true;
        try
        {
            var result = await Store.ForwardMultiMessageAsync(
                talker,
                msgIds,
                targets,
                _forwardExtMsg,
                _forwardSendRecord,
                Store.SelectedDevice.uuid);

            NotifyForwardResult(result, "多条消息转发");
            if (result.success)
            {
                _selectedForwardMsgSvrIds.Clear();
                _multiForwardMode = false;
            }
        }
        finally
        {
            _sending = false;
        }
    }

    private void NotifyForwardResult(TaskResult result, string title)
    {
        NoticeService.Notify(
            result.success ? NotificationSeverity.Success : NotificationSeverity.Error,
            result.success ? $"{title}已下发" : $"{title}失败",
            result.message ?? (result.success ? "等待客户端回传结果" : "任务下发失败"));
    }

    private string ResolveCurrentTalker(Message? msg = null)
    {
        return FirstNonEmpty(
            Store.SelectedConversation?.conversationWxid,
            Store.SelectedContact?.wxid,
            msg == null ? string.Empty : ResolvePeerWxidFromMessage(msg));
    }

    private static string ResolveForwardContent(Message msg)
    {
        return FirstNonEmpty(msg.content, msg.contentXml);
    }

    private static string ResolveForwardThumb(Message msg)
    {
        var body = ResolveForwardContent(msg);
        return ExtractFirstUrl(body);
    }

    private static string BuildForwardMessageSummary(Message msg)
    {
        var content = FirstNonEmpty(msg.content, msg.contentXml, $"消息类型：{msg.messageType}");
        content = Regex.Replace(content, @"\s+", " ", RegexOptions.CultureInvariant).Trim();
        if (content.Length > 80)
        {
            content = $"{content[..80]}...";
        }

        return content;
    }

    private static string ExtractFirstUrl(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var match = Regex.Match(text, @"https?://[^\s""'<>]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success ? match.Value : string.Empty;
    }

    private static string NormalizeForwardTargetIds(string input)
    {
        return string.Join(",", SplitForwardTargetIds(input));
    }

    private static int CountForwardTargetIds(string input)
    {
        return SplitForwardTargetIds(input).Length;
    }

    private static string[] SplitForwardTargetIds(string input)
    {
        return (input ?? string.Empty)
            .Split(new[] { ',', '，', ';', '；', '\n', '\r', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private void ResetForwardSelectionIfContextChanged()
    {
        var key = BuildForwardContextKey();
        if (string.Equals(key, _lastForwardContextKey, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _lastForwardContextKey = key;
        _selectedForwardMsgSvrIds.Clear();
        _multiForwardMode = false;
    }

    private string BuildForwardContextKey()
    {
        return FirstNonEmpty(Store.SelectedConversation?.conversationWxid, Store.SelectedContact?.wxid, Store.SelectedDevice?.uuid);
    }

    #endregion
}
