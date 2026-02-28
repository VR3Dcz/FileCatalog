@echo off
echo ========================================================
echo   Building FileCatalog for Linux (linux-x64)
echo   Mode: Release, Self-Contained, Trimmed, Single-File
echo ========================================================

dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishTrimmed=true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ./publish/linux

echo.
echo Build finished! Check the 'publish\linux' directory.
pause