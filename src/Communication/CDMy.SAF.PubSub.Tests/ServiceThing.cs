// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using nsCDEngine.BaseClasses;
using nsCDEngine.Engines;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ViewModels;
using SAF.Communication.Cde;
using SAF.Communication.PubSub.Cde;
using SAF.Communication.PubSub.Interfaces;

namespace CDMy.SAF.PubSub.Tests
{
    public class ServiceThing : ICDEThing
    {
        public IBaseEngine BaseEngine { get; }
        private bool _isInitialized;
        private bool _isInitializing;
        private bool _isUiInitialized;
        private TheThing TheThing { get; set; }
        private Subscriber _subscriber;
        private IPublisher _publisher;
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

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
            if(_isInitialized || _isInitializing) return _isInitialized;
            _isInitializing = true;

            InitAsync().ConfigureAwait(false);

            return false;
        }

        private async Task InitAsync()
        {
            TheCDEngines.MyContentEngine?.RegisterEvent(eEngineEvents.PreShutdown, OnPreShutdown);

            await TheBaseEngine.WaitForEnginesStartedAsync();
            await TheBaseEngine.WaitForStorageReadinessAsync(true);

            TheThing.RegisterEvent(eEngineEvents.IncomingMessage, HandleMessage);

            var comLine = Operator.GetLine(TheThing);

            var publisher = new Publisher(comLine, _cancellationTokenSource.Token);
            _publisher = await publisher.ConnectAsync();

            _subscriber = new Subscriber(comLine, publisher, _cancellationTokenSource.Token);
            _subscriber.Subscribe("*")
                .With((time, message) => SetProperty("Messages", $"[{time}] {message.Topic}: {message.Payload}"));
            _subscriber.Subscribe("public/pubsub/test/number")
                .With((_, message) =>
                {
                    if (int.TryParse(message.Payload, out var value))
                        SetProperty("SampleProperty", value);
                });

            _publisher.Publish("public:debug", "Publisher initialized");

            _isInitialized = true;
            TheThing.StatusLevel = 1;
            TheThing.LastMessage = "Plug-in initialized";
            FireEvent(eThingEvents.Initialized, TheThing, true, false);
            BaseEngine.ProcessInitialized();
        }

        private void OnPreShutdown(ICDEThing arg1, object arg2)
        {
            ShutdownPubSub();
        }

        private void ShutdownPubSub()
        {
            _cancellationTokenSource.Cancel();

            (_subscriber as IDisposable)?.Dispose();
            (_publisher as IDisposable)?.Dispose();
        }

        public bool IsInit()
        {
            return _isInitialized;
        }

        public bool Delete()
        {
            ShutdownPubSub();
            return true;
        }

        public bool CreateUX()
        {
            if(_isUiInitialized) return true;

            TheNMIEngine.AddDashboard(TheThing, new TheDashboardInfo(BaseEngine, "Sample Plugin Screens"));
            var form = TheNMIEngine.AddForm(new TheFormInfo(TheThing) { FormTitle = "Welcome", DefaultView = eDefaultView.Form });
            TheNMIEngine.AddFormToThingUX(TheThing, form, "CMyForm", "Form Title", 3, 3, 0, TheNMIEngine.GetNodeForCategory(), string.Empty, null);

            TheNMIEngine.AddSmartControl(TheThing, form, eFieldType.SingleEnded, 2, 2, 0, "My Sample Value Is", "SampleProperty");
            TheNMIEngine.AddSmartControl(TheThing, form, eFieldType.BarChart, 3, 2, 0, "My Sample Value Bar", "SampleProperty",
                new nmiCtrlBarChart
                {
                    MaxValue = 255,
                    TileWidth = 3,
                    TileHeight = 3,
                    Foreground = "#996633",
                    OnValueChanged = $"cdeCommCore.PublishCentral('{BaseEngine.GetEngineName()}', 'REFRESH', this.tText.ComplexData.Text);"
                });
            TheNMIEngine.AddSmartControl(TheThing, form, eFieldType.TextArea, 4, 2, 0, "Ticker", "Messages");

            TheNMIEngine.AddAboutButton(TheThing, true);
            _isUiInitialized = true;

            return true;
        }

        public bool IsUXInit()
        {
            return _isUiInitialized;
        }

        public void HandleMessage(ICDEThing sender, object pMsg)
        {
            if(!(pMsg is TheProcessMessage msg)) return;

            if(msg.Message.TXT == "REFRESH")
                _publisher.Publish("public/pubsub/test/number", msg.Message.PLS);

            Debug.WriteLine($"{msg.Message.TXT} {msg.Message.PLS}");
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