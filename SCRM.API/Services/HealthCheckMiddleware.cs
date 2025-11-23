using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace SCRM.Services
{
    public class HealthCheckMiddleware
    {
        private readonly RequestDelegate _next;
        public HealthCheckMiddleware(RequestDelegate next)
        {
            _next = next;
        }
        public async Task InvokeAsync(HttpContext context)
        {
            // Simple health check endpoint at /health
            if (context.Request.Path.StartsWithSegments("/health"))
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                await context.Response.WriteAsync("Healthy");
                return;
            }
            await _next(context);
        }
    }
}
