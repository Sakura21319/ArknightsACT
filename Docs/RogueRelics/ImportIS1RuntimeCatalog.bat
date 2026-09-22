@echo off
setlocal
set "PYTHONUNBUFFERED=1"
set "PYTHONUTF8=1"
cd /d "%~dp0..\.."
set "ROOT=%CD%"
set "SCRIPT=%ROOT%\Assets\_Game\Editor\Tools\import_is1_runtime_catalog.py"
set "XLSX=%ROOT%\Docs\RogueRelics\RogueRelicDatabase_IS1_CurrentPool.xlsx"
set "OUTPUT=%ROOT%\Assets\_Game\Resources\ScavengingCatalog.json"
set "ICONS=%ROOT%\Assets\_Game\Resources\RogueRelics\RuntimeIcons"
set "REPORT=%ROOT%\Logs\RogueRelicRuntimeImport.json"

set "PYTHON="
if exist "D:\Effect\.venv\Scripts\python.exe" set "PYTHON=D:\Effect\.venv\Scripts\python.exe"
if not defined PYTHON if exist "C:\ProgramData\Anaconda3\python.exe" set "PYTHON=C:\ProgramData\Anaconda3\python.exe"
if not defined PYTHON if exist "C:\Users\21613\anaconda3\python.exe" set "PYTHON=C:\Users\21613\anaconda3\python.exe"
if not defined PYTHON if exist "C:\Users\21613\AppData\Roaming\uv\python\cpython-3.12.14-windows-x86_64-none\python.exe" set "PYTHON=C:\Users\21613\AppData\Roaming\uv\python\cpython-3.12.14-windows-x86_64-none\python.exe"

if defined PYTHON goto run
where py >nul 2>nul
if not errorlevel 1 (
  set "PYTHON=py -3"
  goto run
)
where python >nul 2>nul
if not errorlevel 1 (
  set "PYTHON=python"
  goto run
)

echo Python not found.
exit /b 2

:run
echo Importing curated IS1 workbook into runtime catalog...
%PYTHON% -u "%SCRIPT%" "%ROOT%" "%XLSX%" "%OUTPUT%" "%ICONS%" "%REPORT%"
set "RC=%ERRORLEVEL%"
echo.
if not "%RC%"=="0" (
  echo Failed with exit code %RC%.
  exit /b %RC%
)
echo Done.
echo Runtime catalog: %OUTPUT%
echo Report: %REPORT%
endlocal
