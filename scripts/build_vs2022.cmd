@echo off
setlocal
call "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\Common7\Tools\VsDevCmd.bat"
if errorlevel 1 exit /b 1
msbuild "%~dp0..\OpenCode.Acp.sln" /t:Build /m /p:Configuration=Release /p:Platform=x64
exit /b %errorlevel%
