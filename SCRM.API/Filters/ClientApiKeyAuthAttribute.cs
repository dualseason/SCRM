using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SCRM.API.Models.Entities;
using SCRM.Services.Data;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SCRM.API.Filters
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class ClientApiKeyAuthAttribute : Attribute, IAsyncActionFilter
    {
        private const string ApiKeyHeaderName = "X-API-Key";
        private const string ClientUuidHeaderName = "X-Client-UUID";

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var apiKey = configuration.GetValue<string>("SecuritySettings:ClientApiKey");

            string extractedApiKey = null;
            if (context.HttpContext.Request.Headers.TryGetValue("X-App-Secret", out var secret))
            {
                extractedApiKey = secret;
            }
            else if (context.HttpContext.Request.Headers.TryGetValue(ApiKeyHeaderName, out var key))
            {
                extractedApiKey = key;
            }

            if (string.IsNullOrEmpty(extractedApiKey))
            {
                context.Result = new ContentResult()
                {
                    StatusCode = 401,
                    Content = "App Secret / API Key was not provided"
                };
                return;
            }

            if (!apiKey.Equals(extractedApiKey))
            {
                context.Result = new ContentResult()
                {
                    StatusCode = 401,
                    Content = "Unauthorized client"
                };
                return;
            }

            if (!context.HttpContext.Request.Headers.TryGetValue(ClientUuidHeaderName, out var clientUuid))
            {
                context.Result = new ContentResult()
                {
                    StatusCode = 400,
                    Content = "Client UUID was not provided"
                };
                return;
            }

            // Optional: Validate UUID format or existence in DB here if needed.
            // For now, we trust the Key + UUID presence.
            // We can store the UUID in Items for the Controller to use.
            context.HttpContext.Items["ClientUuid"] = clientUuid.ToString();

            await next();
        }
    }
}
