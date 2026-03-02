@echo off
setlocal
chcp 65001 > nul

:menu
cls
echo ================================================
echo   QueryToCsv Build Menu
echo ================================================
echo.
echo   1. Build
echo   2. Create Installer
echo   3. Build + Create Installer
echo.
echo   4. Exit
echo.
echo ================================================
echo.

set "choice="
set /p choice="Select option (1-4): "

if "%choice%"=="1" goto build
if "%choice%"=="2" goto installer
if "%choice%"=="3" goto full_build
if "%choice%"=="4" goto exit
echo.
echo [ERROR] Invalid selection
timeout /t 2 > nul
goto menu

:build
echo.
echo ================================================
echo   Building QueryToCsv...
echo ================================================
echo.
powershell -ExecutionPolicy Bypass -File "%~dp0Build.ps1"
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Build failed
    pause
    goto menu
)
echo.
echo [SUCCESS] Build completed
echo Output: Build\QueryToCsv\
pause
goto menu

:installer
echo.
if not exist "%~dp0QueryToCsv\QueryToCsv.exe" (
    echo [ERROR] Build artifacts not found. Run Build first.
    pause
    goto menu
)
if not exist "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" (
    echo [ERROR] Inno Setup 6 not found. Please install it first.
    pause
    goto menu
)
echo ================================================
echo   Creating Installer...
echo ================================================
echo.
"%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" "%~dp0Setup.iss"
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Installer creation failed
    pause
    goto menu
)
echo.
echo [SUCCESS] Installer created
echo Output: Build\Installer\
pause
goto menu

:full_build
echo.
echo ================================================
echo   Full Build: Build + Create Installer
echo ================================================
echo.

echo [1/2] Building QueryToCsv...
echo.
powershell -ExecutionPolicy Bypass -File "%~dp0Build.ps1"
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Build failed
    pause
    goto menu
)
echo.
echo [SUCCESS] Build completed
echo.

if not exist "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" (
    echo [ERROR] Inno Setup 6 not found. Please install it first.
    pause
    goto menu
)
echo [2/2] Creating Installer...
echo.
"%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" "%~dp0Setup.iss"
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Installer creation failed
    pause
    goto menu
)

echo.
echo ================================================
echo   Full Build Completed
echo ================================================
echo.
echo [SUCCESS] All tasks completed
echo Output: Build\Installer\
pause
goto menu

:exit
echo.
echo Exiting...
exit /b 0
