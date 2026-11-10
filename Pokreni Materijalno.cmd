@echo off
cd /d "%~dp0"
if not exist "%~dp0artifacts\FoxApp\Materijalno.UI.exe" (
  echo Aplikacija nije pronadjena u artifacts\FoxApp.
  pause
  exit /b 1
)
start "" "%~dp0artifacts\FoxApp\Materijalno.UI.exe"
