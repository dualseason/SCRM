# SCRM.SOLUTION - 社交客户关系管理系统

这是一个完整的 SCRM 解决方案，采用现代化的微服务架构，包含 API 服务和测试项目。

## 📁 项目结构

```
SCRM.SOLUTION/
├── SCRM.API/              # 主要的 Web API 项目
│   ├── Controllers/       # API 控制器
│   ├── Services/          # 业务逻辑服务
│   ├── Models/            # 数据模型
│   ├── Entities/          # 数据库实体
│   ├── Middleware/        # 自定义中间件
│   ├── Hubs/              # SignalR 集线器
│   ├── Netty/             # Netty 网络组件
│   ├── Data/              # 数据访问层
│   ├── Migrations/        # 数据库迁移
│   └── Configurations/    # 配置类
├── SCRM.TEST/             # 测试项目
│   ├── Controllers/       # 控制器测试
│   ├── Services/          # 服务测试
│   ├── Middleware/        # 中间件测试
│   ├── Integration/       # 集成测试
│   └── Models/            # 模型测试
├── SCRM.SOLUTION.sln      # Visual Studio 解决方案文件
└── README.md              # 本文件
```

## 🚀 快速开始

### 前置要求
- .NET 8.0 SDK 或更高版本
- PostgreSQL 12+
- Redis 6+
- Docker (可选)

### 开发环境设置

1. **克隆解决方案**
   ```bash
   git clone <repository-url>
   cd SCRM.SOLUTION
   ```

2. **构建解决方案**
   ```bash
   dotnet build
   ```

3. **运行 API 项目**
   ```bash
   cd SCRM.API
   dotnet run
   ```

4. **运行测试**
   ```bash
   dotnet test
   ```

### 使用 Visual Studio

1. 打开 `SCRM.SOLUTION.sln`
2. 设置 `SCRM.API` 为启动项目
3. 按 F5 运行

## 🌐 服务端点

### API 服务
- **基础 URL**: `http://localhost:5151`
- **Swagger 文档**: `http://localhost:5151/swagger`
- **健康检查**: `http://localhost:5151/health`
- **详细健康检查**: `http://localhost:5151/health/detailed`

### 实时通信
- **SignalR Hub**: `http://localhost:5151/scrmhub`

### 其他服务
- **Netty 服务器**: `localhost:8081`

## 🧪 测试

### 运行所有测试
```bash
dotnet test
```

### 运行特定测试项目
```bash
dotnet test SCRM.TEST/SCRM.TEST.csproj
```

### 查看测试覆盖率
```bash
dotnet test --collect:"XPlat Code Coverage"
```

### 测试分类
- **单元测试**: 测试单个组件
- **集成测试**: 测试组件交互
- **功能测试**: 测试端到端功能

## 🐳 Docker 部署

### 使用 Docker Compose
```bash
docker-compose up -d
```

### 构建单个服务
```bash
cd SCRM.API
docker build -t scrm-api .
docker run -p 5151:80 scrm-api
```

## 📚 项目文档

- [SCRM.API README](./SCRM.API/README.md) - API 项目详细文档
- [SCRM.TEST README](./SCRM.TEST/README.md) - 测试项目文档
- [API 文档](http://localhost:5151/swagger) - 交互式 API 文档

## 🔧 配置

### 环境变量
重要配置项可通过环境变量设置：
- `DB_PASSWORD` - 数据库密码
- `JWT_SECRET_KEY` - JWT 密钥
- `REDIS_PASSWORD` - Redis 密码

### 配置文件
- `appsettings.json` - 基础配置
- `appsettings.Development.json` - 开发环境配置
- `appsettings.Production.json` - 生产环境配置

## 🛠️ 开发指南

### 代码规范
- 遵循 C# 命名约定
- 使用异步编程模式
- 编写单元测试
- 添加适当的注释和文档

### Git 工作流
1. 从 `main` 分支创建功能分支
2. 提交代码并推送
3. 创建 Pull Request
4. 代码审查后合并

### 测试要求
- 新功能必须包含测试
- 保持测试覆盖率在 80% 以上
- 所有测试必须通过才能合并

## 📦 部署

### 开发环境
```bash
dotnet run --environment Development
```

### 生产环境
```bash
dotnet run --environment Production
```

### Azure 部署
```bash
dotnet publish -c Release
# 将发布内容部署到 Azure App Service
```

## 🤝 贡献

1. Fork 项目
2. 创建功能分支
3. 提交更改
4. 推送到分支
5. 创建 Pull Request

## 📄 许可证

本项目采用 MIT 许可证 - 查看 [LICENSE](LICENSE) 文件了解详情。

## 🆘 支持

如有问题或建议：
- 创建 GitHub Issue
- 查看项目文档
- 联系开发团队

---

**注意**: 在生产环境中使用前，请确保修改默认配置和密钥。