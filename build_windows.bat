@echo off
echo ========================================================
echo   Building FileCatalog for Windows (win-x64)
echo   Mode: Release, Self-Contained, Trimmed, Single-File
echo ========================================================

dotnet publish -c Release -r win-x64 --self-contained true -p:PublishTrimmed=true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ./publish/windows

echo.
echo Build finished! Check the 'publish\windows' directory.
pause