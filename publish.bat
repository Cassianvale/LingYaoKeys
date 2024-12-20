@echo off
chcp 65001 > nul
setlocal EnableDelayedExpansion

echo [DEBUG] Initialization started...

:: Set color
color 0A

:: Set title
title LingYaoKeys Publisher

:: Create logs directory
echo [DEBUG] Creating logs directory...
if not exist "logs" mkdir logs

:: Get timestamp
echo [DEBUG] Getting timestamp...
set "timestamp=%date:~0,4%%date:~5,2%%date:~8,2%_%time:~0,2%%time:~3,2%%time:~6,2%"
echo [DEBUG] Original timestamp: %timestamp%
set "timestamp=!timestamp: =0!"
echo [DEBUG] Processed timestamp: !timestamp!

:: Set log file
set "logfile=logs\publish_!timestamp!.log"
echo [DEBUG] Log file path: !logfile!

:: Output start message
echo [DEBUG] Writing start message...
echo [%date% %time%] Build process started >> "!logfile!"
echo Build process started...

:: Check required tools
echo [DEBUG] Checking tar command...
where tar >nul 2>nul
if %errorlevel% neq 0 (
    echo Error: tar command not found. Please make sure tar is installed >> "!logfile!"
    echo Error: tar command not found. Please make sure tar is installed
    exit /b 1
)

:: Create temp directory
echo [DEBUG] Creating temp directory...
set "temp_dir=temp_publish"
if exist "%temp_dir%" rd /s /q "%temp_dir%"
mkdir "%temp_dir%"

:: Create publish output directory
echo [DEBUG] Creating publish directory...
set "publish_dir=publish"
if not exist "%publish_dir%" mkdir "%publish_dir%"

:: Extract version and product name
echo [DEBUG] Reading project info...
echo [DEBUG] Reading version...
powershell -Command "(Select-String -Path WpfApp.csproj -Pattern '<Version>(.*)</Version>').Matches.Groups[1].Value" > temp.txt
set /p version=<temp.txt
del temp.txt

echo [DEBUG] Reading product name...
powershell -Command "(Select-String -Path WpfApp.csproj -Pattern '<Product>(.*)</Product>').Matches.Groups[1].Value" > temp.txt
set /p product=<temp.txt
del temp.txt

echo [DEBUG] Product name: !product!
echo [DEBUG] Version: !version!

echo Product Name: !product! >> "!logfile!"
echo Version: !version! >> "!logfile!"
echo Product Name: !product!
echo Version: !version!

:: Set build output path
set "build_output=bin\x64\Release\net8.0-windows\win-x64"
echo [DEBUG] Build output path: !build_output!

:: Clean build directory
echo [DEBUG] Cleaning build directory...
if exist "!build_output!" (
    echo [DEBUG] Removing existing build directory
    rd /s /q "!build_output!"
)

:: Execute build
echo [DEBUG] Starting build process...
echo [DEBUG] Running dotnet restore...
dotnet restore
echo [DEBUG] Running dotnet clean...
dotnet clean
echo [DEBUG] Running dotnet publish...
dotnet publish WpfApp.csproj -c Release -r win-x64 --self-contained false /p:Platform=x64
if %errorlevel% neq 0 (
    echo Error: Build failed >> "!logfile!"
    echo Error: Build failed
    exit /b 1
)

:: Copy files to temp directory
echo [DEBUG] Copying files to temp directory...
if exist "!build_output!" (
    echo [DEBUG] Build directory exists, starting copy...
    xcopy "!build_output!\*" "!temp_dir!\" /E /I /Y > nul
) else (
    echo [DEBUG] Build directory not found: !build_output!
    echo Error: Build directory not found: !build_output! >> "!logfile!"
    echo Error: Build directory not found: !build_output!
    exit /b 1
)

:: Create zip package
echo [DEBUG] Preparing to create zip package...
set "zip_file=!publish_dir!\LingYaoKeys_!version!.zip"
echo [DEBUG] Zip file path: !zip_file!
echo Creating zip package: !zip_file! >> "!logfile!"
echo Creating zip package: !zip_file!

echo [DEBUG] Switching to temp directory...
pushd "!temp_dir!"
echo [DEBUG] Running tar command...
tar -a -c -f "..\!zip_file!" * > nul 2>&1
set tar_error=!errorlevel!
echo [DEBUG] Tar command result: !tar_error!
popd

if !tar_error! neq 0 (
    echo Error: Failed to create zip package >> "!logfile!"
    echo Error: Failed to create zip package
    exit /b 1
)

:: Check if zip package was created successfully
echo [DEBUG] Checking zip package...
if exist "!zip_file!" (
    echo Success: Zip package created: !zip_file! >> "!logfile!"
    echo Success: Zip package created: !zip_file!
) else (
    echo Error: Failed to create zip package >> "!logfile!"
    echo Error: Failed to create zip package
    exit /b 1
)

:: Clean up temp files
echo [DEBUG] Cleaning up temp files...
rd /s /q "!temp_dir!" > nul 2>&1

echo Build process completed! >> "!logfile!"
echo Build process completed!

:: Pause to view results
pause