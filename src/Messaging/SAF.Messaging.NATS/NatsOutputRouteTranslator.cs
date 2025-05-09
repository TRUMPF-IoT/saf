using SAF.Common;

namespace SAF.Messaging.Nats;

public class NatsOutputRouteTranslator : IOutputRouteTranslator
{
    public string TranslateRoute(string routePattern)
    {
        return CharUtilities.CharReplacerFunc(routePattern, (routePatternChar, hasNextChar) =>
            routePatternChar switch
            {
                '.' => '/',
                '>' when !hasNextChar => '*',
                _ => routePatternChar,
            });

    }
}
