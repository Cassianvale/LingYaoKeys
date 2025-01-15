@echo off
setlocal enabledelayedexpansion

:: Read credentials from appsettings.json
set "ROOT_DIR=%~dp0.."
set "CONFIG_FILE=%ROOT_DIR%\appsettings.json"

:: Check if config file exists
if not exist "%CONFIG_FILE%" (
    echo Error: Configuration file not found: %CONFIG_FILE%
    pause
    exit /b 1
)

:: Read and parse JSON using findstr
echo Reading configuration...
for /f "tokens=2 delims=:, " %%a in ('type "%CONFIG_FILE%" ^| findstr "AccessKeyId"') do (
    set "ACCESS_KEY_ID=%%~a"
    set "ACCESS_KEY_ID=!ACCESS_KEY_ID:"=!"
)

for /f "tokens=2 delims=:, " %%a in ('type "%CONFIG_FILE%" ^| findstr "AccessKeySecret"') do (
    set "ACCESS_KEY_SECRET=%%~a"
    set "ACCESS_KEY_SECRET=!ACCESS_KEY_SECRET:"=!"
)

if "%ACCESS_KEY_ID%"=="" (
    echo Error: Failed to read AccessKeyId from config file
    pause
    exit /b 1
)
if "%ACCESS_KEY_SECRET%"=="" (
    echo Error: Failed to read AccessKeySecret from config file
    pause
    exit /b 1
)

:: Configure Aliyun CLI with credentials from appsettings.json
echo Configuring Aliyun CLI...
call aliyun configure set --mode AK --profile default --region cn-shanghai --language zh --access-key-id "%ACCESS_KEY_ID%" --access-key-secret "%ACCESS_KEY_SECRET%"
if %ERRORLEVEL% neq 0 (
    echo Error: Failed to configure Aliyun CLI
    pause
    exit /b 1
)
echo Aliyun CLI configured successfully.

:: Get version from user input if not provided
if "%1"=="" (
    set /p VERSION="Please enter version number (e.g. 1.1.0): "
) else (
    set "VERSION=%1"
)

:: Validate version format (simplified)
echo %VERSION% | findstr /r "[0-9]\.[0-9]\.[0-9]" > nul
if %ERRORLEVEL% neq 0 (
    echo Error: Invalid version format. Please use format like 1.3.0
    pause
    exit /b 1
)

echo Project root directory: %ROOT_DIR%

:: 1. Update csproj file
set "CSPROJ_PATH=%ROOT_DIR%\WpfApp.csproj"
if not exist "%CSPROJ_PATH%" (
    echo Error: Project file not found: %CSPROJ_PATH%
    pause
    exit /b 1
)

:: Create temp file
set "TEMP_FILE=%TEMP%\temp_csproj.txt"
type nul > "%TEMP_FILE%"

:: Update version in csproj while preserving format
powershell -NoProfile -Command "(Get-Content '%CSPROJ_PATH%') -replace '<Version>.*</Version>', '<Version>%VERSION%</Version>' | Set-Content '%TEMP_FILE%' -Encoding UTF8"

:: Replace original file
copy /y "%TEMP_FILE%" "%CSPROJ_PATH%" > nul
del "%TEMP_FILE%"
echo Updated version to: %VERSION%

:: 2. Create version.json
set "VERSION_JSON=%ROOT_DIR%\version.json"
(
echo {
echo     "version": "%VERSION%",
echo     "releaseNotes": "Version %VERSION% update",
echo     "downloadUrl": "https://github.com/Cassianvale/LingYaoKeys/releases/download/v%VERSION%/LingYaoKeys-%VERSION%.zip"
echo }
) > "%VERSION_JSON%"
echo Created version file: %VERSION_JSON%

:: 3. Upload to Aliyun OSS
echo Uploading to Aliyun OSS...
call aliyun oss cp "%VERSION_JSON%" oss://lykeys-remote/version.json
if %ERRORLEVEL% neq 0 (
    echo Error: Failed to upload to Aliyun OSS
    pause
    exit /b 1
)
echo Uploaded version info to Aliyun OSS

echo Version update completed: %VERSION%
pause 