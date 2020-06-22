// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0


using nsCDEngine.ViewModels;

namespace SAF.Communication.PubSub.Cde.MessageHandler
{
    public abstract class MessageHandler
    {
        private readonly MessageHandler _successor;

        protected MessageHandler(MessageHandler successor)
        {
            _successor = successor;
        }

        public bool CanHandle(TheProcessMessage message)
        {
            if (CanHandleThis(message)) return true;
            return _successor?.CanHandleThis(message) ?? false;
        }

        public void Handle(TheProcessMessage message)
        {
            if(CanHandleThis(message))
                HandleThis(message);
            else
                _successor?.Handle(message);
        }

        protected abstract bool CanHandleThis(TheProcessMessage message);
        protected abstract void HandleThis(TheProcessMessage message);
    }
}