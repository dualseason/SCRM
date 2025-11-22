# SCRM.TEST 项目说明

## 项目概述
SCRM.TEST是SCRM.API的完整测试项目，包含单元测试、集成测试和性能测试。

## 项目结构

```
SCRM.TEST/
├── Unit/                      # 单元测试
│   ├── Controllers/          # 控制器单元测试
│   │   └── BasicControllerTests.cs
│   └── Services/            # 服务层单元测试
│       ├── AuthServiceTests.cs
│       ├── BulkOperationServiceTests.cs
│       ├── NettyMessageServiceTests.cs
│       ├── RedisCacheServiceTests.cs
│       ├── RocketMQServicesTests.cs
│       └── SignalRMessageServiceTests.cs
├── Integration/             # 集成测试
│   ├── Controllers/         # 控制器集成测试
│   │   ├── BulkOperationTestController.cs
│   │   ├── DatabaseTestController.cs
│   │   ├── NettyTestController.cs
│   │   ├── RedisCacheTestController.cs
│   │   ├── RocketMQTestController.cs
│   │   ├── SeedDataController.cs
│   │   └── SignalRTestController.cs
│   ├── Database/           # 数据库集成测试
│   │   └── DatabaseTests.cs
│   └── API/               # API集成测试
│       └── AuthControllerTests.cs
├── TestInfrastructure/      # 测试基础设施
│   ├── Builders/          # 测试数据构建器
│   │   ├── UserBuilder.cs
│   │   └── OrderBuilder.cs
│   ├── Mocks/             # Mock服务
│   │   ├── DatabaseInitializationService.cs
│   │   ├── MockRocketMQConsumerService.cs
│   │   ├── MockRocketMQProducerService.cs
│   │   └── PermissionInitializationService.cs
│   ├── TestApplicationFactory.cs  # 测试应用程序工厂
│   ├── TestBase.cs                # 测试基类
│   └── TestDataSeeder.cs          # 测试数据种子器
├── Middleware/             # 中间件测试
│   └── RateLimitingMiddlewareTests.cs
└── TestResults/            # 测试结果文件
```

## 测试分类

### 1. 单元测试 (Unit Tests)
- **目的**: 测试单个类或方法的功能
- **位置**: `Unit/` 目录
- **特点**: 快速执行，不依赖外部资源
- **示例**: 服务类逻辑测试

### 2. 集成测试 (Integration Tests)
- **目的**: 测试多个组件之间的交互
- **位置**: `Integration/` 目录
- **特点**: 需要数据库或其他外部依赖
- **示例**: API控制器与数据库的集成测试

### 3. 测试基础设施 (Test Infrastructure)
- **目的**: 提供测试所需的共享工具和服务
- **位置**: `TestInfrastructure/` 目录
- **组件**:
  - **Builders**: 用于生成测试数据
  - **Mocks**: 模拟外部依赖
  - **Fixtures**: 测试夹具和配置
  - **Helpers**: 辅助工具类

## 运行测试

### 运行所有测试
```bash
dotnet test
```

### 运行特定测试
```bash
# 运行单元测试
dotnet test --filter "Category=Unit"

# 运行集成测试
dotnet test --filter "Category=Integration"

# 运行特定测试类
dotnet test --filter "ClassName=AuthServiceTests"

# 运行特定测试方法
dotnet test --filter "TestMethodName=GenerateToken_ValidClaims_ReturnsToken"
```

### 生成测试报告
```bash
dotnet test --collect:"XPlat Code Coverage"
```

## 编写新测试的指南

### 1. 单元测试
- 继承自适当的基类（如果需要）
- 使用Mock对象隔离外部依赖
- 测试名称遵循 `[MethodName]_[Scenario]_[ExpectedResult]` 格式
- 使用AAA模式（Arrange, Act, Assert）

### 2. 集成测试
- 继承自 `TestBase` 类
- 使用 `TestApplicationFactory` 进行集成测试
- 确保测试数据隔离，避免测试间相互影响
- 在测试完成后清理测试数据

### 3. 使用测试数据构建器
```csharp
var user = new UserBuilder()
    .WithId(1)
    .WithUsername("testuser")
    .AsInactive()
    .Build();
```

### 4. 使用测试应用程序工厂
```csharp
using var factory = new TestApplicationFactory();
var client = factory.CreateClient();
```

## 最佳实践

1. **测试隔离**: 每个测试应该是独立的，不依赖其他测试的状态
2. **数据清理**: 集成测试完成后清理测试数据
3. **命名规范**: 使用清晰、描述性的测试名称
4. **断言**: 使用具体的断言，避免过于宽泛的断言
5. **测试覆盖率**: 确保重要功能都有相应的测试覆盖
6. **性能测试**: 对关键路径进行性能测试
7. **文档**: 为复杂测试添加说明性注释

## 依赖项

- **xUnit**: 测试框架
- **Moq**: Mock对象框架
- **Microsoft.AspNetCore.Mvc.Testing**: ASP.NET Core测试工具
- **Microsoft.EntityFrameworkCore.InMemory**: 内存数据库
- **coverlet.collector**: 代码覆盖率收集器

## 注意事项

- 集成测试会使用内存数据库，测试完成后数据会被清理
- Mock服务主要用于单元测试中模拟外部依赖
- 测试结果文件默认保存在 `TestResults/` 目录
- 所有测试都应该设计为可重复执行的