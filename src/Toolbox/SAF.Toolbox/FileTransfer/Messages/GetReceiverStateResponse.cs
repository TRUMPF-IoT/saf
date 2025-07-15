namespace SAF.Toolbox.FileTransfer.Messages;

public class GetReceiverStateResponse
{
    public required FileReceiverState State { get; set; }
}