// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Cde.Common;
using nsCDEngine.BaseClasses;

/// <summary>
/// Maps the <see cref="CdeConfiguration"/> to C-DEngine arguments. An additional argument with the name of an
/// argument set here is ignored.
/// </summary>
internal sealed class CdeStartArguments : ICdeStartArguments
{
    public IDictionary<string, string> Create(CdeConfiguration configuration)
    {
        var arguments = new Dictionary<string, string>
        {
            { "DontVerifyTrust", $"{configuration.DontVerifyTrust}" },
            { "UseUserMapper", $"{configuration.UseUserMapper}" },
            { "UseRandomDeviceID", $"{configuration.UseRandomDeviceId}" },
            { "UseStrictJSON", $"{configuration.UseStrictJson}" },
            { "AROLE", eEngineName.NMIService + ";" + eEngineName.ContentService },
            { "SROLE", eEngineName.NMIService + ";" + eEngineName.ContentService }
        };

        if (!string.IsNullOrEmpty(configuration.ScopeId))
            arguments["EasyScope"] = configuration.ScopeId;

        return MergeDictionaries(arguments, configuration.AdditionalArguments);
    }

    private static Dictionary<string, string> MergeDictionaries(Dictionary<string, string> dict1, IDictionary<string, string> dict2)
    {
        return dict1
            .Concat(dict2.Where(kvp => !dict1.ContainsKey(kvp.Key)))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }
}
