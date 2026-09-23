@echo off
setlocal
cd /d "%~dp0"

if not exist "appsettings.Local.json" (
    echo ======================================================
    echo FALTA appsettings.Local.json
    echo Copia appsettings.Local.example.json y pon tus datos.
    echo Lee EMPIEZA_AQUI.txt
    echo ======================================================
    pause
    exit /b 1
)

echo =============================================
echo HillApp v3 - restaurando paquetes...
echo =============================================
dotnet restore
if errorlevel 1 goto error

echo.
echo =============================================
echo HillApp v3 - iniciando servidor...
echo Cuando aparezca "Now listening on", abre esa URL.
echo Para detenerlo presiona Ctrl+C.
echo =============================================
dotnet run
goto end

:error
echo.
echo ERROR: No se pudo ejecutar dotnet.
echo Revisa que tengas .NET 10 SDK instalado.
pause

:end
endlocal
