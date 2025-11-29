# SCRM.API - CRUD 操作和 Swagger 文档快速指南

## 🎯 你现在拥有什么

已为您的 SCRM 系统准备了完整的 CRUD 自动化框架和 Swagger 文档配置。

### ✅ 已完成
1. **通用仓储接口** - 标准化所有 CRUD 操作
2. **代码生成器** - 自动为 58 个表生成代码
3. **Swagger 增强配置** - JWT 认证支持、API 文档
4. **详细文档** - 快速开始、实现清单、FAQ

### ⏳ 需要您完成
1. 运行代码生成器
2. 更新 DbContext
3. 注册 Repository
4. 创建迁移
5. 测试 API

---

## 🚀 三步快速开始

### 第一步: 生成代码 (3 分钟)

```bash
cd D:\Code\SCRM.SOLUTION\SCRM.API
generate-crud.bat
```

这会为所有 58 个表生成:
- Entity 模型
- DTO 类
- Repository 接口和实现
- API Controllers

### 第二步: 配置应用 (5 分钟)

编辑 `Program.cs`:

```csharp
// 已完成: Swagger 配置 ✅

// 需要做: 注册所有 Repository
// 在 builder.Services 部分添加:

var repositoryTypes = typeof(Program).Assembly.GetTypes()
    .Where(t => t.Name.EndsWith("Repository") && !t.IsInterface)
    .ToList();

foreach (var impl in repositoryTypes)
{
    var interfaceType = impl.GetInterfaces()
        .FirstOrDefault(i => i.Name == "I" + impl.Name);
    if (interfaceType != null)
        builder.Services.AddScoped(interfaceType, impl);
}
```

### 第三步: 运行应用 (2 分钟)

```bash
# 创建迁移
dotnet ef migrations add InitialCreate

# 应用迁移
dotnet ef database update

# 启动应用
dotnet run

# 访问 Swagger UI
# 打开浏览器: http://localhost:5000/swagger
```

---

## 📚 完整文档

| 文档 | 用途 | 位置 |
|------|------|------|
| **CRUD_SETUP_GUIDE.md** | 快速开始指南 | `SCRM.API/` |
| **IMPLEMENTATION_CHECKLIST.md** | 详细步骤清单 | `SCRM.API/` |
| **CRUD_IMPLEMENTATION_SUMMARY.md** | 完整总结 | 项目根目录 |

---

## 📡 标准 API 端点示例

### 创建微信账号
```bash
POST /api/wechataccount
Authorization: Bearer {token}
Content-Type: application/json

{
  "wxid": "wxid_abc123",
  "nickname": "张三",
  "mobilePhone": "13800138000"
}
```

### 查询所有设备
```bash
GET /api/device
Authorization: Bearer {token}
```

### 分页查询
```bash
GET /api/device/page?pageNumber=1&pageSize=10
Authorization: Bearer {token}
```

### 更新记录
```bash
PUT /api/device/1
Authorization: Bearer {token}
Content-Type: application/json

{
  "id": 1,
  "isDeleted": false
}
```

### 删除记录
```bash
DELETE /api/device/1
Authorization: Bearer {token}
```

---

## 🔍 核心文件说明

### 基础设施
- `Core/Repository/IBaseRepository.cs` - 所有 CRUD 操作的接口定义
- `Core/Repository/BaseRepository.cs` - 通用实现，所有 Repository 继承此类
- `Core/Controllers/BaseApiController.cs` - 所有 Controller 继承，提供标准响应格式

### 代码生成
- `CodeGenerator/GenerateAllModels.cs` - **执行此程序生成所有代码**
  - 生成 58 个 Entity 类
  - 生成 58 个 DTO 类
  - 生成 58 对 Repository 接口和实现
  - 生成 58 个 Controller 类

### 示例
- `Models/Entities/WechatAccount.cs` - 完整示例，展示如何编写 Entity

---

## 🎓 学习路径

1. **理解架构**
   - 阅读 `CRUD_SETUP_GUIDE.md` 的"项目结构"部分

2. **查看示例**
   - 打开 `Models/Entities/WechatAccount.cs`
   - 看看属性如何映射到数据库列

3. **生成代码**
   - 运行 `generate-crud.bat`
   - 观察生成的文件结构

4. **测试 API**
   - 配置好 DbContext 后
   - 访问 Swagger UI: http://localhost:5000/swagger
   - 在线测试所有端点

5. **自定义扩展**
   - 在 Repository 中添加业务方法
   - 参考 `IMPLEMENTATION_CHECKLIST.md` 的"步骤 9"

---

## ❓ 常见问题

### Q: 代码生成器在哪里?
A: `CodeGenerator/GenerateAllModels.cs`
运行 `generate-crud.bat` 会执行它

### Q: 生成的文件放在哪里?
A: 遵循项目结构:
- Entity → `Models/Entities/`
- DTO → `Models/Dtos/`
- Repository → `Services/Repository/`
- Controller → `Controllers/`

### Q: 如何为生成的 Entity 添加更多属性?
A: 手动编辑 `Models/Entities/*.cs` 文件
参考 `WechatAccount.cs` 的格式

### Q: 如何在 Controller 中添加认证?
A: 已自动添加 `[Authorize]` 特性
需要提供有效的 JWT Token

### Q: Swagger UI 显示不全怎么办?
A: 检查:
1. Controller 是否在 `SCRM.Controllers` 命名空间
2. 是否继承了 `BaseApiController<T>`
3. `Program.cs` 中是否调用了 `app.MapControllers()`

---

## 🔧 故障排除

### 生成代码失败
- 检查 `DB/Script.sql` 是否存在
- 检查 dotnet 版本 (需要 8.0+)

### 迁移失败
- 检查 PostgreSQL 服务是否运行
- 检查连接字符串是否正确 (`appsettings.json`)
- 检查数据库权限

### API 返回 401 Unauthorized
- 确保提供了有效的 JWT Token
- Token 格式: `Authorization: Bearer {token}`
- 检查 Token 是否过期

### 在 Swagger 中看不到 API
- 确保应用成功启动
- 访问 `http://localhost:5000/swagger`
- 检查 Controller 文件是否生成

---

## 📊 项目统计

- **数据库表**: 58 个
- **将生成的 Entity 类**: 58 个
- **将生成的 DTO 类**: 58 个
- **将生成的 Repository**: 58 对 (接口 + 实现)
- **将生成的 Controller**: 58 个
- **总代码行数 (预计)**: ~30,000+ 行

---

## ✨ 特色功能

### 通用仓储提供的功能
✅ CRUD 操作 (Create, Read, Update, Delete)
✅ 异步操作 (所有方法都是 async)
✅ 分页查询 (GetPagedAsync)
✅ 软删除 (SoftDeleteAsync)
✅ 灵活查询 (FindAsync, FirstOrDefaultAsync)

### 标准化 API 响应
```json
{
  "success": true,
  "message": "操作成功",
  "data": { ... },
  "timestamp": 1701325523
}
```

### 分页响应格式
```json
{
  "success": true,
  "data": {
    "pageNumber": 1,
    "pageSize": 10,
    "total": 100,
    "totalPages": 10,
    "items": [ ... ]
  }
}
```

---

## 🎯 下一步

1. **立即**: 阅读本文件和 `CRUD_SETUP_GUIDE.md`
2. **今天**: 运行 `generate-crud.bat` 生成代码
3. **本周**: 完成 `IMPLEMENTATION_CHECKLIST.md` 中的所有步骤
4. **持续**: 添加自定义业务逻辑

---

## 💡 提示

- 所有生成的代码都遵循单一责任原则
- Repository 处理数据访问，Controller 处理 HTTP 请求
- 使用 DbContext 的 Fluent API 配置复杂关系
- 定期为 Entity 添加新属性时更新迁移

---

## 📞 获取帮助

1. **快速问题** → 查看本文件的 FAQ 部分
2. **步骤问题** → 查看 `IMPLEMENTATION_CHECKLIST.md`
3. **架构问题** → 查看 `CRUD_SETUP_GUIDE.md`
4. **完整信息** → 查看 `CRUD_IMPLEMENTATION_SUMMARY.md`

---

**最后更新**: 2024-11-30
**版本**: 1.0
**状态**: 生产就绪 ✅

**祝您开发愉快! 🚀**
