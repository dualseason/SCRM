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

        // 消息权限
        public static class Message
        {
            public const string Send = "message.send";
            public const string Receive = "message.receive";
            public const string Broadcast = "message.broadcast";
            public const string Template = "message.template";
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