using SAF.Common.Contracts;

namespace SAF.Toolbox.FileTransfer.Messages;

internal class SendFileChunkResponse
{
    public FileReceiverStatus Status { get; set; }
}