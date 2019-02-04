namespace FileSystemChangeReplicator.FileSystemWatcher
{
    class FileSystemWatcher
    {
        [System.Flags] public enum EventIDs {NONE = 0, CREATED = 1, CHANGED = 2, RENAMED = 4, DELETED = 8, ALL = 15}
        private string sourcePath;
        private string destinationPath;
        private EventIDs events;
        private System.IO.FileSystemWatcher watcher;
        private readonly System.Runtime.Caching.MemoryCache memoryCache;
        private readonly System.Runtime.Caching.CacheItemPolicy cacheItemPolicy;
        private const int CACHE_TIME_MILLISECONDS = 1000;
        private const int NUMBER_OF_RETRIES = 5;
        public class FileSystemWatcherEventDescription
        {
            public EventIDs type;
            public string currentFilePath;
            public string previousFilePath;
        }

        public FileSystemWatcher(string sourcePath, string destinationPath, EventIDs events)
        {
            this.sourcePath = sourcePath;
            this.destinationPath = destinationPath;
            this.events = events;
            Running = false;
            watcher = new System.IO.FileSystemWatcher();
            memoryCache = System.Runtime.Caching.MemoryCache.Default;
            cacheItemPolicy = new System.Runtime.Caching.CacheItemPolicy()
            {
                RemovedCallback = OnRemovedFromCache
            };
        }

        ~FileSystemWatcher()
        {
            watcher.Dispose();
            watcher = null;
        }

        public void Start()
        {
            watcher.Filter = "*";
            watcher.IncludeSubdirectories = true;
            watcher.NotifyFilter = System.IO.NotifyFilters.LastWrite | System.IO.NotifyFilters.FileName | System.IO.NotifyFilters.DirectoryName;
            watcher.Path = sourcePath;

            // Enable events.
            if ((events & EventIDs.CREATED) > 0)
            {
                watcher.Created += new System.IO.FileSystemEventHandler(OnCreated);
            }
            if ((events & EventIDs.CHANGED) > 0)
            {
                watcher.Changed += new System.IO.FileSystemEventHandler(OnChanged);
            }
            if ((events & EventIDs.RENAMED) > 0)
            {
                watcher.Renamed += new System.IO.RenamedEventHandler(OnRenamed);
            }
            if ((events & EventIDs.DELETED) > 0)
            {
                watcher.Deleted += new System.IO.FileSystemEventHandler(OnDeleted);
            }
            watcher.Error += new System.IO.ErrorEventHandler(OnError);
            watcher.EnableRaisingEvents = true;
            Running = true;
        }

        public void Stop()
        {
            // Disable events.
            watcher.Created -= new System.IO.FileSystemEventHandler(OnCreated);
            watcher.Changed -= new System.IO.FileSystemEventHandler(OnChanged);
            watcher.Renamed -= new System.IO.RenamedEventHandler(OnRenamed);
            watcher.Deleted -= new System.IO.FileSystemEventHandler(OnDeleted);
            watcher.Error -= new System.IO.ErrorEventHandler(OnError);
            watcher.EnableRaisingEvents = false;
            Running = false;
        }

        private void OnChanged(object source, System.IO.FileSystemEventArgs myEvent)
        {
            cacheItemPolicy.AbsoluteExpiration = System.DateTimeOffset.Now.AddMilliseconds(CACHE_TIME_MILLISECONDS);

            // Only add if it is not there already (swallow others)
            FileSystemWatcherEventDescription eventParameters = new FileSystemWatcherEventDescription()
            {
                type = EventIDs.CHANGED,
                currentFilePath = myEvent.FullPath,
                previousFilePath = null
            };
            memoryCache.AddOrGetExisting($"{myEvent.FullPath}_Changed", eventParameters, cacheItemPolicy);
        }

        private void OnCreated(object source, System.IO.FileSystemEventArgs myEvent)
        {
            cacheItemPolicy.AbsoluteExpiration = System.DateTimeOffset.Now.AddMilliseconds(CACHE_TIME_MILLISECONDS);

            // Only add if it is not there already (swallow others)
            FileSystemWatcherEventDescription eventParameters = new FileSystemWatcherEventDescription()
            {
                type = EventIDs.CREATED,
                currentFilePath = myEvent.FullPath,
                previousFilePath = null
            };
            memoryCache.AddOrGetExisting($"{myEvent.FullPath}_Created", eventParameters, cacheItemPolicy);
        }

        private void OnRenamed(object source, System.IO.RenamedEventArgs myEvent)
        {
            cacheItemPolicy.AbsoluteExpiration = System.DateTimeOffset.Now.AddMilliseconds(CACHE_TIME_MILLISECONDS);

            // Only add if it is not there already (swallow others)
            FileSystemWatcherEventDescription eventParameters = new FileSystemWatcherEventDescription()
            {
                type = EventIDs.RENAMED,
                currentFilePath = myEvent.FullPath,
                previousFilePath = myEvent.OldFullPath
            };
            memoryCache.AddOrGetExisting($"{myEvent.FullPath}_Renamed", eventParameters, cacheItemPolicy);
        }

        private void OnDeleted(object source, System.IO.FileSystemEventArgs myEvent)
        {
            cacheItemPolicy.AbsoluteExpiration = System.DateTimeOffset.Now.AddMilliseconds(System.Math.Floor((double)CACHE_TIME_MILLISECONDS/2));

            // Only add if it is not there already (swallow others)
            FileSystemWatcherEventDescription eventParameters = new FileSystemWatcherEventDescription()
            {
                type = EventIDs.DELETED,
                currentFilePath = myEvent.FullPath,
                previousFilePath = null
            };
            memoryCache.AddOrGetExisting($"{myEvent.FullPath}_Deleted", eventParameters, cacheItemPolicy);
        }

        private void OnError(object source, System.IO.ErrorEventArgs myEvent)
        {
            if (myEvent.GetException().GetType() == typeof(System.IO.InternalBufferOverflowException))
            {
                //  This can happen if Windows is reporting many file system events quickly 
                //  and internal buffer of the  FileSystemWatcher is not large enough to handle this
                //  rate of events. The InternalBufferOverflowException error informs the application
                //  that some of the file system events are being lost.
                System.Console.WriteLine($"The file system watcher experienced an internal buffer overflow: {myEvent.GetException().Message}.");
                Logging.Logger.Log($"The file system watcher experienced an internal buffer overflow: {myEvent.GetException().Message}.");
            }
            else
            {
                System.Console.WriteLine($"The file sytem watcher has trigerred an unknown error: {myEvent.GetException().Message}.");
                Logging.Logger.Log($"The file sytem watcher has trigerred an unknown error: {myEvent.GetException().Message}.");
            }
        }

        private void OnRemovedFromCache(System.Runtime.Caching.CacheEntryRemovedArguments arguments)
        {
            if (arguments.RemovedReason != System.Runtime.Caching.CacheEntryRemovedReason.Expired)
            {
                return;
            }

            new System.Threading.Thread(
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
            System.Uri fullSourceUri = new System.Uri(fullSourcePath);
            System.Uri relativeSourceUri = new System.Uri(sourcePath).MakeRelativeUri(fullSourceUri);
            string relativeSourcePath = System.Uri.UnescapeDataString(relativeSourceUri.ToString());
            System.Uri fullDestinationUri = new System.Uri(new System.Uri(destinationPath), relativeSourcePath);
            string fullDestinationPath = System.Uri.UnescapeDataString(fullDestinationUri.LocalPath);
            for (int i = 0; i < NUMBER_OF_RETRIES; i++)
            {
                try
                {
                    FileFunctions.FileFunctions.CopyFileOrDirectory(fullSourcePath, fullDestinationPath);
                    return;
                }
                catch (System.IO.FileNotFoundException)
                {
                    System.Console.WriteLine($"Error copying \"{fullSourcePath}\" to \"{fullDestinationPath}\": The file doesn't exist.");
                    Logging.Logger.Log($"Error copying \"{fullSourcePath}\" to \"{fullDestinationPath}\": The file doesn't exist.");
                    return;
                }
                catch (System.Exception e)
                {
                    if (i < NUMBER_OF_RETRIES - 1)
                    {
                        System.Threading.Thread.Sleep(1000);
                    }
                    else
                    {
                        System.Console.WriteLine($"Error copying \"{fullSourcePath}\" to \"{fullDestinationPath}\": {e.Message}.");
                        Logging.Logger.Log($"Error copying \"{fullSourcePath}\" to \"{fullDestinationPath}\": {e.Message}.");
                    }
                }
            }
        }

        private void HandleChangedEvent(string fullSourcePath)
        {
            if(FileFunctions.FileFunctions.GetFileSystemElementType(fullSourcePath) == FileFunctions.FileFunctions.FileSystemElementType.DIRECTORY)
            {
                /* 
                 * In order to reduce the likelihood of events getting fired twice, the change events on directories will be ignored.
                 * Change events on directories that concern the directory itself can be handled as a created, renamed or deleted event. 
                 * Change events on directories that concern subdirectories and files will be handled by their respective handlers. 
                 */
                return;
            }

            System.Uri fullSourceUri = new System.Uri(fullSourcePath);
            System.Uri relativeSourceUri = new System.Uri(sourcePath).MakeRelativeUri(fullSourceUri);
            string relativeSourcePath = System.Uri.UnescapeDataString(relativeSourceUri.ToString());
            System.Uri fullDestinationUri = new System.Uri(new System.Uri(destinationPath), relativeSourcePath);
            string fullDestinationPath = System.Uri.UnescapeDataString(fullDestinationUri.LocalPath);
            for(int i = 0; i < NUMBER_OF_RETRIES; i++)
            {
                try
                {
                    FileFunctions.FileFunctions.CopyFileOrDirectory(fullSourcePath, fullDestinationPath);
                    return;
                }
                catch (System.IO.FileNotFoundException)
                {
                    System.Console.WriteLine($"Error copying \"{fullSourcePath}\" to \"{fullDestinationPath}\": The file doesn't exist.");
                    Logging.Logger.Log($"Error copying \"{fullSourcePath}\" to \"{fullDestinationPath}\": The file doesn't exist.");
                    return;
                }
                catch (System.Exception e)
                {
                    if(i < NUMBER_OF_RETRIES - 1)
                    {
                        System.Threading.Thread.Sleep(1000);
                    }
                    else
                    {
                        System.Console.WriteLine($"Error copying \"{fullSourcePath}\" to \"{fullDestinationPath}\": {e.Message}.");
                        Logging.Logger.Log($"Error copying \"{fullSourcePath}\" to \"{fullDestinationPath}\": {e.Message}.");
                    }
                }
            }
            
        }

        private void HandleRenamedEvent(string oldFullPath, string newFullPath)
        {
            System.Uri oldFullSourceUri = new System.Uri(oldFullPath);
            System.Uri newFullSourceUri = new System.Uri(newFullPath);
            System.Uri oldRelativeSourceUri = new System.Uri(sourcePath).MakeRelativeUri(oldFullSourceUri);
            System.Uri newRelativeSourceUri = new System.Uri(sourcePath).MakeRelativeUri(newFullSourceUri);
            string oldRelativeSourcePath = System.Uri.UnescapeDataString(oldRelativeSourceUri.ToString());
            string newRelativeSourcePath = System.Uri.UnescapeDataString(newRelativeSourceUri.ToString());
            System.Uri oldFullDestinationUri = new System.Uri(new System.Uri(destinationPath), oldRelativeSourcePath);
            System.Uri newFullDestinationUri = new System.Uri(new System.Uri(destinationPath), newRelativeSourcePath);
            string oldFullDestinationPath = System.Uri.UnescapeDataString(oldFullDestinationUri.LocalPath);
            string newFullDestinationPath = System.Uri.UnescapeDataString(newFullDestinationUri.LocalPath);
            for (int i = 0; i < NUMBER_OF_RETRIES; i++)
            {
                try
                {
                    FileFunctions.FileFunctions.MoveFileOrDirectory(oldFullDestinationPath, newFullDestinationPath);
                }
                catch (System.IO.FileNotFoundException)
                {
                    // File might have been renamed during the creation process.
                    HandleCreatedEvent(newFullPath);
                    return;
                }
                catch (System.Exception e)
                {
                    if (i < NUMBER_OF_RETRIES - 1)
                    {
                        System.Threading.Thread.Sleep(1000);
                    }
                    else
                    {
                        System.Console.WriteLine($"Error renaming \"{oldFullDestinationPath}\" to \"{newFullDestinationPath}\": {e.Message}.");
                        Logging.Logger.Log($"Error renaming \"{oldFullDestinationPath}\" to \"{newFullDestinationPath}\": {e.Message}.");
                    }
                }
            }
        }

        private void HandleDeletedEvent(string fullSourcePath)
        {
            System.Uri fullSourceUri = new System.Uri(fullSourcePath);
            System.Uri relativeSourceUri = new System.Uri(sourcePath).MakeRelativeUri(fullSourceUri);
            string relativeSourcePath = System.Uri.UnescapeDataString(relativeSourceUri.ToString());
            System.Uri fullDestinationUri = new System.Uri(new System.Uri(destinationPath), relativeSourcePath);
            string fullDestinationPath = System.Uri.UnescapeDataString(fullDestinationUri.LocalPath);
            for (int i = 0; i < NUMBER_OF_RETRIES; i++)
            {
                try
                {
                    FileFunctions.FileFunctions.DeleteFileOrDirectory(fullDestinationPath);
                }
                catch (System.IO.FileNotFoundException)
                {
                    return;
                }
                catch (System.Exception e)
                {
                    if (i < NUMBER_OF_RETRIES - 1)
                    {
                        System.Threading.Thread.Sleep(1000);
                    }
                    else
                    {
                        System.Console.WriteLine($"Error deleting \"{fullDestinationPath}\": {e.Message}.");
                        Logging.Logger.Log($"Error deleting \"{fullDestinationPath}\": {e.Message}.");
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
