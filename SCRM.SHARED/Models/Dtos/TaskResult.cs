namespace SCRM.SHARED.Models.Dtos
{
    public class TaskResult
    {
        public bool success { get; set; }
        public string? message { get; set; }
        public object? data { get; set; }

        public static TaskResult Ok(object? data = null) => new TaskResult { success = true, data = data };
        public static TaskResult Fail(string message) => new TaskResult { success = false, message = message };
    }
}
