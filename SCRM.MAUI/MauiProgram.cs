using Microsoft.Extensions.Logging;
using Radzen;
using SCRM.UI.Services;

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
                // Note: For Android Emulator, use http://10.0.2.2:5000 (or whatever port API runs on)
                // For Windows, use http://localhost:5000
                string baseUrl = DeviceInfo.Platform == DevicePlatform.Android ? "http://10.0.2.2:5000" : "http://localhost:5000";
                builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(baseUrl) });
                builder.Services.AddScoped<IDeviceService, DeviceService>();

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
