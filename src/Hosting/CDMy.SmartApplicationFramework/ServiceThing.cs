// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using nsCDEngine.BaseClasses;
using nsCDEngine.Engines;
using nsCDEngine.Engines.ThingService;
using SAF.Hosting.Cde;
using SAF.Messaging.Redis;
using SAF.Messaging.Routing;
using SAF.Toolbox.Serialization;

namespace CDMy.SmartApplicationFramework
{
    public class ServiceThing : ICDEThing
    {
        public IBaseEngine BaseEngine { get; }
        private bool _isInitialized;
        private bool _isInitializing;
        private TheThing TheThing { get; set; }
        private ServiceHost _serviceHost;

        public ServiceThing(IBaseEngine baseEngine)
        {
            BaseEngine = baseEngine;
        }

        public void SetBaseThing(TheThing thing)
        {
            TheThing = thing;
        }

        public TheThing GetBaseThing()
        {
            return TheThing;
        }

        public cdeP GetProperty(string name, bool createIfNotExist)
        {
            return TheThing?.GetProperty(name, createIfNotExist);
        }

        public cdeP SetProperty(string name, object value)
        {
            return TheThing?.SetProperty(name, value);
        }

        public bool Init()
        {
            if(_isInitialized || _isInitializing) return true;
            _isInitializing = true;

            // Ramp up application
            InitAsync();

            return false;
        }

        private async void InitAsync()
        {
            TheCDEngines.MyContentEngine?.RegisterEvent(eEngineEvents.PreShutdown, OnPreShutdown);

            await TheBaseEngine.WaitForEnginesStartedAsync();
            await TheBaseEngine.WaitForStorageReadinessAsync(true);

            TheBaseAssets.MyCmdArgs.TryGetValue("SafSearchPath", out var searchPath);
            if (TheBaseAssets.MyCmdArgs.TryGetValue("SafUserBasePath", out var userBasePath) && !Path.IsPathRooted(userBasePath))
            { 
                userBasePath = Path.Combine(TheBaseAssets.MyServiceHostInfo.BaseDirectory, "ClientBin", userBasePath);
            }
            _serviceHost = new ServiceHost(InstallInfrastructureServices, searchPath, userBasePath);

            _isInitialized = true;
            TheThing.StatusLevel = 1;
            TheThing.LastMessage = "Plug-in initialized";
            FireEvent(eThingEvents.Initialized, TheThing, true, false);
            BaseEngine.ProcessInitialized();
        }

        private static void InstallInfrastructureServices(IServiceCollection services)
        {
            if (!TheBaseAssets.MyCmdArgs.TryGetValue("SafUseRedis", out var useRedisString) ||
                !bool.TryParse(useRedisString, out var useRedis))
            {
                useRedis = false;
            }

            TheBaseAssets.MyCmdArgs.TryGetValue("MessageRouting", out var routingConfigFileName);
            var routingConfigFile = routingConfigFileName != null ? Path.Combine(TheBaseAssets.MyServiceHostInfo.BaseDirectory, routingConfigFileName) : null;
            var routingConfigured = routingConfigFileName != null && File.Exists(routingConfigFile);

            if(useRedis)
            {
                if(!routingConfigured)
                {
                    services.AddRedisInfrastructure(config => config.ConnectionString = BuildRedisConnectionString());
                    return;
                }
                services.AddRedisStorageInfrastructure(config => config.ConnectionString = BuildRedisConnectionString());
            }

            if(routingConfigured)
            {
                TheBaseAssets.MySYSLOG?.WriteToLog(0, new TSM("CDMy.SmartApplicationFramework", $"Reading message routing config from file {routingConfigFile}", eMsgLevel.l3_ImportantMessage, null));
                var jsonConfig = File.ReadAllText(routingConfigFile);
                var routingConfig = JsonSerializer.Deserialize<Configuration>(jsonConfig);

                services.AddRoutingMessagingInfrastructure(config => { config.Routings = routingConfig.Routings; });
            }
        }

        private static string BuildRedisConnectionString()
        {
            if (!TheBaseAssets.MyCmdArgs.TryGetValue("SafRedisHost", out var host)) host = "localhost";
            if (!TheBaseAssets.MyCmdArgs.TryGetValue("SafRedisPort", out var port)) port = "6379";
            return $"{host}:{port}";
        }

        private void OnPreShutdown(ICDEThing sender, object msg)
        {
            ShutdownService();
        }

        private void ShutdownService()
        {
            _serviceHost?.Dispose();
        }

        public bool IsInit()
        {
            return _isInitialized;
        }

        public bool Delete()
        {
            ShutdownService();
            return true;
        }

        public bool CreateUX()
        {
            return true;
        }

        public bool IsUXInit()
        {
            return true;
        }

        public void HandleMessage(ICDEThing sender, object pMsg)
        {
        }

        public virtual void RegisterEvent(string eventName, Action<ICDEThing, object> callback)
        {
            TheThing?.RegisterEvent(eventName, callback);
        }

        public virtual void UnregisterEvent(string eventName, Action<ICDEThing, object> callback)
        {
            TheThing?.UnregisterEvent(eventName, callback);
        }

        public virtual void FireEvent(string eventName, ICDEThing sender, object parameter, bool fireAsync)
        {
            TheThing?.FireEvent(eventName, sender, parameter, fireAsync);
        }

        public virtual bool HasRegisteredEvents(string eventName)
        {
            return TheThing != null && TheThing.HasRegisteredEvents(eventName);
        }
    }
}