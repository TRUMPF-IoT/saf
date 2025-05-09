namespace SAF.Common;

public static class CharUtilities
{
    public static string CharReplacerFunc(string source, Func<char, bool, char> charReplacementFunc)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return source;
        }

        var transformedRoute = new char[source.Length];
        var routePatternCharSpan = source.AsSpan();
        for (var index = 0; index < routePatternCharSpan.Length; index++)
        {
            var hasNextChar = index + 1 < routePatternCharSpan.Length;
            transformedRoute[index] = charReplacementFunc(source[index], hasNextChar);
        }

        return new string(transformedRoute);
    }
}
