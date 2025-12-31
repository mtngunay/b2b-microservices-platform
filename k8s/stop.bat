@echo off
title B2B Kubernetes Stack - Stop
cd /d "%~dp0"
powershell -ExecutionPolicy Bypass -File "stop-all.ps1"
pause
