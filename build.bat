@echo off
echo ========================================================
echo   TUH Wristband Printer - Build Script (.NET 4.0 x86)
echo ========================================================
C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe /nologo /codepage:65001 /utf8output /target:winexe /platform:x86 /out:bin\TUH_WristbandPrinter.exe /r:bin\AbsTools.Srv.dll,bin\DataWindow.dll,bin\AbsTools.CSUI.dll,bin\AbsTools.SrvCliNmSpace.dll,bin\zxing.dll,System.dll,System.Windows.Forms.dll,System.Drawing.dll,System.Security.dll,System.Runtime.Remoting.dll,System.Web.Extensions.dll /win32icon:app_icon.ico *.cs
if %ERRORLEVEL% EQU 0 (
    echo.
    echo [SUCCESS] Build completed successfully: bin\TUH_WristbandPrinter.exe
) else (
    echo.
    echo [ERROR] Build failed! Check compiler errors above.
)
