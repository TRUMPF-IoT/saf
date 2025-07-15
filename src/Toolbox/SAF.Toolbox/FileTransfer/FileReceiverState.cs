namespace SAF.Toolbox.FileTransfer;

public class FileReceiverState
{
    public bool FileExists { get; set; }
    public HashSet<uint> TransmittedChunks { get; set; } = [];
}