
@echo off

setlocal
:: 현재 관리자 권한 확인.  관리자 권한이 아니면, 관리자 권한으로 자동 재실행.
net session >nul 2>&1
if %errorLevel% == 0 (
    echo In administrator mode
) else (
    echo Not in administrator mode.  switching to administrator mode...
    
    rem 관리자 권한으로 스크립트 다시 실행
    powershell -Command "Start-Process '%0' -ArgumentList '-runas' -Verb RunAs"
    
    exit
)

:: 이후부터는 관리자 권한이 확보된 상태에서 실행됩니다.


:: Chocolatey가 이미 설치되었는지 확인
if not exist "%ALLUSERSPROFILE%\chocolatey" (
    rem Chocolatey 설치 명령어 실행
    @"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy Bypass -Command "iex ((New-Object System.Net.WebClient).DownloadString('https://chocolatey.org/install.ps1'))"
    
    rem Chocolatey가 제대로 설치되었는지 다시 확인
    if exist "%ALLUSERSPROFILE%\chocolatey" (
        echo Succeeded installing Chocolatey
    ) else (
        echo Failed to install Chocolatey
    )
) else (
    echo Already installed Chocolatey
)

echo Installing GNU make..
choco install make -y

echo Finsihed..
pause