// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System.Threading.Tasks;
using SAF.Common.Contracts;
using SAF.Toolbox.Serialization;

namespace SAF.Toolbox.RequestClient
{
    public interface IRequestClient
    {
        void SetDefaultPrefix(string prefix);

        Task<TResponse> SendRequestAwaitFirstAnswer<TRequest, TResponse>(string topic, TRequest request, string replyTopicPrefix = null, double? millisecondsTimeoutTarget = null)
            where TRequest : MessageRequestBase
            where TResponse : class;

        Task<TResponse> SendRequestAwaitFirstAnswer<TRequest, TResponse>(string topic, TRequest request, IJsonObjectConverter[] converters, string replyTopicPrefix = null, double? millisecondsTimeoutTarget = null)
            where TRequest : MessageRequestBase
            where TResponse : class;

        Task<string> SendRequestAwaitFirstAnswer<TRequest>(string topic, TRequest request,
            IJsonObjectConverter[] converters, string replyTopicPrefix = null, double? millisecondsTimeoutTarget = null)
            where TRequest : MessageRequestBase;
    }
}
