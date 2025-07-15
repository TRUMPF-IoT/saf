namespace SAF.Toolbox.FileTransfer;

internal static class TransportFileExtensions
{
    public static string GetTargetFilePath(this TransportFile file, string folderPath)
        => Path.Combine(folderPath, file.FileName);

    public static string GetTempTargetFilePath(this TransportFile file, string folderPath)
    {
        var targetFilePath = file.GetTargetFilePath(folderPath);
        return Path.ChangeExtension(file.FileName, $".{file.FileId}.temp");
    }

    public static string GetMetadataTargetFilePath(this TransportFile file, string folderPath)
    {
        var targetFilePath = file.GetTargetFilePath(folderPath);
        return Path.ChangeExtension(file.FileName, $".{file.FileId}.meta");
    }
}