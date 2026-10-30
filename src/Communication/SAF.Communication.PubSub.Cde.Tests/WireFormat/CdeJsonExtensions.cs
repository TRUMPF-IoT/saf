// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Communication.PubSub.Cde.Tests.WireFormat;

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>
/// C-DEngine 7 escapes quotes, HTML characters and every non-ASCII character inside strings as <c>\uXXXX</c>,
/// C-DEngine 6 wrote them literally. A peer reads the same values from both, so the tests compare the JSON in one
/// canonical spelling: property names and order, value kinds and omitted properties still have to match.
/// </summary>
internal static class CdeJsonExtensions
{
    private static readonly JsonSerializerOptions CanonicalOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static string ToCanonicalJson(this string json) => JsonNode.Parse(json)!.ToJsonString(CanonicalOptions);
}
