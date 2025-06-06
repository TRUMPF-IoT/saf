﻿// SPDX-FileCopyrightText: 2017-2025 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Common;

public interface IOutputRouteTranslator
{
    string TranslateRoute(string routePattern);
}
