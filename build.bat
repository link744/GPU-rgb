@echo off
cd /d %~dp0
set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
"%CSC%" /nologo -optimize+ ^
  -reference:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Core.dll" ^
  -reference:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Drawing.dll" ^
  -reference:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Web.Extensions.dll" ^
  -out:GpuRgb.exe src\GpuRgb.cs src\Patterns.cs
if %errorlevel%==0 (echo BUILD OK) else (echo BUILD FAILED)
