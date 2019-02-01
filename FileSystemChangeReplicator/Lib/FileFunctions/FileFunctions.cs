namespace FileSystemChangeReplicator.FileFunctions
{
    static class FileFunctions
    {
        public enum FileSystemElementType { FILE = 1, DIRECTORY = 2}

        public static bool IsValidRootedPath(string path)
        {
            try
            {
                System.IO.Path.GetFullPath(path);
            }
            catch (System.Exception)
            {
                return false;
            }

            if (!System.IO.Path.IsPathRooted(path))
            {
                return false;
            }

            return true;
        }

        public static FileSystemElementType GetFileSystemElementType(string path)
        {
            if (System.IO.File.GetAttributes(path) == System.IO.FileAttributes.Directory)
            {
                return FileSystemElementType.DIRECTORY;
            }
            return FileSystemElementType.FILE;
        }

        public static void CopyFileOrDirectory(string sourcePath, string destinationPath)
        {
            if (GetFileSystemElementType(sourcePath) == FileSystemElementType.DIRECTORY)
            {
                System.IO.Directory.CreateDirectory(destinationPath);
                System.Uri fullSourceDirectoryUri = new System.Uri(sourcePath);
                System.Uri fullDestinationDirectoryUri = new System.Uri(destinationPath);
                foreach (string subdirectoryPath in System.IO.Directory.GetDirectories(sourcePath, "*", System.IO.SearchOption.AllDirectories))
                {
                    System.Uri fullSourceSubdirectoryUri = new System.Uri(subdirectoryPath);
                    System.Uri relativeSourceSubdirectoryUri = fullSourceDirectoryUri.MakeRelativeUri(fullSourceSubdirectoryUri);
                    string relativeSourceSubdirectoryPath = System.Uri.UnescapeDataString(relativeSourceSubdirectoryUri.ToString());
                    System.Uri fullDestinationSubdirectoryUri = new System.Uri(fullDestinationDirectoryUri, relativeSourceSubdirectoryPath);
                    string fullDestinationSubdirectoryPath = System.Uri.UnescapeDataString(fullDestinationSubdirectoryUri.LocalPath);
                    System.IO.Directory.CreateDirectory(fullDestinationSubdirectoryPath);
                }

                foreach (string filePath in System.IO.Directory.GetFiles(sourcePath, "*", System.IO.SearchOption.AllDirectories))
                {
                    System.Uri fullSourceFileUri = new System.Uri(filePath);
                    System.Uri relativeSourceFileUri = fullSourceDirectoryUri.MakeRelativeUri(fullSourceFileUri);
                    string relativeSourceFilePath = System.Uri.UnescapeDataString(relativeSourceFileUri.ToString());
                    System.Uri fullDestinationFileUri = new System.Uri(fullDestinationDirectoryUri, relativeSourceFileUri);
                    string fullDestinationFilePath = System.Uri.UnescapeDataString(fullDestinationFileUri.LocalPath);
                    System.IO.File.Copy(fullSourceFileUri.LocalPath, fullDestinationFileUri.LocalPath, true);
                }
            }
            else
            {
                System.IO.File.Copy(sourcePath, destinationPath, true);
            }
        }

        public static void MoveFileOrDirectory(string sourcePath, string destinationPath)
        {
            if (System.IO.File.GetAttributes(sourcePath) == System.IO.FileAttributes.Directory)
            {
                System.IO.Directory.Move(sourcePath, destinationPath);
            }
            else
            {
                System.IO.File.Move(sourcePath, destinationPath);
            }
        }

        public static void DeleteFileOrDirectory(string path)
        {
            if (System.IO.File.GetAttributes(path) == System.IO.FileAttributes.Directory)
            {
                System.IO.Directory.Delete(path);
            }
            else
            {
                System.IO.File.Delete(path);
            }
        }
    }
}
