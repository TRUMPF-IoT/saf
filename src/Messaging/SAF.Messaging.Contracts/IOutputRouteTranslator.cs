// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Contracts;

public interface IOutputRouteTranslator
{
    string TranslateRoute(string routePattern);
}

