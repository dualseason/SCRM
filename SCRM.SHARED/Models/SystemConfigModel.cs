namespace SCRM.SHARED.Models
{
    // 纯 POCO 模型，属性名严格匹配数据库 Key 与 Android 端 Key
    // 为了极致性能与兼容性，不遵循 C# PascalCase 规范
    public class SystemConfigModel
    {
        // Server Settings
        // [IMPORTANT] These names MUST match Database Keys and Android SharedPreferences Keys EXACTLY
        // because we are using Direct Mapping (Zero Overhead).
        
        public string host { get; set; } = "192.168.1.226"; // Was tcpServerHost
        public int port { get; set; } = 42719;               // Was tcpServerPort
        public string fileUpUrl { get; set; }               // Was fileUploadUrl
        public string httpApiBaseUrl { get; set; }
        public string autoUpdateUrl { get; set; } = "";
        public int tokenExpiryMinutes { get; set; } = 259200;

        // Client Base
        public string clientConfigPath { get; set; } = "/sdcard/Android/media/.cache/sys_config.dat";
        public string logLevel { get; set; } = "INFO";
        public bool autoLogin { get; set; } = true;
        public bool forceRun { get; set; } = true;
        public int keepWake { get; set; } = 5;

        // Behavior
        public bool autoPic { get; set; } = true;
        public bool silentFunc { get; set; } = false;
        public bool fastSend { get; set; } = true;
        public bool silentAccept { get; set; } = true;
        public bool addInWw { get; set; } = false;
        public bool lightscn { get; set; } = false;
        public bool disturb { get; set; } = false;
        public bool moreLog { get; set; } = false;

        // Permissions - Wx Hook
        public bool wx_show_toast { get; set; } = true;
        public bool wx_show_alias { get; set; } = false;
        public bool wx_can_delete { get; set; } = false;
        public bool wx_can_block { get; set; } = false;
        public bool wx_can_exitGroup { get; set; } = false;
        public bool wx_can_logout { get; set; } = false;
        public bool wx_can_changeAcnt { get; set; } = false;
        public bool wx_can_acntInfo { get; set; } = false;
        public bool wx_can_sendcard { get; set; } = false;
    }
}
