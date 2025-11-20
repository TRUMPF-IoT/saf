// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Communication.Cde.ConnectionTypes;
using nsCDEngine.BaseClasses;
using nsCDEngine.Communication;
using nsCDEngine.Engines.ThingService;

/// <summary>
/// Link class between the C-DEngine object <c>TheThing</c> and the SAF objects
/// <c>Subscriber</c> and <c>SubscriptionRegistry</c>. 
/// </summary>
public class DefaultComLine : ComLine
{
    private readonly TheThing _thing;
    private readonly List<string> _subscriptions = new();

    public DefaultComLine(TheThing thing)
    {
        _thing = thing;

        _thing.RegisterEvent(eEngineEvents.IncomingMessage, HandleMessage); // listen to messages sent to thing engine
        _thing.RegisterEvent(eThingEvents.IncomingMessage, HandleMessage);  // listen to messages sent to the thing itself
    }

    public override string Address => $"{_thing.cdeN}:{_thing.cdeMID}";

    public override event MessageReceivedHandler? MessageReceived;

    /// <summary>
    /// Find the engine with the name passed by 'engineName' (in the C-DEngine environment always "ContentService",
    /// which is running on every node) and assign to it an event with the target function <see cref="HandleMessage"/>.
    /// </summary>
    /// <param name="topic">Name of the underlying CDE engine.</param>
    public override async Task Subscribe(string topic)
    {
        if(_subscriptions.Contains(topic))
            return;

        _subscriptions.Add(topic);

        var engines = TheThingRegistry.GetBaseEngines(false);
        var myEngine = engines.Find(e => e.GetEngineName() == topic);

        var baseEngine = myEngine?.GetBaseEngine();
        if(baseEngine == null)
            throw new ArgumentException(topic);

        while(!baseEngine.EngineState.IsStarted)
            await Task.Delay(300);

        myEngine!.RegisterEvent(eEngineEvents.IncomingMessage, HandleMessage);
    }

    public override void Broadcast(TSM message)
    {
        message.SetOriginatorThing(_thing);
        TheCommCore.PublishCentral(message, true);
    }

    public override void AnswerToSender(TSM originalMessage, TSM reply)
    {
        reply.SetOriginatorThing(_thing);
        TheCommCore.PublishToOriginator(originalMessage, reply, originalMessage.IsLocalHost());
    }

    private void HandleMessage(ICDEThing sender, object msg)
    {
        MessageReceived?.Invoke(sender, msg);
    }
}