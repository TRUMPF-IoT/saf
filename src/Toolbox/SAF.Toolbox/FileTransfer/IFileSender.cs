namespace SAF.Toolbox.FileTransfer;

public interface IFileSender
{
    Task<FileTransferStatus> SendAsync(string topic, string fullFilePath, ulong timeoutMs);
    Task<FileTransferStatus> SendAsync(string topic, string fullFilePath, ulong timeoutMs, IDictionary<string, string> properties);
}