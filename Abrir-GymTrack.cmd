@echo off
setlocal

set "GYMTRACK_DRIVE=M:"

if exist "%GYMTRACK_DRIVE%\GymTrack.sln" goto open_solution

subst %GYMTRACK_DRIVE% "%~dp0" >nul
if errorlevel 1 (
    echo Nao foi possivel criar o caminho temporario %GYMTRACK_DRIVE%.
    echo Verifique se essa letra de unidade ja esta em uso.
    pause
    exit /b 1
)

:open_solution
start "" "%GYMTRACK_DRIVE%\GymTrack.sln"
