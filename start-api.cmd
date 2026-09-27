@echo off
rem ---------------------------------------------------------------
rem Start the local YinYanMusic API for development.
rem
rem Why this script exists (both are real traps):
rem   1) Running the exe directly without --environment Development
rem      skips appsettings.Development.json -> DB login fails (28P01).
rem   2) Without --urls 0.0.0.0 the server only binds localhost, and a
rem      physical Android phone can NEVER reach it (use adb reverse or
rem      the LAN IP instead).
rem Keep the window open while using the app; closing it stops the API.
rem ---------------------------------------------------------------
setlocal
set "BIN=%~dp0src\YinYanMusic.Api\bin\Debug\net10.0"

if not exist "%BIN%\YinYanMusic.Api.exe" (
  echo [ERROR] Not found: %BIN%\YinYanMusic.Api.exe
  echo Build it first:  dotnet build src\YinYanMusic.Api\YinYanMusic.Api.csproj
  pause
  exit /b 1
)

cd /d "%BIN%"
echo Starting API on http://0.0.0.0:5116 (Development) ...
echo Close this window to stop the service.
YinYanMusic.Api.exe --environment Development --urls http://0.0.0.0:5116
pause
