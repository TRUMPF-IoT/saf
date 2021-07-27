:: SPDX-FileCopyrightText: 2017-2021 TRUMPF Laser GmbH
:: SPDX-License-Identifier: MPL-2.0
:: Kill the running services and revise the log files.
:: This file had to be executed with administrator privilegs.
echo off
echo Stop the services.
taskkill /f /im TestRunnerCdeService2.exe
:: For some reason the following exe has also already been terminated.
::taskkill /f /im TestRunnerCdeService1.exe
pause
echo Revise the log files.
"%~dp0src\Utils\CdeLogSorter\bin\Debug\netcoreapp3.1\CdeLogSorter.exe" %USERPROFILE%\Documents\Service2_01.log %USERPROFILE%\Documents\Service1_01.log
pause