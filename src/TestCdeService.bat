:: SPDX-FileCopyrightText: 2017-2021 TRUMPF Laser GmbH
:: SPDX-License-Identifier: MPL-2.0
:: Start file for the Ping/Pong example between two CDE-services.
:: This file had to be executed with administrator privilegs.
:: The sender (service2) must be started first to avoid error messages.
start "" /d "%~dp0\TestRunnerCdeService2\bin\Debug\netcoreapp3.1" "%~dp0\TestRunnerCdeService2\bin\Debug\netcoreapp3.1\TestRunnerCdeService2.exe"
start "" /d "%~dp0\TestRunnerCdeService1\bin\Debug\netcoreapp3.1" "%~dp0\TestRunnerCdeService1\bin\Debug\netcoreapp3.1\TestRunnerCdeService1.exe"
