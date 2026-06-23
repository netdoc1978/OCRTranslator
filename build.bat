@echo off
setlocal

set CSC_PATH=C:\Windows\Microsoft.NET\Framework\v4.0.30319\Csc.exe
set OUTPUT_DIR=bin\Release

if not exist "%OUTPUT_DIR%" mkdir "%OUTPUT_DIR%"

echo Compiling OCRTranslator (x86)...

"%CSC_PATH%" /nologo /target:winexe /platform:x86 /out:"%OUTPUT_DIR%\OCRTranslator.exe" /utf8output /warn:4 /optimize /unsafe Program.cs MainForm.cs BaiDuOCRHelper.cs BaiduTranslateHelper.cs ScreenCaptureHelper.cs

if %ERRORLEVEL% EQU 0 (
    echo Build OCRTranslator successful!
) else (
    echo Build OCRTranslator failed!
)

pause
