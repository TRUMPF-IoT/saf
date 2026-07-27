// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace CdeLogSorter;

internal class Program
{
    static void Main(string[] args)
    {
        foreach (var v in args)
        {
            Sorter sorter = new(v);
            sorter.Sort();
        }
    }
}