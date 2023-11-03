// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Communication.PubSub.Interfaces
{
    public class MessageEventArgs : EventArgs
    {
        public DateTimeOffset Time { get; }
        public string Topic { get; }
        public string Message { get; }

        public MessageEventArgs(DateTimeOffset time, string topic, string message)
        {
            Time = time;
            Topic = topic;
            Message = message;
        }
    }
}