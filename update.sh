#!/bin/bash

# 配置信息
APP_DIR="/opt/SCRM"
ZIP_FILE="release.zip"
SERVICE_NAME="scrm"

# 检查是否以 root 运行 (若是 ubuntu 用户需加 sudo)
if [ "$EUID" -ne 0 ]; then
  echo "Please run as root (sudo ./update.sh)"
  exit 1
fi

# 进入应用目录
cd $APP_DIR || { echo "Directory $APP_DIR not found!"; exit 1; }

# 1. 停止服务
echo "Stopping $SERVICE_NAME service..."
systemctl stop $SERVICE_NAME

# 2. 备份旧版本 (可选，建议保留)
# if [ -d "publish" ]; then
#     mv publish publish_backup_$(date +%Y%m%d%H%M%S)
# fi

# 3. 删除旧的发布文件
echo "Removing old publish folder..."
rm -rf publish

# 4. 解压新的发布文件
# 注意：我们上传的文件名是 release.zip
if [ -f "$ZIP_FILE" ]; then
    echo "Extracting new publish files..."
    # -o: overwrite without prompting
    # -d: extract to directory
    unzip -o $ZIP_FILE -d ./publish
    
    # 删除压缩包 (可选)
    # rm $ZIP_FILE
else
    echo "Error: $ZIP_FILE not found!"
    exit 1
fi

# 5. 设置执行权限
echo "Setting execute permissions..."
chmod +x ./publish/SCRM.API

# 6. 启动服务
echo "Starting $SERVICE_NAME service..."
systemctl start $SERVICE_NAME

# 7. 检查服务状态
echo "Checking service status..."
systemctl status $SERVICE_NAME --no-pager
