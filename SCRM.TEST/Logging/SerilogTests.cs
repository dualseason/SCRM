using Xunit;
using FluentAssertions;
using SCRM.Shared.Core;
using Serilog;
using Serilog.Events;
using System.Collections.Generic;
using System.Linq;

namespace SCRM.TEST.Logging
{
    public class SerilogTests : IDisposable
    {
        private readonly List<LogEvent> _logEvents;
        private readonly ILogger _testLogger;

        public SerilogTests()
        {
            _logEvents = new List<LogEvent>();
            
            _testLogger = new LoggerConfiguration()
                .WriteTo.Sink(new TestSink(_logEvents))
                .CreateLogger();
        }

        [Fact]
        public void Utility_Logger_ShouldNotBeNull()
        {
            // Assert
            Utility.logger.Should().NotBeNull();
        }

        [Fact]
        public void TestLogger_Information_ShouldCaptureLog()
        {
            // Act
            _testLogger.Information("Test message: {Value}", 123);

            // Assert
            _logEvents.Should().HaveCount(1);
            _logEvents[0].Level.Should().Be(LogEventLevel.Information);
            _logEvents[0].MessageTemplate.Text.Should().Contain("Test message");
        }

        [Fact]
        public void TestLogger_Warning_ShouldCaptureLog()
        {
            // Act
            _testLogger.Warning("Warning message");

            // Assert
            _logEvents.Should().HaveCount(1);
            _logEvents[0].Level.Should().Be(LogEventLevel.Warning);
        }

        [Fact]
        public void TestLogger_Error_ShouldCaptureException()
        {
            // Arrange
            var exception = new System.Exception("Test exception");

            // Act
            _testLogger.Error(exception, "Error occurred");

            // Assert
            _logEvents.Should().HaveCount(1);
            _logEvents[0].Level.Should().Be(LogEventLevel.Error);
            _logEvents[0].Exception.Should().Be(exception);
        }

        [Fact]
        public void TestLogger_MultipleProperties_ShouldCapture()
        {
            // Act
            _testLogger.Information("User {UserId} performed {Action}", 123, "Login");

            // Assert
            _logEvents.Should().HaveCount(1);
            _logEvents[0].Properties.Should().ContainKey("UserId");
            _logEvents[0].Properties.Should().ContainKey("Action");
        }

        public void Dispose()
        {
            _logEvents.Clear();
        }

        // 测试用的 Sink
        private class TestSink : Serilog.Core.ILogEventSink
        {
            private readonly List<LogEvent> _events;

            public TestSink(List<LogEvent> events)
            {
                _events = events;
            }

            public void Emit(LogEvent logEvent)
            {
                _events.Add(logEvent);
            }
        }
    }
}
