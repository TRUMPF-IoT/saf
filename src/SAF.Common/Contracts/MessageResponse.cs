// SPDX-FileCopyrightText: 2017-2022 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Common.Contracts;

/// <summary>
/// The standard response message for all responses.
/// </summary>
/// <typeparam name="TResponseType">The type of the Response object</typeparam>
/// <typeparam name="TErrorDetailsType"></typeparam>
public class MessageResponse<TResponseType, TErrorDetailsType>
{
    /// <summary>
    /// The message response. All results are listed here. 
    /// </summary>
    public TResponseType Response { get; set; } = default!;
    /// <summary>
    /// The aggregate error response.
    /// </summary>
    public IEnumerable<ResponseError<TErrorDetailsType>>? Error { get; set; }
}

/// <summary>
/// The Error response object. Contains a title, type and details
/// </summary>
/// <typeparam name="TErrorDetailsType">The details type.</typeparam>
public class ResponseError<TErrorDetailsType>
{
    /// <summary>
    /// The error type that describes the error.
    /// </summary>
    public string Type { get; set; } = default!;
    /// <summary>
    /// The short title for the error. 
    /// </summary>
    public string Title { get; set; } = default!;
    /// <summary>
    /// A detailed object for the error.
    /// </summary>
    public TErrorDetailsType Details { get; set; } = default!;
}