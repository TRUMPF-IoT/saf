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
                int indexSnStart = theString.IndexOf(" SN:") + 4;
                int indexSnEnd = theString.IndexOf(" ", indexSnStart);
                string snString = theString[indexSnStart..indexSnEnd];
                return Convert.ToInt32(snString);
            }
            return -1;
        }

        public static DateTime Time(this string theString)
        {
            if (theString.Contains(" : "))
            {
                int indexTimeEnd = theString.IndexOf(" : ");
                string timeString = theString[(indexTimeEnd - 23)..indexTimeEnd];
                return DateTime.Parse(timeString);
            }
            return new DateTime(2000, 1, 1, 0, 0, 0);
        }

        public static void SeparatePraefix(this List<string> theList, int index)
        {
            string line = theList[index];
            string praefix = line.Substring(0, line.IndexOf("ID:"));
            line = line.Substring(line.IndexOf("ID:"));
            theList[index] = line;
            theList[index + 1] = praefix + theList[index + 1];
        }
    }
}
