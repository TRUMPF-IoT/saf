// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0


using SAF.Communication.PubSub.Interfaces;

namespace SAF.Communication.PubSub
{
    public class Topic
    {
        public string Channel;
        public string MsgId;
    }

    public static class TopicExtensions
    {
        public static string ToTsmTxt(this Topic topic, string version)
        {
            var tsmTxt = $"{topic.Channel}|{topic.MsgId}";
            return string.IsNullOrEmpty(version) ? tsmTxt : $"{tsmTxt}|{version}";
        }
    }

    public static class StringExtensions
    {
        public static (Topic topic, string version) ToTopic(this string txt)
        {
            var parts = txt.Split('|');
            if (parts.Length < 2) return (null, null);

            return (new Topic
            {
                Channel = parts[0],
                MsgId = parts[1]
            }, parts.Length >= 3 ? parts[2] : PubSubVersion.V1);
        }
    }
}