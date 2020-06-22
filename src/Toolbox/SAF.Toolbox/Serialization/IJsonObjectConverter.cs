// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System;

namespace SAF.Toolbox.Serialization
{
    public interface IJsonObjectConverter
    {
        bool CanRead { get; }
        bool CanWrite { get; }        

        bool CanConvert(Type objectType);
        string SerializeObject(object sourceObject);
        object DeserializeObject(Type objectType, string jsonObject);
    }

    public interface IJsonObjectConverter<T> : IJsonObjectConverter
    {
        string SerializeObject(T sourceObject);
        T DeserializeObject(string jsonObject);
    }
}