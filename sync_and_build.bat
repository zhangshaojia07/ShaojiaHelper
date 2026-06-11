@echo off
chcp 65001 > nul

if "%CELESTEGAMEPATH%"=="" (
    echo [错误] 你的电脑没有配置系统环境变量 CELESTEGAMEPATH！
    pause
    exit /b
)

set CWD=%~dp0
if "%CWD:~-1%"=="\" set CWD=%CWD:~0,-1%

robocopy "%CWD%" "%CELESTEGAMEPATH%\Mods\ShaojiaHelper" /E /PURGE /XD "Source" "Code" "workplace" ".vscode" ".git" /XF "sync_and_build.bat" ".gitignore" "README.md" /R:1 /W:1

dotnet build "%CWD%\Source" -c Release