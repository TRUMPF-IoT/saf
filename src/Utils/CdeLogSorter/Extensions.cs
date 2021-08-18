// SPDX-FileCopyrightText: 2017-2021 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Generic;

namespace CdeLogSorter
{
    public static class Extensions
    {
        public static int SnNumber(this string theString)
        {
            if (theString.Contains(" SN:"))
            {
                var indexSnStart = theString.IndexOf(" SN:") + 4;
                var indexSnEnd = theString.IndexOf(" ", indexSnStart);
                var snString = theString[indexSnStart..indexSnEnd];
                return Convert.ToInt32(snString);
            }
            return -1;
        }

        public static DateTime Time(this string theString)
        {
            if (theString.Contains(" : "))
            {
                var indexTimeEnd = theString.IndexOf(" : ");
                var timeString = theString[(indexTimeEnd - 23)..indexTimeEnd];
                return DateTime.Parse(timeString);
            }
            return new DateTime(2000, 1, 1, 0, 0, 0);
        }

        public static void SeparatePraefix(this List<string> theList, int index)
        {
            var line = theList[index];
            var praefix = line.Substring(0, line.IndexOf("ID:"));
            line = line.Substring(line.IndexOf("ID:"));
            theList[index] = line;
            theList[index + 1] = praefix + theList[index + 1];
        }
    }
}
