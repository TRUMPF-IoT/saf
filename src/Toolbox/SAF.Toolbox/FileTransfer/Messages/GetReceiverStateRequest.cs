using SAF.Common.Contracts;

namespace SAF.Toolbox.FileTransfer.Messages;

internal class GetReceiverStateRequest : MessageRequestBase
{
    public required TransportFile File { get; set; }
}