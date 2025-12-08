using Microsoft.Extensions.Logging;
using Radzen;
using SCRM.UI.Services;
using Blazored.LocalStorage;

namespace SCRM.MAUI
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            try
            {
                var builder = MauiApp.CreateBuilder();
                builder
                    .UseMauiApp<App>()
                    .ConfigureFonts(fonts =>
                    {
                        fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                        fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    });

#if DEBUG
                builder.Services.AddBlazorWebViewDeveloperTools();
                builder.Logging.AddDebug();
#endif
                builder.Services.AddMauiBlazorWebView();
                builder.Services.AddRadzenComponents();
                
                // Register HttpClient and DeviceService
                // Note: For Android Emulator, use http://10.0.2.2:42718 (or whatever port API runs on)
                // For Windows, use http://localhost:42718
                string baseUrl = DeviceInfo.Platform == DevicePlatform.Android ? "http://10.0.2.2:42718" : "http://localhost:42718";
                builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(baseUrl) });
                builder.Services.AddScoped<IDeviceService, DeviceService>();
                builder.Services.AddScoped<IClientTaskService, ClientTaskService>();
                
                builder.Services.AddBlazoredLocalStorage();
                builder.Services.AddScoped<WeChatService>();
                builder.Services.AddScoped<CrmStore>();

                return builder.Build();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MAUI App Creation Failed: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
                throw;
            }
        }
    }
}
