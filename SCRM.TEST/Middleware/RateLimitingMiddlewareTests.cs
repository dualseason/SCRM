using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using SCRM.Middleware;
using Xunit;

namespace SCRM.TEST.Middleware;

public class RateLimitingMiddlewareTests
{
    [Fact]
    public void RateLimitOptions_DefaultValues_AreCorrect()
    {
        // Arrange
        var options = new RateLimitOptions();

        // Assert
        Assert.True(options.EnableRateLimit);
        Assert.Equal(100, options.DefaultLimit);
        Assert.Equal(1, options.DefaultWindowMinutes);
        Assert.True(options.EnableEndpointRateLimit);
        Assert.NotNull(options.QuotaExceededResponse);
        Assert.Equal(429, options.QuotaExceededResponse.StatusCode);
    }

    [Fact]
    public void QuotaExceededResponse_DefaultValues_AreCorrect()
    {
        // Arrange
        var response = new QuotaExceededResponse();

        // Assert
        Assert.Equal(429, response.StatusCode);
        Assert.NotNull(response.Message);
        Assert.NotEmpty(response.Message);
    }
}