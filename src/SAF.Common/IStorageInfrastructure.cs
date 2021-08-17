// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Common
{
    /// <summary>
    /// Provides access to a storing infrastrucure to store data temporarily or permanently
    /// </summary>
    public interface IStorageInfrastructure
    {
        /// <summary>
        /// Sets a string value under a key in a global area. 
        /// </summary>
        /// <param name="key">The key</param>
        /// <param name="value">The value</param>
        /// <returns>The storage infrastructure to provide a fluid workflow.</returns>
        IStorageInfrastructure Set(string key, string value);

        /// <summary>
        /// Sets a string value under a key in a specific area
        /// </summary>
        /// <param name="area">The area</param>
        /// <param name="key">The key</param>
        /// <param name="value">The value</param>
        /// <returns>The storage infrastructure to provide a fluid workflow.</returns>
        IStorageInfrastructure Set(string area, string key, string value);

        /// <summary>
        /// Sets a byte array value under a key in a global area
        /// </summary>
        /// <param name="key">The key</param>
        /// <param name="value">The value</param>
        /// <returns>The storage infrastructure to provide a fluid workflow.</returns>
        IStorageInfrastructure Set(string key, byte[] value);

        /// <summary>
        /// Sets a byte array value under a key in a specific area
        /// </summary>
        /// <param name="area">The area</param>
        /// <param name="key">The key</param>
        /// <param name="value">The value</param>
        /// <returns>The storage infrastructure to provide a fluid workflow.</returns>
        IStorageInfrastructure Set(string area, string key, byte[] value);

        /// <summary>
        /// Returns the value that is saved under the key as a string. Returns null if not found.
        /// </summary>
        /// <param name="key">The Key</param>
        /// <returns>The value as a string saved under the key. Null if not found.</returns>
        string GetString(string key);

        /// <summary>
        /// Returns the value that is saved under the key in a specific area as a string. Returns null if not found.
        /// </summary>
        /// <param name="area">The area</param>
        /// <param name="key">The Key</param>
        /// <returns>The value as a string saved under the key. Null if not found.</returns>
        string GetString(string area, string key);

        /// <summary>
        /// Returns the value that is saved under the key as a byte array. Returns null if not found.
        /// </summary>
        /// <param name="key">The Key</param>
        /// <returns>The value as a byte array saved under the key. Null if not found.</returns>
        byte[] GetBytes(string key);

        /// <summary>
        /// Returns the value that is saved under the key in a specific area as a byte array. Returns null if not found.
        /// </summary>
        /// <param name="area">The area</param>
        /// <param name="key">The Key</param>
        /// <returns>The value as a byte array saved under the key. Null if not found.</returns>
        byte[] GetBytes(string area, string key);

        /// <summary>
        /// Removes the key with its value from the global area. 
        /// </summary>
        /// <param name="key">The key</param>
        /// <returns> The storage infrastructure to provide a fluid workflow.</returns>
        IStorageInfrastructure RemoveKey(string key);

        /// <summary>
        /// Removes the key with its value from a specific area. 
        /// </summary>
        /// <param name="area">The area</param>
        /// <param name="key">The key</param>
        /// <returns>The storage infrastructure to provide a fluid workflow.</returns>
        IStorageInfrastructure RemoveKey(string area, string key);

        /// <summary>
        /// Removes a specific area and all key value pairs contained in that area. 
        /// </summary>
        /// <param name="area">The area</param>
        /// <returns>The storage infrastructure to provide a fluid workflow.</returns>
        IStorageInfrastructure RemoveArea(string area);
    }
}
