@echo off
REM Build the PurePad solution. Forwards args to build.ps1 (e.g. build.cmd -Test).
where pwsh >nul 2>nul && (
    pwsh -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1" %*
) || (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1" %*
)
