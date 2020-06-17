// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

﻿
using System;
using SAF.Communication.PubSub.Cde.Authorization;
using nsCDEngine.ViewModels;

namespace SAF.Communication.PubSub.Cde.MessageHandler.Authorization
{
    internal class GetTokenHandler : MessageHandler
    {
        private readonly AuthorizationService _authService;
        private static readonly string Key = $"cdepubsub:publish:{AuthorizationService.ChannelGetToken}";

        public GetTokenHandler(AuthorizationService authService, MessageHandler successor) : base(successor)
        {
            _authService = authService;
        }

        protected override bool CanHandleThis(TheProcessMessage message)
        {
            return message.Message.TXT.StartsWith(Key, StringComparison.Ordinal);
        }

        protected override void HandleThis(TheProcessMessage message)
        {
            _authService.GetToken(message);
        }
    }
}