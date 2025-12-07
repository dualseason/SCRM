namespace SCRM.SHARED.Models
{
    public class TaskResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public object? Data { get; set; }

        public static TaskResult Ok(object? data = null) => new TaskResult { Success = true, Data = data };
        public static TaskResult Fail(string message) => new TaskResult { Success = false, Message = message };
    }
}
