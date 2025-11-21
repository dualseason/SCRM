using Xunit;
using SCRM.Models.Identity;

namespace SCRM.TEST.Controllers;

public class BasicControllerTests
{
    [Fact]
    public void BasicTest_CanRun()
    {
        // This is a basic test to ensure the test project is working
        Assert.True(true);
    }

    [Fact]
    public void User_CanBeCreated()
    {
        // Arrange & Act
        var user = new User
        {
            Email = "test@example.com",
            UserName = "testuser",
            FirstName = "Test",
            LastName = "User",
            CreatedAt = DateTime.UtcNow
        };

        // Assert
        Assert.NotNull(user);
        Assert.Equal("test@example.com", user.Email);
        Assert.Equal("testuser", user.UserName);
        Assert.Equal("Test", user.FirstName);
        Assert.Equal("User", user.LastName);
    }
}