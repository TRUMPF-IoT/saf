// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

﻿
namespace SAF.Communication.PubSub
{
    public static class WildcardMatcher
    {
        private const char Single = '?';
        private const char Multiple = '*';
        private const char End = '\0';
        private const string All = "*"; // Use for short-circuit

        public static unsafe bool IsMatch(this string @string, string pattern)
        {
            // Optimize catch-all and equality
            if(pattern == All) return true;

            fixed(char* p = pattern)
            {
                fixed(char* s = @string)
                    return IsMatch(p, s);
            }
        }

        private static unsafe bool IsMatch(char* pattern, char* @string)
        {
            // Check if content is at end.
            if(*pattern == End)
                return *@string == End;

            // Check for single character missing or match.
            if(*pattern == Single || *pattern == *@string)
                return *@string != End
                       && IsMatch(pattern + 1, @string + 1);

            // Check for multiple character missing.
            if(*pattern == Multiple)
                return IsMatch(pattern + 1, @string)
                       || *@string != End
                       && IsMatch(pattern, @string + 1);

            return false;
        }
    }
}