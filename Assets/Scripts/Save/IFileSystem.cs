using System.IO;

namespace InTheArena.Save
{
    public interface IFileSystem
    {
        bool FileExists(string path);
        void CreateDirectory(string path);
        string ReadAllText(string path);
        void WriteAllText(string path, string contents);
        void Copy(string sourceFileName, string destFileName, bool overwrite);
        void Replace(string sourceFileName, string destinationFileName, string destinationBackupFileName, bool ignoreMetadataErrors);
        void Delete(string path);
        void Move(string sourceFileName, string destFileName);
        Stream OpenWrite(string path);
    }

    public class SystemFileSystem : IFileSystem
    {
        public bool FileExists(string path) => File.Exists(path);
        public void CreateDirectory(string path) => Directory.CreateDirectory(path);
        public string ReadAllText(string path) => File.ReadAllText(path);
        public void WriteAllText(string path, string contents) => File.WriteAllText(path, contents);
        public void Copy(string sourceFileName, string destFileName, bool overwrite) => File.Copy(sourceFileName, destFileName, overwrite);
        public void Replace(string sourceFileName, string destinationFileName, string destinationBackupFileName, bool ignoreMetadataErrors) 
            => File.Replace(sourceFileName, destinationFileName, destinationBackupFileName, ignoreMetadataErrors);
        public void Delete(string path) => File.Delete(path);
        public void Move(string sourceFileName, string destFileName) => File.Move(sourceFileName, destFileName);
        public Stream OpenWrite(string path) => new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
    }
}
