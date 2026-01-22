@echo off
set ServerIP=112.74.164.233
set User=root
set "PscpPath=C:\Program Files\PuTTY\pscp.exe"
set "Password=Lingjie123%%"
set "RemotePath=/opt/SCRM"

set "ProjectDir=E:\fan\work\code\vs\SCRM\SCRM.API"
set "PublishDir=%ProjectDir%\bin\Release\net9.0\publish"
set "ObfuscatedDir=%ProjectDir%\bin\Release\net9.0\publish_obfuscated"
set "ZipPath=%ProjectDir%\release.zip"

echo [1/5] Publishing project...
if exist "%PublishDir%" rd /s /q "%PublishDir%"
dotnet publish "%ProjectDir%\SCRM.API.csproj" -c Release -r linux-x64 --self-contained false -o "%PublishDir%"
if %errorlevel% neq 0 (
    echo Publish failed!
    pause
    exit /b %errorlevel%
)


echo [3/5] Zipping files...
if exist "%ZipPath%" del "%ZipPath%"
powershell -Command "Compress-Archive -Path '%PublishDir%\*' -DestinationPath '%ZipPath%'"
if %errorlevel% neq 0 (
    echo Zipping failed!
    pause
    exit /b %errorlevel%
)

echo [4/5] Uploading to %ServerIP%...
"%PscpPath%" -pw "%Password%" "%ZipPath%" %User%@%ServerIP%:%RemotePath%/release.zip

if %errorlevel% equ 0 (
    echo [5/5] Upload successful!
) else (
    echo Upload failed!
)

pause
