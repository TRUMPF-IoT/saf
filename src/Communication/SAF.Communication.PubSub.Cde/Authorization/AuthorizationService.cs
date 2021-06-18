// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0


using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using nsCDEngine.BaseClasses;
using nsCDEngine.Security;
using nsCDEngine.ViewModels;
using SAF.Common;
using SAF.Communication.PubSub.Interfaces;

namespace SAF.Communication.PubSub.Cde.Authorization
{
    public class AuthorizationService
    {
        private readonly Publisher _publisher;

        public const string BaseChannelName = "internal/auth";
        public static readonly string ChannelGetToken = $"{BaseChannelName}/token/get";
        public static readonly string ChannelCheckToken = $"{BaseChannelName}/token/check";

        private readonly ConcurrentDictionary<string, string> _tokens = new();

        public AuthorizationService(Publisher publisher)
        {
            _publisher = publisher;
        }

        public void CheckToken(string msgVersion, TheProcessMessage msg)
        {
            try
            {
                var message = msgVersion == PubSubVersion.V1
                    ? TheCommonUtils.DeserializeJSONStringToObject<AuthCheckRequestMessage>(msg.Message.PLS)
                    : TheCommonUtils.DeserializeJSONStringToObject<AuthCheckRequestMessage>(TheCommonUtils.DeserializeJSONStringToObject<Message>(msg.Message.PLS).Payload);
                var replyTo = message.replyTo;
                var resource = message.resource;
                var token = message.token;
                var accessLevel = message.accessLevel;
                var res = token.Substring(0, resource.Length);
                var hasAccess = false;
                if(res == resource && _tokens.ContainsKey(token))
                {
                    hasAccess = TheUserManager.HasUserAccess(TheCommonUtils.CGuid(_tokens[token]), accessLevel);
                    _tokens.TryRemove(token, out _);
                }

                var uid = msg.CurrentUserID != Guid.Empty ? $"{msg.CurrentUserID}" : $"{msg.Message.UID}";
                SendReply(uid, replyTo, $"{hasAccess}".ToLowerInvariant());
            }
            catch(Exception e)
            {
                Debug.WriteLine(e);
            }
        }

        public void GetToken(string msgVersion, TheProcessMessage msg)
        {
            // Check if user is locally known - given the user has set at least one bit
            if(!TheUserManager.HasUserAccess(TheCommonUtils.CGuid(msg.CurrentUserID), 0xffffff)) return;

            try
            {
                var message = msgVersion == PubSubVersion.V1
                    ? TheCommonUtils.DeserializeJSONStringToObject<AuthTokenRequestMessage>(msg.Message.PLS)
                    : TheCommonUtils.DeserializeJSONStringToObject<AuthTokenRequestMessage>(TheCommonUtils.DeserializeJSONStringToObject<Message>(msg.Message.PLS).Payload);
                var replyTo = message.replyTo;
                var resource = message.resource;
                var hash = $"{resource}{replyTo}";
                var uid = msg.CurrentUserID != Guid.Empty ? $"{msg.CurrentUserID}" : $"{msg.Message.UID}";
                _tokens[hash] = uid;
                SendReply(uid, replyTo, hash);
            }
            catch(Exception e)
            {
                Debug.WriteLine(e);
            }
        }

        private void SendReply(string uid, string channel, string reply)
        {
            var replyMsg = new Message {Topic = channel, Payload = reply};
            _publisher.Publish(replyMsg, uid);
        }

#pragma warning disable 169
#pragma warning disable 649
        // ReSharper disable InconsistentNaming

        private struct AuthCheckRequestMessage
        {
            public string replyTo;
            public string resource;
            public string token;
            public int accessLevel;
        }

        private struct AuthTokenRequestMessage
        {
            public string replyTo;
            public string resource;
        }

        // ReSharper restore InconsistentNaming
#pragma warning restore 169
#pragma warning restore 694
    }
}