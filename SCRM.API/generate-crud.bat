@echo off
REM SCRM API CRUD Code Generation Script
REM This script generates Entity Models, DTOs, Repositories, and Controllers

echo ========================================
echo SCRM API - CRUD Code Generation
echo ========================================
echo.

setlocal enabledelayedexpansion

cd /d "%~dp0"

echo 检查dotnet是否已安装...
where dotnet > nul 2>&1
if %errorlevel% neq 0 (
    echo 错误: 未找到dotnet命令。请先安装.NET SDK。
    pause
    exit /b 1
)

echo ✓ dotnet已找到
echo.

echo 编译代码生成器...
dotnet build CodeGenerator/GenerateAllModels.cs 2>nul

if %errorlevel% neq 0 (
    echo 错误: 编译失败
    pause
    exit /b 1
)

echo ✓ 编译完成
echo.

echo 生成所有实体和控制器...
echo 这可能需要几秒钟...
echo.

REM Run the generator
dotnet run --project . --no-build -- generate-all

if %errorlevel% equ 0 (
    echo.
    echo ========================================
    echo ✓ 代码生成完成!
    echo ========================================
    echo.
    echo 下一步操作:
    echo 1. 检查生成的文件 (Models/, Services/Repository/, Controllers/)
    echo 2. 更新 Program.cs 中的 DbSet 配置
    echo 3. 在 Program.cs 中注册所有 Repository
    echo 4. 运行迁移: dotnet ef migrations add InitialCreate
    echo 5. 更新数据库: dotnet ef database update
    echo 6. 启动应用测试 Swagger: dotnet run
    echo.
) else (
    echo.
    echo ✗ 生成失败，请检查错误信息
    echo.
)

pause
