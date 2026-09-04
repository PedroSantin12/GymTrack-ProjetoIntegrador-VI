@echo off
setlocal

set "SOLUCAO=%~dp0GymTrack.sln"

if not exist "%SOLUCAO%" (
    echo Nao foi possivel encontrar GymTrack.sln em:
    echo %~dp0
    pause
    exit /b 1
)

start "" "%SOLUCAO%"
