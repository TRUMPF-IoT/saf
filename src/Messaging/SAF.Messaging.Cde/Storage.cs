// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using nsCDEngine.Engines.StorageService;
using nsCDEngine.ViewModels;
using SAF.Common;

namespace SAF.Messaging.Cde
{
    internal class StorageEntry : TheDataBase
    {
        public string Key { get; set; } = default!;
        public string? Value { get; set; }
    }

    internal class Storage : IStorageInfrastructure, IDisposable
    {
        private readonly ILogger<Storage> _log;
        private const string GlobalArea = "global";

        private readonly ReaderWriterLockSlim _syncStorageAccess = new(LockRecursionPolicy.SupportsRecursion);
        private readonly Dictionary<string, TheStorageMirror<StorageEntry>> _openStorageAreas = new();

        private const int SaveStorageAreaIntervalSeconds = 5;

        public Storage(ILogger<Storage>? log)
        {
            _log = log ?? NullLogger<Storage>.Instance;
        }

        public IStorageInfrastructure Set(string key, string value) => Set(GlobalArea, key, value);

        public IStorageInfrastructure Set(string area, string key, string value)
        {
            _syncStorageAccess.EnterWriteLock();
            try
            {
                var storage = CreateOrGetStorageArea(area);

                key = key.ToLowerInvariant();
                var entry = storage.MyMirrorCache.GetEntryByFunc(e => string.Equals(e.Key, key, StringComparison.InvariantCultureIgnoreCase));
                if (entry == null)
                {
                    storage.AddAnItem(new StorageEntry { Key = key, Value = value });
                }
                else
                {
                    entry.Value = value;
                    storage.UpdateItem(entry);
                }
            }
            finally
            {
                _syncStorageAccess.ExitWriteLock();
            }

            return this;
        }

        public IStorageInfrastructure Set(string key, byte[] value) => Set(key, ByteArrayToString(value));

        public IStorageInfrastructure Set(string area, string key, byte[] value) => Set(area, key, ByteArrayToString(value));

        public string? GetString(string key) => GetString(GlobalArea, key);

        public string? GetString(string area, string key)
        {
            _syncStorageAccess.EnterReadLock();
            try
            {
                var storage = CreateOrGetStorageArea(area);
                key = key.ToLowerInvariant();
                var entry = storage.MyMirrorCache.GetEntryByFunc(e => string.Equals(e.Key, key, StringComparison.InvariantCultureIgnoreCase));
                return entry?.Value;
            }
            finally
            {
                _syncStorageAccess.ExitReadLock();
            }
        }

        public byte[]? GetBytes(string key) => GetBytes(GlobalArea, key);

        public byte[]? GetBytes(string area, string key) => StringToByteArray(GetString(area, key));

        public IStorageInfrastructure RemoveKey(string key) => RemoveKey(GlobalArea, key);

        public IStorageInfrastructure RemoveKey(string area, string key)
        {
            _syncStorageAccess.EnterUpgradeableReadLock();
            try
            {
                var storage = CreateOrGetStorageArea(area);

                key = key.ToLowerInvariant();
                var entry = storage.MyMirrorCache.GetEntryByFunc(e => string.Equals(e.Key, key, StringComparison.InvariantCultureIgnoreCase));
                if (entry == null) return this;

                _syncStorageAccess.EnterWriteLock();
                try
                {
                    storage.RemoveAnItem(entry, _ => { });
                }
                finally
                {
                    _syncStorageAccess.ExitWriteLock();
                }
            }
            finally
            {
                _syncStorageAccess.ExitUpgradeableReadLock();
            }

            return this;
        }

        public IStorageInfrastructure RemoveArea(string area)
        {
            area = area.ToLowerInvariant();
            if (area == GlobalArea)
                throw new NotSupportedException("It is not allowed to delete the global storage area.");

            _syncStorageAccess.EnterWriteLock();
            try
            {
                var storage = CreateOrGetStorageArea(area);
                storage.RemoveStore(false);
            }
            finally
            {
                _syncStorageAccess.ExitWriteLock();
            }

            return this;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if(!disposing) return;

            lock (_openStorageAreas)
            {
                foreach (var storage in _openStorageAreas.Values)
                {
                    storage.ForceSave();
                    storage.Cleanup();
                }
                _openStorageAreas.Clear();
            }
        }

        private static string ByteArrayToString(byte[] value) => Encoding.UTF8.GetString(value);

        private static byte[]? StringToByteArray(string? value) => value != null ? Encoding.UTF8.GetBytes(value) : null;

        private TheStorageMirror<StorageEntry> CreateOrGetStorageArea(string area)
        {
            lock (_openStorageAreas)
            {
                area = area.ToLowerInvariant();
                if (!_openStorageAreas.TryGetValue(area, out var openStorageArea))
                {
                    openStorageArea = InitializeStorage(area);
                    _openStorageAreas[area] = openStorageArea;
                }
                return openStorageArea;
            }
        }

        private TheStorageMirror<StorageEntry> InitializeStorage(string area)
        {
            var storageAreaName = $"saf.{area}.storage";
            _log.LogTrace($"Initializing storage area \"{storageAreaName}\"");

            var storageMirror = new TheStorageMirror<StorageEntry>(null)
            {
                IsRAMStore = true,
                IsCachePersistent = true,
                CacheTableName = storageAreaName,
                CacheStoreInterval = SaveStorageAreaIntervalSeconds,
                IsStoreIntervalInSeconds = true,
                UseSafeSave = true
            };
            storageMirror.InitializeStore(new TheStorageMirrorParameters
            {
                CanBeFlushed = true,
                LoadSync = true
            });
            return storageMirror;
        }
    }
}