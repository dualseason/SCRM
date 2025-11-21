# SCRM - Social Customer Relationship Management System

一个基于 ASP.NET Core 8.0 的现代化客户关系管理系统，具有实时通信、企业级安全性和高可扩展性架构。

## 🚀 项目概述

SCRM 是一个功能齐全的社交客户关系管理系统，专为现代企业设计。系统采用微服务架构，支持实时通信、多角色权限管理、消息队列等功能，适用于处理大规模客户关系数据。

### ✨ 核心特性

- 🔐 **企业级安全**: JWT 认证 + 基于角色的权限控制
- 📡 **实时通信**: SignalR + Netty 实现实时消息推送
- 🗄️ **高性能数据库**: PostgreSQL + Entity Framework Core
- ⚡ **缓存优化**: Redis 分布式缓存系统
- 📋 **消息队列**: RocketMQ 支持异步事件处理
- 📚 **API 文档**: Swagger/OpenAPI 自动生成文档
- 🏗️ **清洁架构**: 分层设计，易于维护和扩展

## 🛠️ 技术栈

### 后端技术
- **.NET 8.0** - 主要开发框架
- **ASP.NET Core** - Web API 框架
- **Entity Framework Core** - ORM 数据访问层
- **SignalR** - 实时通信
- **JWT Authentication** - 身份认证
- **BCrypt.Net** - 密码加密

### 数据库与缓存
- **PostgreSQL** - 主数据库
- **Redis** - 分布式缓存
- **EFCore.BulkExtensions** - 批量操作优化

### 消息与通信
- **DotNetty** - 异步网络通信
- **RocketMQ** - 消息队列（含 Mock 实现）
- **SignalR Hub** - 实时消息中心

### 开发工具
- **Swashbuckle.AspNetCore** - API 文档生成
- **Microsoft.Extensions.Logging** - 日志记录
- **Microsoft.Extensions.Caching** - 缓存管理

## 📁 项目结构

```
D:\Code\SCRM\
├── Controllers/                 # API 控制器
│   ├── Auth/                   # 认证相关控制器
│   ├── Permission/             # 权限管理控制器
│   └── Examples/               # 示例控制器
├── Services/                   # 业务逻辑服务层
├── Models/                     # 数据模型
│   └── Identity/               # 用户、角色、权限模型
├── Entities/                   # 数据库实体
├── Data/                       # 数据访问层
│   ├── SCRMContext.cs         # EF Core 上下文
│   └── Repositories/           # 仓储模式实现
├── Configurations/             # 配置类
├── Authorization/              # 授权处理器
├── Attributes/                 # 自定义属性
├── Constants/                  # 应用常量
├── Hubs/                       # SignalR 集线器
├── Netty/                      # Netty 消息处理
├── Migrations/                 # 数据库迁移
└── Properties/                 # 项目属性
```

## 🚀 快速开始

### 环境要求
- .NET 8.0 SDK 或更高版本
- PostgreSQL 12+
- Redis 6+
- Visual Studio 2022 或 VS Code

### 安装步骤

1. **克隆项目**
   ```bash
   git clone <repository-url>
   cd SCRM
   ```

2. **配置数据库**
   ```bash
   # 创建 PostgreSQL 数据库
   createdb SCRM

   # 更新连接字符串（在 appsettings.json 中）
   "ConnectionStrings": {
     "DefaultConnection": "Host=localhost;Database=SCRM;Username=your_username;Password=your_password"
   }
   ```

3. **配置 Redis**
   ```bash
   # 确保 Redis 服务正在运行
   # 默认连接: localhost:6379
   ```

4. **运行数据库迁移**
   ```bash
   dotnet ef database update
   ```

5. **启动应用**
   ```bash
   # 开发模式
   dotnet run

   # 或使用 Visual Studio 启动
   ```

6. **访问应用**
   - API 文档: `http://localhost:5151/swagger`
   - HTTPS: `https://localhost:7175`
   - SignalR Hub: `/scrmhub`

## 🔧 配置说明

### 应用配置 (appsettings.json)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=SCRM;Username=postgres;Password=your_password"
  },
  "Redis": {
    "ConnectionString": "localhost:6379",
    "Database": 0,
    "InstanceName": "SCRM"
  },
  "JwtSettings": {
    "SecretKey": "Your-Secret-Key",
    "Issuer": "SCRM",
    "Audience": "SCRM.Clients",
    "ExpiryMinutes": 60
  },
  "RocketMQ": {
    "NameServer": "localhost:9876",
    "ProducerGroup": "SCRM_Producer_Group",
    "ConsumerGroup": "SCRM_Consumer_Group"
  }
}
```

### 角色权限系统
系统支持以下用户角色：
- **SuperAdmin**: 系统超级管理员
- **Admin**: 管理员
- **Manager**: 经理
- **Sales**: 销售人员

## 📋 API 端点

### 认证相关
- `POST /api/auth/login` - 用户登录
- `POST /api/auth/register` - 用户注册
- `POST /api/auth/refresh-token` - 刷新令牌
- `POST /api/auth/logout` - 用户登出

### 权限管理
- `GET /api/permission/roles` - 获取角色列表
- `GET /api/permission/users/{userId}/permissions` - 获取用户权限
- `POST /api/permission/assign` - 分配权限

### 实时通信
- SignalR Hub: `/scrmhub`

更多详细的 API 文档请访问 `/swagger` 端点。

## 🧪 测试

### 运行测试
```bash
# 运行所有测试
dotnet test

# 运行特定测试项目
dotnet test ./Tests/SCRM.Tests.csproj
```

### 测试端点
项目包含多个测试控制器用于验证功能：
- `/api/test/auth` - 认证测试
- `/api/test/permission` - 权限测试
- `/api/test/redis` - Redis 缓存测试

## 📦 部署

### 开发环境
```bash
dotnet run --environment Development
```

### 生产环境
```bash
dotnet run --environment Production
```

### Docker 部署（建议）
```dockerfile
# TODO: 添加 Dockerfile 配置
```

## 🔐 安全特性

- **JWT 认证**: 无状态的令牌认证机制
- **密码加密**: BCrypt 哈希算法
- **CORS 配置**: 跨域资源共享控制
- **角色授权**: 基于角色的访问控制
- **权限细分**: 粒度化的权限管理系统

## 📊 性能优化

- **数据库优化**:
  - 批量操作支持
  - 连接池管理
  - 查询优化
- **缓存策略**:
  - Redis 分布式缓存
  - 内存缓存
  - 缓存预热
- **异步处理**: 全面使用 async/await 模式

## 🤝 贡献指南

1. Fork 项目
2. 创建功能分支 (`git checkout -b feature/AmazingFeature`)
3. 提交更改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 开启 Pull Request

## 📝 更新日志

### v1.0.0 (2024-11)
- ✅ 初始版本发布
- ✅ 基础认证和授权系统
- ✅ SignalR 实时通信
- ✅ PostgreSQL 数据持久化
- ✅ Redis 缓存集成
- ✅ Swagger API 文档

## 📄 许可证

本项目采用 MIT 许可证 - 查看 [LICENSE](LICENSE) 文件了解详情。

## 📞 联系方式

如有问题或建议，请通过以下方式联系：
- 邮箱: [your-email@example.com]
- 项目 Issues: [GitHub Issues 链接]

---

**注意**: 请在生产环境中修改默认的配置信息，包括 JWT 密钥、数据库密码等敏感信息。