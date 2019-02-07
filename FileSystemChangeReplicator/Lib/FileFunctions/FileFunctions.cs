namespace FileSystemChangeReplicator.FileFunctions
{
    using System.IO;
    using System;

    static class FileFunctions
    {
        public enum FileSystemElementType { FILE = 1, DIRECTORY = 2}

        public static bool IsValidRootedPath(string path)
        {
            try
            {
                Path.GetFullPath(path);
            }
            catch (System.Exception)
            {
                return false;
            }

            if (!Path.IsPathRooted(path))
            {
                return false;
            }

            return true;
        }

        public static FileSystemElementType GetFileSystemElementType(string path)
        {
            if (File.GetAttributes(path) == FileAttributes.Directory)
            {
                return FileSystemElementType.DIRECTORY;
            }
            return FileSystemElementType.FILE;
        }

        public static void CopyFileOrDirectory(string sourcePath, string destinationPath, bool createLocationIfNotExists = true)
        {
            if (GetFileSystemElementType(sourcePath) == FileSystemElementType.DIRECTORY)
            {
                Directory.CreateDirectory(destinationPath);
                Uri fullSourceDirectoryUri = new Uri(sourcePath);
                Uri fullDestinationDirectoryUri = new Uri(destinationPath);
                foreach (string subdirectoryPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
                {
                    Uri fullSourceSubdirectoryUri = new Uri(subdirectoryPath);
                    Uri relativeSourceSubdirectoryUri = fullSourceDirectoryUri.MakeRelativeUri(fullSourceSubdirectoryUri);
                    string relativeSourceSubdirectoryPath = Uri.UnescapeDataString(relativeSourceSubdirectoryUri.ToString());
                    Uri fullDestinationSubdirectoryUri = new Uri(fullDestinationDirectoryUri, relativeSourceSubdirectoryPath);
                    string fullDestinationSubdirectoryPath = Uri.UnescapeDataString(fullDestinationSubdirectoryUri.LocalPath);
                    Directory.CreateDirectory(fullDestinationSubdirectoryPath);
                }

                foreach (string filePath in Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories))
                {
                    Uri fullSourceFileUri = new Uri(filePath);
                    Uri relativeSourceFileUri = fullSourceDirectoryUri.MakeRelativeUri(fullSourceFileUri);
                    string relativeSourceFilePath = Uri.UnescapeDataString(relativeSourceFileUri.ToString());
                    Uri fullDestinationFileUri = new Uri(fullDestinationDirectoryUri, relativeSourceFileUri);
                    string fullDestinationFilePath = Uri.UnescapeDataString(fullDestinationFileUri.LocalPath);
                    File.Copy(fullSourceFileUri.LocalPath, fullDestinationFileUri.LocalPath, true);
                }
            }
            else
            {
                DirectoryInfo destinationDirectory = new FileInfo(destinationPath).Directory;
                if (!destinationDirectory.Exists)
                {
                    if (createLocationIfNotExists)
                    {
                        destinationDirectory.Create();
                    }
                    else
                    {
                        throw new Exception(
                            $"Cannot create file \"{destinationPath}\". Output directory \"{destinationDirectory.FullName}\" doesn't exist."
                        );
                    }
                }
                File.Copy(sourcePath, destinationPath, true);
            }
        }

        public static void MoveFileOrDirectory(string sourcePath, string destinationPath)
        {
            if (File.GetAttributes(sourcePath) == FileAttributes.Directory)
            {
                Directory.Move(sourcePath, destinationPath);
            }
            else
            {
                File.Move(sourcePath, destinationPath);
            }
        }

        public static void DeleteFileOrDirectory(string path)
        {
            if (File.GetAttributes(path) == FileAttributes.Directory)
            {
                Directory.Delete(path);
            }
            else
            {
                File.Delete(path);
            }
        }
    }
}
