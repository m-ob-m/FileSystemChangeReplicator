namespace FileSystemChangeReplicator.FileSystemWatcher
{
    using FileSystemChangeReplicator.FileFunctions;
    using FileSystemChangeReplicator.Logging;
    using System.IO;
    using System.Runtime.Caching;
    using System.Threading;
    using System;

    class FileSystemWatcher2
    {
        [Flags] public enum EventIDs {NONE = 0, CREATED = 1, CHANGED = 2, RENAMED = 4, DELETED = 8, ALL = 15}
        private string sourcePath;
        private string destinationPath;
        private EventIDs events;
        private FileSystemWatcher watcher;
        private readonly MemoryCache memoryCache;
        private readonly CacheItemPolicy cacheItemPolicy;
        private const int CACHE_TIME_MILLISECONDS = 1000;
        private const int NUMBER_OF_RETRIES = 5;
        public class FileSystemWatcherEventDescription
        {
            public EventIDs type;
            public string currentFilePath;
            public string previousFilePath;
        }

        public FileSystemWatcher2(string sourcePath, string destinationPath, EventIDs events)
        {
            this.sourcePath = sourcePath;
            this.destinationPath = destinationPath;
            this.events = events;
            Running = false;
            watcher = new FileSystemWatcher();
            memoryCache = MemoryCache.Default;
            cacheItemPolicy = new CacheItemPolicy()
            {
                RemovedCallback = OnRemovedFromCache
            };
        }

        ~FileSystemWatcher2()
        {
            memoryCache.Dispose();
            watcher.Dispose();
            watcher = null;
        }

        public void Start()
        {
            watcher.Filter = "*";
            watcher.IncludeSubdirectories = true;
            watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
            watcher.Path = sourcePath;

            // Enable events.
            if ((events & EventIDs.CREATED) > 0)
            {
                watcher.Created += new FileSystemEventHandler(OnCreated);
            }
            if ((events & EventIDs.CHANGED) > 0)
            {
                watcher.Changed += new FileSystemEventHandler(OnChanged);
            }
            if ((events & EventIDs.RENAMED) > 0)
            {
                watcher.Renamed += new RenamedEventHandler(OnRenamed);
            }
            if ((events & EventIDs.DELETED) > 0)
            {
                watcher.Deleted += new FileSystemEventHandler(OnDeleted);
            }
            watcher.Error += new ErrorEventHandler(OnError);
            watcher.EnableRaisingEvents = true;
            Running = true;
        }

        public void Stop()
        {
            // Disable events.
            watcher.Created -= new FileSystemEventHandler(OnCreated);
            watcher.Changed -= new FileSystemEventHandler(OnChanged);
            watcher.Renamed -= new RenamedEventHandler(OnRenamed);
            watcher.Deleted -= new FileSystemEventHandler(OnDeleted);
            watcher.Error -= new ErrorEventHandler(OnError);
            watcher.EnableRaisingEvents = false;
            Running = false;
        }

        private void OnChanged(object source, FileSystemEventArgs myEvent)
        {
            cacheItemPolicy.AbsoluteExpiration = DateTimeOffset.Now.AddMilliseconds(CACHE_TIME_MILLISECONDS);

            // Only add if it is not there already (swallow others)
            FileSystemWatcherEventDescription eventParameters = new FileSystemWatcherEventDescription()
            {
                type = EventIDs.CHANGED,
                currentFilePath = myEvent.FullPath,
                previousFilePath = null
            };
            memoryCache.AddOrGetExisting($"{myEvent.FullPath}_Changed", eventParameters, cacheItemPolicy);
        }

        private void OnCreated(object source, FileSystemEventArgs myEvent)
        {
            cacheItemPolicy.AbsoluteExpiration = DateTimeOffset.Now.AddMilliseconds(CACHE_TIME_MILLISECONDS);

            // Only add if it is not there already (swallow others)
            FileSystemWatcherEventDescription eventParameters = new FileSystemWatcherEventDescription()
            {
                type = EventIDs.CREATED,
                currentFilePath = myEvent.FullPath,
                previousFilePath = null
            };
            memoryCache.AddOrGetExisting($"{myEvent.FullPath}_Created", eventParameters, cacheItemPolicy);
        }

        private void OnRenamed(object source, RenamedEventArgs myEvent)
        {
            cacheItemPolicy.AbsoluteExpiration = DateTimeOffset.Now.AddMilliseconds(CACHE_TIME_MILLISECONDS);

            // Only add if it is not there already (swallow others)
            FileSystemWatcherEventDescription eventParameters = new FileSystemWatcherEventDescription()
            {
                type = EventIDs.RENAMED,
                currentFilePath = myEvent.FullPath,
                previousFilePath = myEvent.OldFullPath
            };
            memoryCache.AddOrGetExisting($"{myEvent.FullPath}_Renamed", eventParameters, cacheItemPolicy);
        }

        private void OnDeleted(object source, FileSystemEventArgs myEvent)
        {
            cacheItemPolicy.AbsoluteExpiration = DateTimeOffset.Now.AddMilliseconds(CACHE_TIME_MILLISECONDS);

            // Only add if it is not there already (swallow others)
            FileSystemWatcherEventDescription eventParameters = new FileSystemWatcherEventDescription()
            {
                type = EventIDs.DELETED,
                currentFilePath = myEvent.FullPath,
                previousFilePath = null
            };
            memoryCache.AddOrGetExisting($"{myEvent.FullPath}_Deleted", eventParameters, cacheItemPolicy);
        }

        private void OnError(object source, ErrorEventArgs myEvent)
        {
            if (myEvent.GetException().GetType() == typeof(InternalBufferOverflowException))
            {
                //  This can happen if Windows is reporting many file system events quickly 
                //  and internal buffer of the  FileSystemWatcher is not large enough to handle this
                //  rate of events. The InternalBufferOverflowException error informs the application
                //  that some of the file system events are being lost.
                Console.WriteLine($"The file system watcher experienced an internal buffer overflow: {myEvent.GetException().Message}");
                Logger.Log($"The file system watcher experienced an internal buffer overflow: {myEvent.GetException().Message}");
            }
            else
            {
                Console.WriteLine($"The file sytem watcher has trigerred an unknown error: {myEvent.GetException().Message}");
                Logger.Log($"The file sytem watcher has trigerred an unknown error: {myEvent.GetException().Message}");
            }
        }

        private void OnRemovedFromCache(CacheEntryRemovedArguments arguments)
        {
            if (arguments.RemovedReason != CacheEntryRemovedReason.Expired)
            {
                return;
            }

            new Thread(
                delegate ()
                {
                    FileSystemWatcherEventDescription myEventDescription = (FileSystemWatcherEventDescription)arguments.CacheItem.Value;
                    switch (myEventDescription.type)
                    {
                        case EventIDs.CREATED:
                            HandleCreatedEvent(myEventDescription.currentFilePath);
                            break;
                        case EventIDs.CHANGED:
                            HandleChangedEvent(myEventDescription.currentFilePath);
                            break;
                        case EventIDs.RENAMED:
                            HandleRenamedEvent(myEventDescription.previousFilePath, myEventDescription.currentFilePath);
                            break;
                        case EventIDs.DELETED:
                            HandleDeletedEvent(myEventDescription.currentFilePath);
                            break;
                        default:
                            break;
                    }
                }
            ).Start();
        }

        private void HandleCreatedEvent(string fullSourcePath)
        {
            Uri fullSourceUri = new Uri(fullSourcePath);
            Uri relativeSourceUri = new Uri(sourcePath).MakeRelativeUri(fullSourceUri);
            string relativeSourcePath = Uri.UnescapeDataString(relativeSourceUri.ToString());
            Uri fullDestinationUri = new Uri(new Uri(destinationPath), relativeSourcePath);
            string fullDestinationPath = Uri.UnescapeDataString(fullDestinationUri.LocalPath);
            for (int i = 0; i < NUMBER_OF_RETRIES; i++)
            {
                try
                {
                    FileFunctions.CopyFileOrDirectory(fullSourcePath, fullDestinationPath, createLocationIfNotExists: true);
                    break;
                }
                catch (FileNotFoundException)
                {
                    if (i < NUMBER_OF_RETRIES - 1)
                    {
                        Thread.Sleep(1000);
                    }
                    else
                    {
                        Console.WriteLine($"Error copying \"{fullSourcePath}\" to \"{fullDestinationPath}\": The file doesn't exist.");
                        Logger.Log($"Error copying \"{fullSourcePath}\" to \"{fullDestinationPath}\": The file doesn't exist.");
                    }
                }
                catch (Exception exception)
                {
                    if (i < NUMBER_OF_RETRIES - 1)
                    {
                        Thread.Sleep(1000);
                    }
                    else
                    {
                        Console.WriteLine($"Error copying \"{fullSourcePath}\" to \"{fullDestinationPath}\": {exception.Message}");
                        Logger.Log($"Error copying \"{fullSourcePath}\" to \"{fullDestinationPath}\": {exception.Message}");
                    }
                }
            }
        }

        private void HandleChangedEvent(string fullSourcePath)
        {
            FileFunctions.FileSystemElementType sourceElementType = FileFunctions.FileSystemElementType.FILE;
            for (int i = 0; i < NUMBER_OF_RETRIES; i++)
            {
                try
                {
                    sourceElementType = FileFunctions.GetFileSystemElementType(fullSourcePath);
                    break;
                }
                catch (FileNotFoundException)
                {
                    if (i < NUMBER_OF_RETRIES - 1)
                    {
                        Thread.Sleep(1000);
                    }
                    else
                    {
                        Console.WriteLine($"Error getting information on \"{fullSourcePath}\": The file system element doesn't exist.");
                        Logger.Log($"Error getting information on \"{fullSourcePath}\": The file system element doesn't exist.");
                    }
                }
                catch (Exception exception)
                {
                    if (i < NUMBER_OF_RETRIES - 1)
                    {
                        Thread.Sleep(1000);
                    }
                    else
                    {
                        Console.WriteLine($"Error getting information on \"{fullSourcePath}\": {exception.Message}");
                        Logger.Log($"Error getting information on \"{fullSourcePath}\": {exception.Message}");
                    }
                }
            }


            if (sourceElementType == FileFunctions.FileSystemElementType.DIRECTORY)
            {
                /* 
                 * In order to reduce the likelihood of events getting fired twice, the change events on directories will be ignored.
                 * Change events on directories that concern the directory itself can be handled as a created, renamed or deleted event. 
                 * Change events on directories that concern subdirectories and files will be handled by their respective handlers. 
                 */
                return;
            }

            Uri fullSourceUri = new Uri(fullSourcePath);
            Uri relativeSourceUri = new Uri(sourcePath).MakeRelativeUri(fullSourceUri);
            string relativeSourcePath = Uri.UnescapeDataString(relativeSourceUri.ToString());
            Uri fullDestinationUri = new Uri(new Uri(destinationPath), relativeSourcePath);
            string fullDestinationPath = Uri.UnescapeDataString(fullDestinationUri.LocalPath);
            for(int i = 0; i < NUMBER_OF_RETRIES; i++)
            {
                try
                {
                    FileFunctions.CopyFileOrDirectory(fullSourcePath, fullDestinationPath, createLocationIfNotExists: true);
                    break;
                }
                catch (FileNotFoundException)
                {
                    if (i < NUMBER_OF_RETRIES - 1)
                    {
                        Thread.Sleep(1000);
                    }
                    else
                    {
                        Console.WriteLine($"Error copying \"{fullSourcePath}\" to \"{fullDestinationPath}\": The file doesn't exist.");
                        Logger.Log($"Error copying \"{fullSourcePath}\" to \"{fullDestinationPath}\": The file doesn't exist.");
                    }
                }
                catch (Exception exception)
                {
                    if(i < NUMBER_OF_RETRIES - 1)
                    {
                        Thread.Sleep(1000);
                    }
                    else
                    {
                        Console.WriteLine($"Error copying \"{fullSourcePath}\" to \"{fullDestinationPath}\": {exception.Message}");
                        Logger.Log($"Error copying \"{fullSourcePath}\" to \"{fullDestinationPath}\": {exception.Message}");
                    }
                }
            }
            
        }

        private void HandleRenamedEvent(string oldFullPath, string newFullPath)
        {
            Uri oldFullSourceUri = new Uri(oldFullPath);
            Uri newFullSourceUri = new Uri(newFullPath);
            Uri oldRelativeSourceUri = new Uri(sourcePath).MakeRelativeUri(oldFullSourceUri);
            Uri newRelativeSourceUri = new Uri(sourcePath).MakeRelativeUri(newFullSourceUri);
            string oldRelativeSourcePath = Uri.UnescapeDataString(oldRelativeSourceUri.ToString());
            string newRelativeSourcePath = Uri.UnescapeDataString(newRelativeSourceUri.ToString());
            Uri oldFullDestinationUri = new Uri(new Uri(destinationPath), oldRelativeSourcePath);
            Uri newFullDestinationUri = new Uri(new Uri(destinationPath), newRelativeSourcePath);
            string oldFullDestinationPath = Uri.UnescapeDataString(oldFullDestinationUri.LocalPath);
            string newFullDestinationPath = Uri.UnescapeDataString(newFullDestinationUri.LocalPath);
            for (int i = 0; i < NUMBER_OF_RETRIES; i++)
            {
                try
                {
                    FileFunctions.MoveFileOrDirectory(oldFullDestinationPath, newFullDestinationPath);
                    break;
                }
                catch (FileNotFoundException)
                {
                    // File might have been renamed during the creation process.
                    HandleCreatedEvent(newFullPath);
                    break;
                }
                catch (Exception e)
                {
                    if (i < NUMBER_OF_RETRIES - 1)
                    {
                        Thread.Sleep(1000);
                    }
                    else
                    {
                        Console.WriteLine($"Error renaming \"{oldFullDestinationPath}\" to \"{newFullDestinationPath}\": {e.Message}");
                        Logger.Log($"Error renaming \"{oldFullDestinationPath}\" to \"{newFullDestinationPath}\": {e.Message}");
                    }
                }
            }
        }

        private void HandleDeletedEvent(string fullSourcePath)
        {
            Uri fullSourceUri = new Uri(fullSourcePath);
            Uri relativeSourceUri = new Uri(sourcePath).MakeRelativeUri(fullSourceUri);
            string relativeSourcePath = Uri.UnescapeDataString(relativeSourceUri.ToString());
            Uri fullDestinationUri = new Uri(new Uri(destinationPath), relativeSourcePath);
            string fullDestinationPath = Uri.UnescapeDataString(fullDestinationUri.LocalPath);
            for (int i = 0; i < NUMBER_OF_RETRIES; i++)
            {
                try
                {
                    FileFunctions.DeleteFileOrDirectory(fullDestinationPath);
                    break;
                }
                catch (FileNotFoundException)
                {
                    if (i < NUMBER_OF_RETRIES - 1)
                    {
                        Thread.Sleep(1000);
                    }
                    else
                    {
                        Console.WriteLine($"Error deleting \"{fullDestinationPath}\": the file could not be found.");
                        Logger.Log($"Error deleting \"{fullDestinationPath}\":  could not be found.");
                    }
                }
                catch (Exception exception)
                {
                    if (i < NUMBER_OF_RETRIES - 1)
                    {
                        Thread.Sleep(1000);
                    }
                    else
                    {
                        Console.WriteLine($"Error deleting \"{fullDestinationPath}\": {exception.Message}");
                        Logger.Log($"Error deleting \"{fullDestinationPath}\": {exception.Message}");
                    }
                }
            }
        }

        public string SourcePath {
            get { return sourcePath; }
            set{ if (!Running){ sourcePath = value; } }
        }
        public string DestinationPath {
            get { return destinationPath; }
            set{ if (!Running) { destinationPath = value; } }
        }
        public EventIDs Events {
            get { return events; }
            set { if (!Running) { events = value; } }
        }
        public bool Running { get; private set; }
    }
}
