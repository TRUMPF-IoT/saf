// SPDX-FileCopyrightText: 2017-2021 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace CdeLogSorter;

/// <summary>
/// Offers the possibility to sort a C-DEngine log-file. These log-files contains two kinds of entries.
/// Log entries with and without SN-Numer. The SN-Number reflects the order of the entries. Example of a log entry:
/// ID:1 SN:5 2021-07-26 13:55:35.377 : /5/l3_ImportantMessage : BaseAssets : 79019ed8-378f-47c2-b2cd-1b7bc1657d81 ...
/// </summary>
public class Sorter
{
    private readonly string _filename;

    public Sorter(string filename)
    {
        _filename = filename;
    }

    public void Sort()
    {
        // Prepare the lines before sorting.
        List<string> lines = new(File.ReadAllLines(_filename));
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var snNum = line.SnNumber();
            if (snNum > 0)
            {
                if (!line.StartsWith("ID:"))
                {
                    // Two overlapping logging issues must be separated.
                    lines.SeparatePraefix(i);
                }
                // Search all lines that occured at almost the same time.
                var time = line.Time();
                var j = i + 1;
                while (j < lines.Count && (lines[j].SnNumber() < 0 || Math.Abs(lines[j].Time().Subtract(time).TotalMilliseconds) < 10))
                {
                    j++;
                }
                // Search all SN-Numbers which are less than the current and put these lines after the current line.
                // So they will still be considered in the main loop.
                var diff = j - (i + 1);
                for (var k = 1; k < diff; k++)
                {
                    var index = i + 1 + k;
                    if (lines[index].SnNumber() > 0 && lines[index].SnNumber() < snNum)
                    {
                        if (!lines[index].StartsWith("ID:"))
                        {
                            // Two overlapping logging issues must be separated.
                            lines.SeparatePraefix(index);
                        }
                        var move = lines[index];
                        lines.RemoveAt(index);
                        lines.Insert(i + 1, move);
                    }
                }
 
                // Format the ID
                var indexSn = lines[i].IndexOf(" SN:");
                if (indexSn < 8)
                {
                    lines[i] = lines[i].Replace("ID:", "ID:" + new string(' ', 8 - indexSn));
                }
            }
        }

        // Sort the lines
        List<string> result = new();
        var takenOverUntil = 0;
        while(takenOverUntil < lines.Count - 1)
        {
            takenOverUntil = this.SortStep1(lines.ToArray(), takenOverUntil, result);
        }
        FileInfo fi = new(_filename);
        File.WriteAllLines(_filename.Replace(fi.Extension, ".sort" + fi.Extension), result.ToArray());
    }

    /// <summary>
    /// Step one: append the lines which have no SN-Number.
    /// </summary>
    private int SortStep1(string[] lines, int start, List<string> result)
    {
        var takenOverUntil = start;
        for (var i = start; i < lines.Length; i++)
        {
            takenOverUntil = i;
            if (!lines[i].Contains(" SN:"))
            {
                result.Add(lines[i]);
            }
            else
            {
                break;
            }
        }
        return this.SortStep2(lines, takenOverUntil, result);
    }

    /// <summary>
    /// Step two: append the lines which have a SN-Number sorted by this number.
    /// </summary>
    private int SortStep2(string[] lines, int start, List<string> result)
    {
        var takenOverUntil = start;
        Dictionary<int, string> dicLines = new();
        for (var i = start; i < lines.Length; i++)
        {
            takenOverUntil = i;
            var line = lines[i];
            var snNumber = line.SnNumber();
            if (snNumber > 0)
            {
                dicLines.Add(snNumber, line);
            }
            else
            {
                break;
            }
        }
        List<int> lstKeys = new(dicLines.Keys);
        lstKeys.Sort();
        foreach(var i in lstKeys)
        {
            result.Add(dicLines[i]);
        }
        return takenOverUntil;
    }

}