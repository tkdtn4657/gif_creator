@echo off
rem =========================================================
rem  GifCreator build script
rem  Uses csc.exe bundled with .NET Framework 4.x, which is
rem  preinstalled on Windows 10/11 - no SDK required.
rem =========================================================
setlocal

set FW=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319
if not exist "%FW%\csc.exe" set FW=%WINDIR%\Microsoft.NET\Framework\v4.0.30319
if not exist "%FW%\csc.exe" (
    echo [ERROR] .NET Framework 4.x csc.exe not found.
    exit /b 1
)

if not exist "%~dp0dist" mkdir "%~dp0dist"

"%FW%\csc.exe" /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 ^
  /out:"%~dp0dist\GifCreator.exe" ^
  /win32manifest:"%~dp0src\app.manifest" ^
  /r:"%FW%\WPF\PresentationCore.dll" ^
  /r:"%FW%\WPF\WindowsBase.dll" ^
  /r:"%FW%\System.Xaml.dll" ^
  "%~dp0src\GifCreator.cs"

if errorlevel 1 (
    echo [ERROR] build failed.
    exit /b 1
)
echo [OK] built: dist\GifCreator.exe
endlocal
