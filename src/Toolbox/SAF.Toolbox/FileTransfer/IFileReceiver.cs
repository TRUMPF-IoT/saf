namespace SAF.Toolbox.FileTransfer;

public interface IFileReceiver
{
    void Subscribe(string topic, IStatefulFileReceiver statefulFileReceiver, string folderPath);
    void Unsubscribe(string topic);
    void Unsubscribe();
}