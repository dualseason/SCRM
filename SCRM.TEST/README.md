# SCRM.TEST - 测试项目

这是 SCRM 项目的测试项目，包含单元测试、集成测试和功能测试。

## 🧪 测试结构

```
SCRM.TEST/
├── Controllers/          # 控制器测试
│   └── AuthControllerTests.cs
├── Services/             # 服务层测试
│   └── JwtServiceTests.cs
├── Middleware/           # 中间件测试
│   └── RateLimitingMiddlewareTests.cs
├── Integration/          # 集成测试
│   └── HealthCheckIntegrationTests.cs
├── Models/               # 模型测试
├── TestApplicationFactory.cs  # 测试应用程序工厂
├── MockRedisCacheService.cs   # 模拟 Redis 缓存服务
├── appsettings.test.json      # 测试配置
└── README.md               # 本文件
```

## 🛠️ 测试技术栈

- **xUnit** - 测试框架
- **Moq** - 模拟框架
- **FluentAssertions** - 断言库
- **AutoFixture** - 测试数据生成
- **Microsoft.AspNetCore.Mvc.Testing** - ASP.NET Core 测试
- **Microsoft.EntityFrameworkCore.InMemory** - 内存数据库

## 🚀 运行测试

### 运行所有测试
```bash
dotnet test
```

### 运行特定测试类
```bash
dotnet test --filter "FullyQualifiedName~AuthControllerTests"
```

### 运行特定测试方法
```bash
dotnet test --filter "FullyQualifiedName~Login_WithValidCredentials_ReturnsOkResult"
```

### 查看测试覆盖率
```bash
dotnet test --collect:"XPlat Code Coverage"
```

## 📝 测试编写指南

### 单元测试示例
```csharp
[Fact]
public async Task ServiceMethod_WithValidInput_ReturnsExpectedResult()
{
    // Arrange
    var service = new MyService();
    var input = "test";

    // Act
    var result = await service.ProcessAsync(input);

    // Assert
    result.Should().NotBeNull();
    result.Status.Should().Be("Success");
}
```

### 集成测试示例
```csharp
[Fact]
public async Task ApiEndpoint_WithValidRequest_ReturnsOk()
{
    // Arrange
    var client = _factory.CreateClient();
    var request = new MyRequest { Data = "test" };

    // Act
    var response = await client.PostAsJsonAsync("/api/myendpoint", request);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK);
}
```

## 🎯 测试覆盖范围

### �� 已覆盖的测试
- [x] 认证控制器 (AuthController)
- [x] JWT 服务 (JwtService)
- [x] 限流中间件 (RateLimitingMiddleware)
- [x] 健康检查集成测试
- [x] API 端点可访问性测试

### 🔄 待添加的测试
- [ ] 权限服务测试 (PermissionService)
- [ ] Redis 缓存服务测试
- [ ] SignalR Hub 测试
- [ ] 数据库仓储测试
- [ ] Netty 服务测试
- [ ] RocketMQ 服务测试
- [ ] 批量操作服务测试
- [ ] 错误处理测试
- [ ] 安全性测试

## 🔧 测试配置

### TestApplicationFactory
- 使用内存数据库
- 模拟 Redis 缓存服务
- 配置测试用 JWT 设置
- 配置测试用限流设置

### MockRedisCacheService
- 提供 Redis 操作的内存实现
- 支持基本的缓存操作（Get, Set, Remove, Exists）
- 支持过期时间
- 支持列表、哈希、集合等 Redis 数据结构

## 📊 测试指标

- **单元测试覆盖率**: 目标 80%+
- **集成测试覆盖率**: 目标 60%+
- **API 端点测试覆盖率**: 目标 100%

## 🚨 测试命名约定

### 测试类命名
```
ClassName + Tests
例如: AuthControllerTests
```

### 测试方法命名
```
MethodName_WithCondition_ExpectedResult
例如: Login_WithValidCredentials_ReturnsOkResult
```

## 🔍 调试测试

### 使用 Visual Studio
1. 在测试方法上设置断点
2. 右键选择"调试测试"
3. 查看变量值和执行流程

### 使用命令行
```bash
dotnet test --logger "console;verbosity=detailed"
```

## 📝 持续集成

测试将在以下情况下自动运行：
- Pull Request 创建时
- 代码推送到主分支时
- 发布前的验证阶段

## 🤝 贡献指南

1. 为新功能编写相应的测试
2. 确保测试覆盖率不下降
3. 使用有意义的测试名称
4. 提供清晰的 Arrange-Act-Assert 结构
5. 模拟外部依赖，避免测试间的相互影响

## 🐛 常见问题

### Q: 测试运行很慢怎么办？
A: 检查是否有不必要的异步等待，考虑使用并行测试。

### Q: 测试之间有依赖关系怎么办？
A: 确保每个测试使用独立的数据和状态，避免共享状态。

### Q: 如何测试私有方法？
A: 考虑测试公共接口，如果必须测试私有方法，可以通过反射或将其设为 internal。

---

**注意**: 运行测试前请确保所有必要的测试数据和环境已正确配置。