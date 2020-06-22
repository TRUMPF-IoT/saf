// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

(function (module) {

    var engineName = "CDEPUBSUB";

    function Service() {
        this.MyBaseEngine = cdeCommCore.StartNewEngine(engineName);
        cdeCommCore.RegisterTopic(engineName);
        this.MyBaseEngine.RegisterIncomingMessage(this.HandleMessage);
    }

    Service.StartEngine = function() {
        Service.MyEngine = new Service();
        Service.HaveCtrlsLoaded = true;
        cde.MyBaseAssets.MyEngines[engineName].FireEngineIsReady();
        cdeNMI.FireEvent(false, "EngineReady", engineName);
    };
    Service.prototype.HandleMessage = function(pmsg) {
        var message = pmsg.Message;
        if (!message)
            return;
        var command = message.TXT.split(":");
        switch (command[0]) {
        case "CDE_INITIALIZED":
            if (this.MyBaseEngine)
                this.MyBaseEngine.SetInitialized(message);
            break;
        default:
            break;
        }
    };

    module.PubSub = {
        Registry: {
            ServicePlugin: Service
        }
    };
})(window.CDMy || (window.CDMy = {}));