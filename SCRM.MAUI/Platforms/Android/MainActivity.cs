using Android.App;
using Android.Content.PM;
using Android.OS;

namespace SCRM.MAUI
{
    [Activity(Theme = "@style/Maui.MainTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            try
            {
                // 基本的Android日志
                Android.Util.Log.Debug("SCRM_DEBUG", "MainActivity OnCreate STARTED");
                Android.Util.Log.Debug("SCRM_DEBUG", $"Device: {Android.OS.Build.Manufacturer} {Android.OS.Build.Model}");
                Android.Util.Log.Debug("SCRM_DEBUG", $"Android: {Android.OS.Build.VERSION.Release} (API {Android.OS.Build.VERSION.SdkInt})");

                // Setup global exception handling
                AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
                {
                    Android.Util.Log.Error("SCRM_ERROR", $"Unhandled Exception: {e.ExceptionObject}");
                };

                Android.Util.Log.Debug("SCRM_DEBUG", "Calling base.OnCreate");
                base.OnCreate(savedInstanceState);
                Android.Util.Log.Debug("SCRM_DEBUG", "base.OnCreate COMPLETED");
            }
            catch (System.Exception ex)
            {
                // 记录异常到Android日志
                Android.Util.Log.Error("SCRM_ERROR", $"MainActivity OnCreate FAILED: {ex.Message}");
                Android.Util.Log.Error("SCRM_ERROR", $"Stack: {ex.StackTrace}");

                // 尝试显示Toast
                try
                {
                    Android.Widget.Toast.MakeText(this, $"启动失败: {ex.Message}", Android.Widget.ToastLength.Long).Show();
                }
                catch
                {
                    Android.Util.Log.Error("SCRM_CRITICAL", $"Toast failed: {ex.Message}");
                }

                throw;
            }
        }

        protected override void OnResume()
        {
            try
            {
                Android.Util.Log.Debug("SCRM_DEBUG", "MainActivity OnResume");
                base.OnResume();
                Android.Util.Log.Debug("SCRM_DEBUG", "MainActivity OnResume COMPLETED");
            }
            catch (System.Exception ex)
            {
                Android.Util.Log.Error("SCRM_ERROR", $"MainActivity OnResume FAILED: {ex.Message}");
            }
        }

        protected override void OnPause()
        {
            try
            {
                Android.Util.Log.Debug("SCRM_DEBUG", "MainActivity OnPause");
                base.OnPause();
                Android.Util.Log.Debug("SCRM_DEBUG", "MainActivity OnPause COMPLETED");
            }
            catch (System.Exception ex)
            {
                Android.Util.Log.Error("SCRM_ERROR", $"MainActivity OnPause FAILED: {ex.Message}");
            }
        }

        protected override void OnDestroy()
        {
            try
            {
                Android.Util.Log.Debug("SCRM_DEBUG", "MainActivity OnDestroy");
                base.OnDestroy();
                Android.Util.Log.Debug("SCRM_DEBUG", "MainActivity OnDestroy COMPLETED");
            }
            catch (System.Exception ex)
            {
                Android.Util.Log.Error("SCRM_ERROR", $"MainActivity OnDestroy FAILED: {ex.Message}");
            }
        }
    }
}