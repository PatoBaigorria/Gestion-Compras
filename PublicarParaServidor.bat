@echo off
echo ========================================
echo   COMPILANDO APLICACION PARA SERVIDOR
echo ========================================
echo.

echo [1/3] Limpiando compilaciones anteriores...
dotnet clean -c Release

echo.
echo [2/3] Compilando aplicacion...
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:IncludeNativeLibrariesForSelfExtract=true

echo.
echo [3/3] Copiando appsettings.json...
copy /Y appsettings.json bin\Release\net8.0\win-x64\publish\appsettings.json

echo.
echo ========================================
echo   PUBLICACION COMPLETADA EXITOSAMENTE
echo ========================================
echo.
echo Carpeta generada: bin\Release\net8.0\win-x64\publish\
echo.
echo ========================================
echo   INSTRUCCIONES PARA COPIAR AL SERVIDOR
echo ========================================
echo.
echo IMPORTANTE: Debes copiar TODA la carpeta publish completa
echo.
echo 1. Abre la carpeta: bin\Release\net8.0\win-x64\publish\
echo.
echo 2. Selecciona TODO el contenido (Ctrl+A):
echo    - Gestion-Compras.exe (136 KB - launcher)
echo    - Todas las DLLs (44+ archivos)
echo    - Carpeta wwwroot\ (con archivos PWA)
echo    - appsettings.json
echo    - Todos los demas archivos
echo.
echo 3. Copia todo al servidor (Ctrl+C)
echo.
echo 4. En el servidor, pega en la carpeta de la aplicacion
echo    y reemplaza todos los archivos
echo.
echo 5. Ejecuta Gestion-Compras.exe en el servidor
echo.
echo ========================================
echo   ARCHIVOS PWA INCLUIDOS
echo ========================================
echo - manifest.json (configuracion PWA)
echo - service-worker.js (cache offline)
echo - offline.html (pagina sin conexion)
echo - wwwroot\icons\ (8 iconos PNG)
echo.
echo La aplicacion ahora es una PWA instalable!
echo.
pause