// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0


using System;
using nsCDEngine.Engines;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ViewModels;

namespace CDMyPubSub.Registry
{
    public class ServiceThing : ICDEThing
    {
        public IBaseEngine BaseEngine { get; }
        private bool _isInitialized;
        private bool _isUiInitialized;
        private TheThing TheThing { get; set; }

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
            if(_isInitialized) return true;
            _isInitialized = true;

            TheThing.AddCapability(eThingCaps.SkinProvider); // Complete fake but ensures that plugin gets loaded before pub/sub clients which are supposed to be regular plugins.
            TheThing.StatusLevel = 1;
            TheThing.LastMessage = "Plug-in initialized";
            FireEvent(eThingEvents.Initialized, TheThing, true, false);
            BaseEngine.ProcessInitialized();

            return true;
        }

        public bool IsInit()
        {
            return _isInitialized;
        }

        public bool Delete()
        {
            return true;
        }

        public bool CreateUX()
        {
            if(_isUiInitialized) return true;

            _isUiInitialized = true;

            var dashboard = TheNMIEngine.AddDashboard(TheThing, new TheDashboardInfo(BaseEngine, "")
            {
                PropertyBag = new ThePropertyBag
                {
                    "ForceLoad=true",
                    "Category=SAF",
                    "TileWidth=0",
                    "TileHeight=0"
                }
            });

            TheNMIEngine.AddDashPanel(dashboard, new TheDashPanelInfo(TheThing, new Guid("{66FD3715-5F02-4F02-AD80-8983A2A925F5}"), "Details", $"{BaseEngine.GetEngineName()}:ServiceScreen")
            {
                Category = "SAF",
                IsFullSceen = true,
                PropertyBag = new ThePropertyBag
                {
                    "ForceLoad=true",
                    "HidePins=true"
                }
            });
            TheNMIEngine.AddAboutButton(TheThing, true);
            TheNMIEngine.RegisterEngine(BaseEngine);

            return true;
        }

        public bool IsUXInit()
        {
            return _isUiInitialized;
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