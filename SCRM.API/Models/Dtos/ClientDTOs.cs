using System.Text.Json.Serialization;

namespace SCRM.API.Models.DTOs
{
    public class ApiResponse<T>
    {
        public int code { get; set; }
        public string message { get; set; }
        public T data { get; set; }

        public static ApiResponse<T> Success(T data, string message = "Success")
        {
            return new ApiResponse<T> { code = 200, message = message, data = data };
        }

        public static ApiResponse<T> Fail(int code, string message)
        {
            return new ApiResponse<T> { code = code, message = message, data = default };
        }
    }



    public class UserSearchParams
    {
        public string serverUrl { get; set; }
        public string heartbeatData { get; set; }
        public int interval { get; set; }
    }

    public class UserAuthToken
    {
        public string userId { get; set; }
        public string token { get; set; }
        public string tcpHost { get; set; }
        public int tcpPort { get; set; }
    }

    public class UserAuthInfo
    {
        public string userIdentifier { get; set; }
    }
}
