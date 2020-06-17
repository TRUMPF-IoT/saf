// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

﻿
namespace SAF.Communication.PubSub
{
    public class Topic
    {
        public string Channel;
        public string MsgId;
    }

    public static class TopicExtensions
    {
        public static string ToTsmTxt(this Topic topic)
        {
            return $"{topic.Channel}|{topic.MsgId}";
        }
    }

    public static class StringExtensions
    {
        public static Topic ToTopic(this string txt)
        {
            var parts = txt.Split('|');
            if (parts.Length < 2) return null;

            return new Topic
            {
                Channel = parts[0],
                MsgId = parts[1]
            };
        }
    }
}