// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Common;

public interface IInputRouteTranslator
{
    string TranslateRoute(string routePattern);
}
