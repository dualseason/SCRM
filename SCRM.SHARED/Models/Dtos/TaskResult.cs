namespace SCRM.SHARED.Models.Dtos
{
    public class TaskResult
    {
        /// <summary>
        /// 对应的任务ID。
        /// <para>用于把“下发成功”与后续异步回执关联起来；未设置时为 0。</para>
        /// </summary>
        public long taskId { get; set; }

        public bool success { get; set; }
        public string? message { get; set; }
        public object? data { get; set; }

        public static TaskResult Ok(object? data = null) => new TaskResult { success = true, data = data };

        /// <summary>
        /// 构造成功结果，并保留任务号和提示文案。
        /// <para>页面需要区分“下发成功”和“业务最终成功”时，可用 taskId 继续关联后续异步回执。</para>
        /// </summary>
        public static TaskResult Ok(long taskId, string? message = null, object? data = null)
            => new TaskResult { taskId = taskId, success = true, message = message, data = data };

        public static TaskResult Fail(string message) => new TaskResult { success = false, message = message };

        /// <summary>
        /// 构造失败结果，并保留任务号。
        /// </summary>
        public static TaskResult Fail(long taskId, string message)
            => new TaskResult { taskId = taskId, success = false, message = message };
    }
}
