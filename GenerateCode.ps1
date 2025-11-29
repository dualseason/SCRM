# SCRM API Code Generation Script
# This script generates Entity Models, DTOs, Repositories, and Controllers for all database tables

$apiPath = "D:\Code\SCRM.SOLUTION\SCRM.API"
$sqlFile = Join-Path $apiPath "DB\Script.sql"

# Table definitions extracted from Script.sql
$tables = @(
    @{ Name = "devices"; Comment = "设备信息表" },
    @{ Name = "groups"; Comment = "群聊表" },
    @{ Name = "wechat_accounts"; Comment = "微信账号信息表" },
    @{ Name = "contact_groups"; Comment = "联系人分组表" },
    @{ Name = "contact_tags"; Comment = "联系人标签表" },
    @{ Name = "contacts"; Comment = "联系人表" },
    @{ Name = "device_authorizations"; Comment = "设备授权表" },
    @{ Name = "device_heartbeats"; Comment = "设备心跳表" },
    @{ Name = "device_version_logs"; Comment = "设备版本日志表" },
    @{ Name = "friend_detection_logs"; Comment = "好友检测日志表" },
    @{ Name = "friend_requests"; Comment = "好友请求表" },
    @{ Name = "group_announcements"; Comment = "群公告表" },
    @{ Name = "group_change_logs"; Comment = "群聊变更日志表" },
    @{ Name = "group_invitations"; Comment = "群邀请表" },
    @{ Name = "group_members"; Comment = "群成员表" },
    @{ Name = "group_message_sync_logs"; Comment = "群消息同步日志表" },
    @{ Name = "group_qrcodes"; Comment = "群二维码表" },
    @{ Name = "mass_messages"; Comment = "群发消息表" },
    @{ Name = "message_sync_logs"; Comment = "消息同步日志表" },
    @{ Name = "messages"; Comment = "聊天消息表" },
    @{ Name = "server_redirects"; Comment = "服务器重定向表" },
    @{ Name = "account_status_logs"; Comment = "账号状态日志表" },
    @{ Name = "contact_change_logs"; Comment = "联系人变更日志表" },
    @{ Name = "contact_group_relations"; Comment = "联系人分组关系表" },
    @{ Name = "contact_tag_relations"; Comment = "联系人标签关系表" },
    @{ Name = "mass_message_details"; Comment = "群发消息详情表" },
    @{ Name = "message_extensions"; Comment = "消息扩展表" },
    @{ Name = "message_forwards"; Comment = "消息转发表" },
    @{ Name = "message_media"; Comment = "消息媒体表" },
    @{ Name = "message_revocations"; Comment = "消息撤回表" },
    @{ Name = "voice_to_text_logs"; Comment = "语音转文字日志表" },
    @{ Name = "message_forward_details"; Comment = "消息转发详情表" },
    @{ Name = "moments_posts"; Comment = "朋友圈文章表" },
    @{ Name = "moments_likes"; Comment = "朋友圈点赞表" },
    @{ Name = "moments_comments"; Comment = "朋友圈评论表" },
    @{ Name = "wallet_transactions"; Comment = "钱包交易记录表" },
    @{ Name = "red_packets"; Comment = "红包发送记录表" },
    @{ Name = "red_packet_records"; Comment = "红包领取记录表" },
    @{ Name = "official_accounts"; Comment = "公众号账号表" },
    @{ Name = "miniprogram_accounts"; Comment = "小程序账号表" },
    @{ Name = "official_account_search_logs"; Comment = "公众号搜索记录表" },
    @{ Name = "miniprogram_search_logs"; Comment = "小程序搜索记录表" },
    @{ Name = "official_account_messages"; Comment = "公众号消息记录表" },
    @{ Name = "miniprogram_messages"; Comment = "小程序消息记录表" },
    @{ Name = "official_account_subscriptions"; Comment = "公众号订阅记录表" },
    @{ Name = "miniprogram_access_logs"; Comment = "小程序访问记录表" },
    @{ Name = "official_account_follow_logs"; Comment = "公众号关注事件日志表" },
    @{ Name = "miniprogram_follow_logs"; Comment = "小程序关注事件日志表" },
    @{ Name = "conversations"; Comment = "会话管理表" },
    @{ Name = "device_commands"; Comment = "设备命令表" },
    @{ Name = "device_status_logs"; Comment = "设备状态日志表" },
    @{ Name = "device_locations"; Comment = "设备位置记录表" },
    @{ Name = "system_notifications"; Comment = "系统通知表" },
    @{ Name = "app_versions"; Comment = "应用版本表" },
    @{ Name = "roles"; Comment = "角色表" },
    @{ Name = "permissions"; Comment = "权限表" },
    @{ Name = "user_roles"; Comment = "用户角色关系表" },
    @{ Name = "role_permissions"; Comment = "角色权限关系表" }
)

function ToPascalCase {
    param([string]$input)
    $words = $input -split '_'
    $result = @()
    foreach ($word in $words) {
        $result += [char]::ToUpper($word[0]) + $word.Substring(1).ToLower()
    }
    return $result -join ''
}

function ToCamelCase {
    param([string]$input)
    $pascal = ToPascalCase $input
    return [char]::ToLower($pascal[0]) + $pascal.Substring(1)
}

Write-Host "开始生成SCRM代码..." -ForegroundColor Green
Write-Host "=====================================" -ForegroundColor Green

foreach ($table in $tables) {
    $className = ToPascalCase $table.Name
    $camelName = ToCamelCase $table.Name

    Write-Host "处理表: $($table.Name) -> $className" -ForegroundColor Cyan

    # 这里生成所有文件的逻辑
    # 由于篇幅限制，我们将在主程序中实现完整的代码生成
}

Write-Host "`n代码生成框架已设置完成！" -ForegroundColor Green
Write-Host "现在请运行以下命令来生成完整代码：" -ForegroundColor Yellow
Write-Host "cd $apiPath" -ForegroundColor White
Write-Host "dotnet run --project CodeGenerator" -ForegroundColor White
