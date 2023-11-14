// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0


using System.Security.Cryptography;
using System.Text;
using SAF.Toolbox.Serialization;

namespace SAF.Toolbox.FileTransfer;

public class TransportFile
{
    private readonly MD5 _md5 = MD5.Create();

    internal string Content { get; set; } = string.Empty;
    internal long OriginalLength { get; set; }

    public string Name { get; }
    public string? MimeType { get; }
    public IDictionary<string, string>? Properties { get; }

    public TransportFile(string name, string? mimeType = null, IDictionary<string, string>? properties = null)
    {
        Name = !string.IsNullOrEmpty(name) ? name : throw new ArgumentException(nameof(name));
        MimeType = mimeType;
        Properties = properties;
    }

    public void ReadFrom(Stream stream) => (Content, OriginalLength) = Encode(stream);

    public void WriteTo(Stream stream) => Decode(stream, Content);

    public IDictionary<string, string> ToSerializableProperties()
    {
        var properties = new Dictionary<string, string>();

        if (Properties != null)
            foreach (var kvp in Properties)
                properties.Add(kvp.Key, kvp.Value);

        properties["Name"] = Name;
        if(!string.IsNullOrEmpty(MimeType))
            properties["MimeType"] = MimeType;
        properties["Content"] = Content;
        properties["OriginalLength"] = $"{OriginalLength}";

        using var ms = new MemoryStream();
        Decode(ms, Content);
        properties["Fingerprint"] = GenerateFingerprint(ms);

        return properties;
    }

    public override string ToString() => JsonSerializer.Serialize(ToSerializableProperties());

    public bool Verify()
    {
        if (Properties == null) return false;

        var hasFingerprint = Properties.TryGetValue("Fingerprint", out var fingerprint);

        if (!hasFingerprint) return false;

        using var ms = new MemoryStream();
        Decode(ms, Content);
        var hash = GenerateFingerprint(ms);
        return StringComparer.OrdinalIgnoreCase.Compare(hash, fingerprint) == 0;
    }

    private static (string encodedContent, long originalLength) Encode(Stream stream)
    {
        using var ms = new MemoryStream();
        using var cs = new CryptoStream(ms, new ToBase64Transform(), CryptoStreamMode.Write);
        stream.CopyTo(cs);
        var originalLength = stream.Position;

        cs.FlushFinalBlock();
        ms.Flush();
        ms.Seek(0, SeekOrigin.Begin);
        var reader = new StreamReader(ms);
        return (reader.ReadToEnd(), originalLength);
    }

    private static void Decode(Stream stream, string content)
    {
        if (string.IsNullOrEmpty(content)) return;

        using var ms = new MemoryStream(Encoding.ASCII.GetBytes(content));
        using var cs = new CryptoStream(ms, new FromBase64Transform(), CryptoStreamMode.Read);
        cs.CopyTo(stream);
        stream.Flush();
        stream.Seek(0, SeekOrigin.Begin);
    }

    private string GenerateFingerprint(Stream stream)
    {
        var data = _md5.ComputeHash(stream);
        var sb = new StringBuilder();

        foreach (var b in data)
            sb.Append(b.ToString("x2"));

        return sb.ToString();
    }
}