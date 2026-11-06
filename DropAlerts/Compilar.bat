@echo off
setlocal
cd /d "%~dp0"
title Compilar DropAlerts

where dotnet >nul 2>&1
if errorlevel 1 (
    echo ERROR: No se encuentra dotnet. Instala el SDK de .NET 9.
    pause
    exit /b 1
)

if not exist "References\TShockAPI.dll" (
    echo ERROR: Crea la carpeta References y copia dentro TShockAPI.dll, OTAPI.dll, TerrariaServer.dll y ModFramework.dll
    pause
    exit /b 1
)

dotnet build "DropAlerts.csproj" -c Release
if errorlevel 1 (
    echo ERROR: La compilacion ha fallado.
    pause
    exit /b 1
)

echo.
echo COMPILACION CORRECTA
echo %CD%\bin\Release\net9.0\DropAlerts.dll
echo Copia DropAlerts.dll a ServerPlugins\
pause
endlocal
