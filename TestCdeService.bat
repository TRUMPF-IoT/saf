:: SPDX-FileCopyrightText: 2017-2021 TRUMPF Laser GmbH
:: SPDX-License-Identifier: MPL-2.0
:: Start file for the Ping/Pong example between two CDE-services.
:: This file had to be executed with administrator privilegs.
:: The sender (service2) must be started first to avoid error messages.
start "" /b /d "%~dp0\src\TestRunnerCdeService2\bin\Debug\netcoreapp3.1" "%~dp0\src\TestRunnerCdeService2\bin\Debug\netcoreapp3.1\TestRunnerCdeService2.exe" > %USERPROFILE%\Documents\Service2_01.log
start "" /b /d "%~dp0\src\TestRunnerCdeService1\bin\Debug\netcoreapp3.1" "%~dp0\src\TestRunnerCdeService1\bin\Debug\netcoreapp3.1\TestRunnerCdeService1.exe" > %USERPROFILE%\Documents\Service1_01.log
