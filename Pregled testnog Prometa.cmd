@echo off
cd /d "%~dp0"
if not exist "%~dp0artifacts\PrometTest\Materijalno.UI.exe" (
  echo Testna aplikacija nije pronadjena u artifacts\PrometTest.
  pause
  exit /b 1
)
start "" "%~dp0artifacts\PrometTest\Materijalno.UI.exe"
