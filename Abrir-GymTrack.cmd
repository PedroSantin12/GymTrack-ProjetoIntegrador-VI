@echo off
setlocal

set "SOLUCAO=%~dp0GymTrack.sln"
set "GYMTRACK_DRIVE="
for %%I in ("%~dp0.") do set "GYMTRACK_DIR=%%~fI"

if not exist "%SOLUCAO%" (
    echo Nao foi possivel encontrar GymTrack.sln em:
    echo %~dp0
    pause
    exit /b 1
)

for /f "tokens=1,2,*" %%A in ('subst') do (
    for %%D in (M N P Q R S T U V W X Y Z) do (
        if /I "%%A"=="%%D:\:" (
            for %%I in ("%%C\.") do (
                if /I "%%~fI"=="%GYMTRACK_DIR%" set "GYMTRACK_DRIVE=%%D:"
            )
        )
    )
)

for %%D in (M N P Q R S T U V W X Y Z) do (
    if not defined GYMTRACK_DRIVE (
        subst %%D: "%~dp0" >nul 2>&1
        if not errorlevel 1 set "GYMTRACK_DRIVE=%%D:"
    )
)

if not defined GYMTRACK_DRIVE (
    echo Nao foi possivel criar um caminho temporario para o projeto.
    echo Verifique se as letras de unidade M a Z ja estao em uso.
    pause
    exit /b 1
)

start "" "%GYMTRACK_DRIVE%\GymTrack.sln"
