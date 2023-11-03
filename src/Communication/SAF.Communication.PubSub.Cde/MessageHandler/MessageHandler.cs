// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0


using nsCDEngine.ViewModels;

namespace SAF.Communication.PubSub.Cde.MessageHandler
{
    public abstract class MessageHandler
    {
        private readonly MessageHandler? _successor;

        protected MessageHandler(MessageHandler? successor)
        {
            _successor = successor;
        }

        public bool CanHandle(string msgVersion, TheProcessMessage message)
        {
            if (CanHandleThis(msgVersion, message)) return true;
            return _successor?.CanHandleThis(msgVersion, message) ?? false;
        }

        public void Handle(string msgVersion, TheProcessMessage message)
        {
            if(CanHandleThis(msgVersion, message))
                HandleThis(msgVersion, message);
            else
                _successor?.Handle(msgVersion, message);
        }

        protected abstract bool CanHandleThis(string msgVersion, TheProcessMessage message);
        protected abstract void HandleThis(string msgVersion, TheProcessMessage message);
    }
}