@echo off
echo Compilando aplicacion para servidor...
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:IncludeNativeLibrariesForSelfExtract=true

echo Copiando appsettings.json...
copy /Y appsettings.json bin\Release\net8.0\win-x64\publish\appsettings.json

echo.
echo ========================================
echo Publicacion completada!
echo Carpeta: bin\Release\net8.0\win-x64\publish\
echo ========================================
echo.
echo INSTRUCCIONES:
echo 1. Copiar solo 2 archivos:
echo    - Gestion-Compras.exe (~75 MB)
echo    - appsettings.json
echo 2. Pegar en el servidor (C:\publish\)
echo 3. Reemplazar archivos anteriores
echo 4. Reiniciar la aplicacion en el servidor
echo.
pause