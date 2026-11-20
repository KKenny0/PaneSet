@echo off
setlocal
set "PANESET_TRIAL_DOTNET=%~dp0..\..\.tools\dotnet\dotnet.exe"
set "PANESET_TRIAL_ARTIFACTS=%TEMP%\PaneSet-SelectionTrial-build-%RANDOM%"
"%PANESET_TRIAL_DOTNET%" build "%~dp0SelectionTrial.csproj" -c Release --artifacts-path "%PANESET_TRIAL_ARTIFACTS%" --nologo
if errorlevel 1 (
  pause
  exit /b 1
)
"%PANESET_TRIAL_DOTNET%" "%PANESET_TRIAL_ARTIFACTS%\bin\SelectionTrial\release\SelectionTrial.dll"
