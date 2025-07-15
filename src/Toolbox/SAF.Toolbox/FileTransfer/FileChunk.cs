namespace SAF.Toolbox.FileTransfer;

public class FileChunk
{
    public required uint Index { get; set; }
    public required byte[] Data { get; set; }
}