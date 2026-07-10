// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

using SAF.Common;
using SAF.Messaging.Contracts;

namespace SAF.Messaging.Nats;

public class NatsInputRouteTranslator : IInputRouteTranslator
{
    public string TranslateRoute(string routePattern)
    {
        return CharUtilities.CharReplacerFunc(routePattern, (routePatternChar, hasNextChar) =>
            routePatternChar switch
            {
                '/' => '.',
                '*' when !hasNextChar => '>',
                _ => routePatternChar,
            });
    }
}


