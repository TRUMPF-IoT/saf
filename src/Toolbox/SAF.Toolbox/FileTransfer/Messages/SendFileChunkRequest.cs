using SAF.Common.Contracts;

namespace SAF.Toolbox.FileTransfer.Messages;

internal class SendFileChunkRequest : MessageRequestBase
{
    public required TransportFile File { get; set; }
    public FileChunk? FileChunk { get; set; }
}