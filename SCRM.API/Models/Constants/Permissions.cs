namespace SCRM.Models.Constants
{
    public static class Permissions
    {
        // 用户管理权限
        public static class User
        {
            public const string View = "user.view";
            public const string Create = "user.create";
            public const string Edit = "user.edit";
            public const string Delete = "user.delete";
            public const string ManageRoles = "user.manage_roles";
            public const string ResetPassword = "user.reset_password";
        }

        // 角色管理权限
        public static class Role
        {
            public const string View = "role.view";
            public const string Create = "role.create";
            public const string Edit = "role.edit";
            public const string Delete = "role.delete";
            public const string ManagePermissions = "role.manage_permissions";
        }

        // 权限管理权限
        public static class Permission
        {
            public const string View = "permission.view";
            public const string Create = "permission.create";
            public const string Edit = "permission.edit";
            public const string Delete = "permission.delete";
        }

        // 客户管理权限
        public static class Customer
        {
            public const string View = "customer.view";
            public const string Create = "customer.create";
            public const string Edit = "customer.edit";
            public const string Delete = "customer.delete";
            public const string Export = "customer.export";
            public const string Import = "customer.import";
            public const string AssignSales = "customer.assign_sales";
            public const string ViewPhone = "customer.view_phone";
            public const string ViewWechatId = "customer.view_wechat_id";
            public const string ViewSensitive = "customer.view_sensitive";
        }

        // 订单管理权限
        public static class Order
        {
            public const string View = "order.view";
            public const string Create = "order.create";
            public const string Edit = "order.edit";
            public const string Delete = "order.delete";
            public const string Cancel = "order.cancel";
            public const string Refund = "order.refund";
            public const string Approve = "order.approve";
            public const string Export = "order.export";
        }

        // 产品管理权限
        public static class Product
        {
            public const string View = "product.view";
            public const string Create = "product.create";
            public const string Edit = "product.edit";
            public const string Delete = "product.delete";
            public const string ManageInventory = "product.manage_inventory";
            public const string ManagePrice = "product.manage_price";
        }

        // 系统管理权限
        public static class System
        {
            public const string Dashboard = "system.dashboard";
            public const string Settings = "system.settings";
            public const string Logs = "system.logs";
            public const string Backup = "system.backup";
            public const string Maintenance = "system.maintenance";
        }

        // 报表权限
        public static class Report
        {
            public const string View = "report.view";
            public const string Create = "report.create";
            public const string Export = "report.export";
            public const string Sales = "report.sales";
            public const string Customer = "report.customer";
            public const string Finance = "report.finance";
        }

        // 风控与审计权限
        public static class Risk
        {
            public const string AuditView = "risk.audit.view";
        }

        // 设备任务权限
        public static class DeviceTask
        {
            public const string Sync = "device_task.sync";
            public const string ConfigManage = "device_task.config_manage";
            public const string SensitiveConfigManage = "device_task.sensitive_config_manage";
            public const string Screenshot = "device_task.screenshot";
            public const string DeleteDevice = "device_task.delete_device";
        }

        // 微信账号操作权限
        public static class WechatOperation
        {
            public const string LocationQuery = "wechat.location.query";
            public const string WalletQuery = "wechat.wallet.query";
            public const string Logout = "wechat.account.logout";
            public const string SettingUpdate = "wechat.setting.update";
            public const string QrCodePull = "wechat.qrcode.pull";
            public const string A8KeyQuery = "wechat.a8key.query";
        }

        // 消息操作权限
        public static class MessageOperation
        {
            public const string Revoke = "message.revoke";
            public const string ClearWechat = "message.clear_wechat";
            public const string PullOriginal = "message.pull_original";
            public const string VoiceTransText = "message.voice_trans_text";
        }

        // 联系人操作权限
        public static class ContactOperation
        {
            public const string DeleteWechat = "contact.delete_wechat";
            public const string PermissionSet = "contact.permission_set";
            public const string LabelManage = "contact.label_manage";
        }

        // 群操作权限
        public static class GroupOperation
        {
            public const string Manage = "group.manage";
            public const string MemberAdd = "group.member_add";
            public const string MemberKick = "group.member_kick";
            public const string JoinByQr = "group.join_by_qr";
            public const string InviteApprove = "group.invite_approve";
            public const string Exit = "group.exit";
        }

        // 朋友圈操作权限
        public static class MomentOperation
        {
            public const string Post = "moment.post";
            public const string Delete = "moment.delete";
            public const string Interact = "moment.interact";
        }

        // 视频号操作权限
        public static class FinderOperation
        {
            public const string Read = "finder.read";
            public const string Interact = "finder.interact";
            public const string DeleteComment = "finder.delete_comment";
            public const string Export = "finder.export";
            public const string ViewRaw = "finder.raw.view";
            public const string ViewMedia = "finder.media.view";
            public const string ViewMetrics = "finder.metrics.view";
        }

        // 消息权限
        public static class Message
        {
            public const string Send = "message.send";
            public const string Receive = "message.receive";
            public const string Broadcast = "message.broadcast";
            public const string Template = "message.template";
            public const string ViewContent = "message.view_content";
            public const string ViewRaw = "message.view_raw";
        }

        // 手机短信/通话记录权限
        public static class PhoneRecord
        {
            public const string ViewNumber = "phone_record.view_number";
            public const string ViewSmsContent = "phone_record.view_sms_content";
            public const string ViewCallRecordUrl = "phone_record.view_call_record_url";
        }
    }

    public static class Roles
    {
        public const string SuperAdmin = "SuperAdmin";
        public const string Admin = "Admin";
        public const string Manager = "Manager";
        public const string Sales = "Sales";
        public const string CustomerService = "CustomerService";
        public const string User = "User";
    }
}
