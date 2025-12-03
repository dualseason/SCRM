namespace SCRM.API.Models.DTOs
{
    public class ApiResponse<T>
    {
        public int Code { get; set; }
        public string Message { get; set; }
        public T Data { get; set; }

        public static ApiResponse<T> Success(T data, string message = "Success")
        {
            return new ApiResponse<T> { Code = 200, Message = message, Data = data };
        }

        public static ApiResponse<T> Fail(int code, string message)
        {
            return new ApiResponse<T> { Code = code, Message = message, Data = default };
        }
    }

    public class DeviceInfoConfig
    {
        public string RegCode { get; set; }
        public string Imei { get; set; }
        public string Mac { get; set; }
        public string AndroidId { get; set; }
        public string PackageName { get; set; }
        public string ReturnKey { get; set; }
        public string Hsman { get; set; }
        public string Hstype { get; set; }
        public string AndroidApi { get; set; }
        public int VersionCode { get; set; }
    }

    public class BasicDeviceInfo : DeviceInfoConfig
    {
        // Inherits all properties from DeviceInfoConfig
    }

    public class ExtendedDeviceInfo : DeviceInfoConfig
    {
        public string Imsi { get; set; }
        public string Fingerprint { get; set; }
        public string UserData { get; set; }
    }

    public class UserSearchParams
    {
        public string ServerUrl { get; set; }
        public string HeartbeatData { get; set; }
        public int Interval { get; set; }
    }

    public class UserAuthToken
    {
        public string UserId { get; set; }
        public string Token { get; set; }
    }

    public class UserAuthInfo
    {
        public string UserIdentifier { get; set; }
    }
}
