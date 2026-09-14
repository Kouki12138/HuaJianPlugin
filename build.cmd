@echo off
setlocal
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set ACD=D:\AutoCAD 2024
set OUT=HuaJianPlugin.dll

if not exist "%ACD%\AcMgd.dll" (
    echo ERROR: AutoCAD 2024 not found at %ACD%
    echo Edit ACD in this file if your path differs.
    exit /b 1
)

"%CSC%" /nologo /target:library /platform:anycpu /codepage:65001 /out:%OUT% ^
  /r:"%ACD%\AcMgd.dll" /r:"%ACD%\AcDbMgd.dll" /r:"%ACD%\AcCoreMgd.dll" ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
  SplineGenerator.cs

if errorlevel 1 goto :fail

if not exist "HuaJianPlugin.bundle\Contents\Windows" mkdir "HuaJianPlugin.bundle\Contents\Windows"
copy /y "%OUT%" "HuaJianPlugin.bundle\Contents\Windows\%OUT%" >nul

rem ---- build standalone setup exe (embeds plugin dll + manifest) ----
"%CSC%" /nologo /target:winexe /codepage:65001 /out:HuaJianPluginSetup.exe ^
  /res:"%OUT%",HuaJianPlugin.dll ^
  /res:"HuaJianPlugin.bundle\PackageContents.xml",PackageContents.xml ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
  Setup.cs
if errorlevel 1 goto :fail

echo.
echo BUILD OK: %OUT%
echo SETUP OK: HuaJianPluginSetup.exe
echo Use: run HuaJianPluginSetup.exe to install
echo Or: APPLOAD -^> NETLOAD -^> select %OUT%
exit /b 0

:fail
echo BUILD FAILED.
exit /b 1
