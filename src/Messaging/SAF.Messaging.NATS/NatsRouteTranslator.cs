// SPDX-FileCopyrightText: 2017-2025 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using SAF.Common;

namespace SAF.Messaging.Nats;

public class NatsRouteTranslator : IRouteTranslator
{
    public string TranslateRoute(string routePattern)
    {
        if (string.IsNullOrWhiteSpace(routePattern))
        {
            return routePattern;
        }

        var transformedRoute = new char[routePattern.Length];
        var routePatternCharSpan = routePattern.AsSpan();
        for (var index = 0; index < routePatternCharSpan.Length; index++)
        {
            var hasNextChar = index + 1 < routePatternCharSpan.Length;
            transformedRoute[index] = routePatternCharSpan[index] switch
            {
                '/' => '.',
                '*' when !hasNextChar => '>',
                _ => routePatternCharSpan[index]
            };
        }

        return new string(transformedRoute);
    }
}
