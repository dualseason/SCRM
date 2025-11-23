using Serilog;
using SCRM.Shared.Core;
using Xunit;

namespace SCRM.TEST
{
    /// <summary>
    /// 测试初始化类 - 在所有测试运行前初始化 Utility.logger
    /// </summary>
    public class TestInitializer : IAsyncLifetime
    {
        public Task InitializeAsync()
        {
            // 初始化 Utility.logger 用于测试
            if (Utility.logger == null)
            {
                Utility.logger = new LoggerConfiguration()
                    .WriteTo.Console()
                    .WriteTo.Debug()
                    .CreateLogger();
            }

            return Task.CompletedTask;
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 所有测试类应继承此基类以确保logger已初始化
    /// </summary>
    public class TestBase : IClassFixture<TestInitializer>
    {
        protected TestBase(TestInitializer initializer)
        {
            // Fixture会自动初始化
        }
    }
}
