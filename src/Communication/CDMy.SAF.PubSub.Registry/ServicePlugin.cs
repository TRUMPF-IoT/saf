// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using CDMyPubSub.Registry;
using nsCDEngine.Engines;
using nsCDEngine.Engines.ThingService;

namespace CDMy.SAF.PubSub.Registry
{
    public class ServicePlugin : ICDEPlugin, ICDEThing
    {
        private ICDEThing _thing;
        private ICDEThing Thing => _thing ?? (_thing = new ServiceThing(BaseEngine));
        protected IBaseEngine BaseEngine { get; private set; }


        public IBaseEngine GetBaseEngine()
        {
            return BaseEngine;
        }

        public void InitEngineAssets(IBaseEngine pEngine)
        {
            BaseEngine = pEngine;

            BaseEngine.SetEngineName("CDEPUBSUB");
            BaseEngine.SetEngineType(GetType());
            BaseEngine.SetFriendlyName("CDE SAF Pub/Sub Registry");
            BaseEngine.SetEngineService(true);

            BaseEngine.SetEngineID(new Guid("{7CFBB1FC-647A-4B58-964F-83C6A45C932D}"));
            BaseEngine.SetPluginInfo("Provides Pub/Sub for CDE", 0, "https://github.com/TRUMPF-IoT/smart-application-framework", string.Empty, "TRUMPF Laser GmbH", "https://github.com/TRUMPF-IoT", new List<string>(), "2017-2020 TRUMPF Laser GmbH");
            BaseEngine.SetVersion(GetPluginVersionFromAssemblyVersion());
        }

        public void SetBaseThing(TheThing pThing)
        {
            Thing.SetBaseThing(pThing);
        }

        public TheThing GetBaseThing()
        {
            return Thing.GetBaseThing();
        }

        public cdeP GetProperty(string name, bool createIfNotExist)
        {
            return Thing.GetProperty(name, createIfNotExist);
        }

        public cdeP SetProperty(string name, object value)
        {
            return Thing.SetProperty(name, value);
        }

        public bool Init()
        {
            return Thing.Init();
        }

        public bool IsInit()
        {
            return Thing.IsInit();
        }

        public bool Delete()
        {
            return Thing.Delete();
        }

        public bool CreateUX()
        {
            return Thing.CreateUX();
        }

        public bool IsUXInit()
        {
            return Thing.IsUXInit();
        }

        public void HandleMessage(ICDEThing sender, object pMsg)
        {
            Thing.HandleMessage(sender, pMsg);
        }

        public void RegisterEvent(string pEventName, Action<ICDEThing, object> pCallback)
        {
            Thing.RegisterEvent(pEventName, pCallback);
        }

        public void UnregisterEvent(string pEventName, Action<ICDEThing, object> pCallback)
        {
            Thing.UnregisterEvent(pEventName, pCallback);
        }

        public void FireEvent(string pEventName, ICDEThing sender, object parameter, bool fireAsync)
        {
            Thing.FireEvent(pEventName, sender, parameter, fireAsync);
        }

        public bool HasRegisteredEvents(string pEventName)
        {
            return Thing.HasRegisteredEvents(pEventName);
        }

        private static double GetPluginVersionFromAssemblyVersion()
        {
            var assemblyFile = Assembly.GetExecutingAssembly().Location;
            var fileVersionInfo = FileVersionInfo.GetVersionInfo(assemblyFile);
            return 4.0 + fileVersionInfo.FileMajorPart + 0.01 * fileVersionInfo.FileMinorPart + 0.00001 * fileVersionInfo.FileBuildPart;
        }
    }
}