// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System.Diagnostics;
using System.Text.Json;
using SAF.Common;

namespace SAF.DevToolbox.TestRunner;

internal class TestSequenceTracer : IDisposable
{
    private readonly string _testSequenceName;
    private readonly TestSequenceBase _testSequence;
    private readonly StreamWriter _writer;
    private readonly Stopwatch _sw = new();

    public TestSequenceTracer(string basePath, string testSequenceName, TestSequenceBase testSequence)
    {
        _testSequenceName = testSequenceName;
        _testSequence = testSequence;

        var path = Path.Combine(Environment.CurrentDirectory, basePath, testSequenceName + ".md");
        _writer = File.CreateText(path);

        _sw.Start();
        _writer.WriteLine($"# {testSequenceName}");
        _writer.WriteLine();
    }

    public void MessagingTrace(Message message)
    {
        var stackTrace = new StackTrace(true);
        var frames = stackTrace.GetFrames();
        if (frames == null || frames.Length < 4) return;
        var firstNonInfrastructureFrame = frames[3];
        var method = firstNonInfrastructureFrame.GetMethod();

        lock (_writer)
        {
            _writer.WriteLine("### Message");
            _writer.WriteLine($"_{_sw.Elapsed}_  ");
            _writer.WriteLine($"> {method?.DeclaringType?.FullName} - {method}  ");
            _writer.WriteLine($"> {firstNonInfrastructureFrame.GetFileName()}:{firstNonInfrastructureFrame.GetFileLineNumber()}  ");
            _writer.WriteLine();
            _writer.WriteLine($"Topic: {message.Topic}  ");
            WritePayload(message.Payload ?? string.Empty);
            _writer.WriteLine("```json");
            _writer.WriteLine();
            _writer.WriteLine("```");
            _writer.WriteLine("---");
        }
    }

    private void WritePayload(string payload)
    {
        try
        {
            var prettyJson = PrettyPrintJson(payload);
            _writer.WriteLine("```json");
            _writer.WriteLine(prettyJson);
            _writer.WriteLine("```");
        }
        catch
        {
            // in case of any de-/serialization exceptions write payload itself
            _writer.WriteLine("```");
            _writer.WriteLine(payload);
            _writer.WriteLine("```");
        }
    }

    public void TitleTrace(string title)
    {
        lock (_writer)
        {
            _writer.WriteLine($"## {title}");
            _writer.WriteLine();
        }
    }

    public void DocumentationTrace(string title, string trace)
    {
        lock (_writer)
        {
            if (!string.IsNullOrEmpty(title))
            {
                _writer.WriteLine($"### {title}");
                _writer.WriteLine($"_{_sw.Elapsed}_  ");
                _writer.WriteLine();
            }

            _writer.WriteLine(trace);
            _writer.WriteLine("---");
        }
    }

    public void TestSequenceSuccessful()
    {
        lock (_writer)
        {
            _writer.WriteLine("__Sequence finished successful__  ");
            _writer.WriteLine($"_{_sw.Elapsed}_");
        }
    }

    public void TestSequenceFailed(Exception ex)
    {
        lock (_writer)
        {
            _writer.WriteLine($"### Sequence failed");
            _writer.WriteLine($"_{_sw.Elapsed}_  ");
            _writer.WriteLine();
            _writer.WriteLine($"{ex.GetType().FullName}: {ex.Message}  ");
            _writer.WriteLine("```");
            _writer.WriteLine(ex.StackTrace);
            _writer.WriteLine("```");
            _writer.Flush();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing) return;

        _writer.Close();
        _writer.Dispose();
    }

    private static string PrettyPrintJson(string json)
    {
        // wastes some time, but it's just for testing.
        var parsedJson = JsonSerializer.Deserialize<object>(json);
        if (parsedJson == null) return json;

        return JsonSerializer.Serialize(parsedJson, new JsonSerializerOptions{WriteIndented = true});
    }
}