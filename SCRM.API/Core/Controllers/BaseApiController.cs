using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using System.ComponentModel.DataAnnotations;

namespace SCRM.Core.Controllers
{
    /// <summary>
    /// 通用API响应包装类
    /// </summary>
    [ApiController]
    public class ApiResponse<T>
    {
        /// <summary>是否成功</summary>
        public bool Success { get; set; }

        /// <summary>响应消息</summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>响应数据</summary>
        public T? Data { get; set; }

        /// <summary>时间戳</summary>
        public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        public static ApiResponse<T> SuccessResponse(T? data = default, string message = "操作成功")
        {
            return new ApiResponse<T>
            {
                Success = true,
                Message = message,
                Data = data
            };
        }

        public static ApiResponse<T> ErrorResponse(string message = "操作失败")
        {
            return new ApiResponse<T>
            {
                Success = false,
                Message = message
            };
        }
    }

    /// <summary>
    /// 分页查询响应类
    /// </summary>
    public class PagedResponse<T>
    {
        /// <summary>当前页码</summary>
        [Required]
        public int PageNumber { get; set; }

        /// <summary>每页记录数</summary>
        [Required]
        public int PageSize { get; set; }

        /// <summary>总记录数</summary>
        [Required]
        public int Total { get; set; }

        /// <summary>总页数</summary>
        [Required]
        public int TotalPages => (Total + PageSize - 1) / PageSize;

        /// <summary>数据列表</summary>
        public List<T> Items { get; set; } = new();

        public static PagedResponse<T> Create(int pageNumber, int pageSize, int total, List<T> items)
        {
            return new PagedResponse<T>
            {
                PageNumber = pageNumber,
                PageSize = pageSize,
                Total = total,
                Items = items
            };
        }
    }

    /// <summary>
    /// 基础API控制器
    /// </summary>
    /// <typeparam name="TDto">DTO类型</typeparam>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [Consumes("application/json")]
    public abstract class BaseApiController<TDto> : ControllerBase where TDto : class
    {
        /// <summary>
        /// 返回成功响应
        /// </summary>
        protected OkObjectResult OkResponse<T>(T? data = default, string message = "操作成功")
        {
            return Ok(ApiResponse<T>.SuccessResponse(data, message));
        }

        /// <summary>
        /// 返回分页成功响应
        /// </summary>
        protected OkObjectResult OkPagedResponse<T>(int pageNumber, int pageSize, int total, List<T> items)
        {
            return Ok(ApiResponse<PagedResponse<T>>.SuccessResponse(
                PagedResponse<T>.Create(pageNumber, pageSize, total, items),
                "获取成功"
            ));
        }

        /// <summary>
        /// 返回错误响应
        /// </summary>
        protected BadRequestObjectResult BadRequestResponse<T>(string message = "操作失败")
        {
            return BadRequest(ApiResponse<T>.ErrorResponse(message));
        }

        /// <summary>
        /// 返回未找到响应
        /// </summary>
        protected NotFoundObjectResult NotFoundResponse<T>(string message = "资源未找到")
        {
            return NotFound(ApiResponse<T>.ErrorResponse(message));
        }

        /// <summary>
        /// 返回服务器错误响应
        /// </summary>
        protected ObjectResult ErrorResponse<T>(string message = "服务器内部错误")
        {
            return StatusCode(500, ApiResponse<T>.ErrorResponse(message));
        }
    }
}
