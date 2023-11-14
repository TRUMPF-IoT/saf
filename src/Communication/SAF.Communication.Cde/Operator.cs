// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.Engines.ThingService;
using SAF.Communication.Cde.ConnectionTypes;

namespace SAF.Communication.Cde;

public static class Operator
{
    public static ComLine GetLine(TheThing thing)
    {
        return new DefaultComLine(thing);
    }

    public static ComLine GetLine(TheThing thing, string address, string scope)
    {
        return new AdvancedComLine(thing, address, scope);
    }
}