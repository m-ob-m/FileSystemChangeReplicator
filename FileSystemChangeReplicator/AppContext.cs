namespace FileSystemChangeReplicator
{
    class AppContext : System.Windows.Forms.ApplicationContext
    {
        private readonly System.Collections.Hashtable properties;
        private System.Windows.Forms.NotifyIcon trayIcon;
        private FileSystemWatcher.FileSystemWatcher fileSystemWatcher;
        private System.Windows.Forms.ToolStripMenuItem startSynchronizationMenuItem;
        private System.Windows.Forms.ToolStripMenuItem stopSynchronizationMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exitMenuItem;
        private System.Windows.Forms.ContextMenuStrip trayIconContextMenu;

        public AppContext()
        {
            properties = ConfigINI.GetInstance().Items;

            if (properties.ContainsKey("SOURCE_PATH"))
            {
                SourcePath = properties["SOURCE_PATH"].ToString();
                if (!FileFunctions.FileFunctions.IsValidRootedPath(SourcePath))
                {
                    throw new System.Exception($"Source path \"{SourcePath}\" is not a valid rooted path.");
                }
            }
            else
            {
                throw new System.Exception("Source path is undefined.");
            }

            if (properties.ContainsKey("DESTINATION_PATH"))
            {
                DestinationPath = properties["DESTINATION_PATH"].ToString();
                if (!FileFunctions.FileFunctions.IsValidRootedPath(DestinationPath))
                {
                    throw new System.Exception($"Destination path \"{DestinationPath}\" is not a valid rooted path.");
                }
            }
            else
            {
                throw new System.Exception("Destination path is undefined.");
            }

            CreateSystemTrayIcon();
            System.Windows.Forms.Application.ApplicationExit += OnApplicationExit;

            FileSystemWatcher.FileSystemWatcher.EventIDs events = FileSystemWatcher.FileSystemWatcher.EventIDs.NONE;
            if (properties.ContainsKey("MANAGE_EVENTS"))
            {
                foreach(string eventName in properties["MANAGE_EVENTS"].ToString().Split(','))
                {
                    switch (eventName)
                    {
                        case "Created":
                            events |= FileSystemWatcher.FileSystemWatcher.EventIDs.CREATED;
                            break;
                        case "Changed":
                            events |= FileSystemWatcher.FileSystemWatcher.EventIDs.CHANGED;
                            break;
                        case "Renamed":
                            events |= FileSystemWatcher.FileSystemWatcher.EventIDs.RENAMED;
                            break;
                        case "Deleted":
                            events |= FileSystemWatcher.FileSystemWatcher.EventIDs.DELETED;
                            break;
                        default:
                            // This is an invalid event name.
                            break;
                    }
                }
            }
            else
            {
                events = FileSystemChangeReplicator.FileSystemWatcher.FileSystemWatcher.EventIDs.ALL;
            }
            fileSystemWatcher = new FileSystemWatcher.FileSystemWatcher(SourcePath, DestinationPath, events);
            StartFileSystemWatcher();
        }

        private void CreateSystemTrayIcon()
        {
            // ExitMenuItem
            exitMenuItem = new System.Windows.Forms.ToolStripMenuItem
            {
                Name = "Exit",
                Size = new System.Drawing.Size(152, 22),
                Text = "Quitter"
            };
            exitMenuItem.Click += ExitMenuItem_Click;

            // StopSynchronisationMenuItem
            stopSynchronizationMenuItem = new System.Windows.Forms.ToolStripMenuItem
            {
                Name = "Stop synchronization",
                Size = new System.Drawing.Size(152, 22),
                Text = "Arrêter la synchronisation"
            };
            stopSynchronizationMenuItem.Click += StopSynchronizationMenuItem_Click;

            // StartSynchronisationMenuItem
            startSynchronizationMenuItem = new System.Windows.Forms.ToolStripMenuItem
            {
                Name = "Start synchronization",
                Size = new System.Drawing.Size(152, 22),
                Text = "Démarrer la synchronisation"
            };
            startSynchronizationMenuItem.Click += StartSynchronizationMenuItem_Click;

            // TrayIconContextMenu
            trayIconContextMenu = new System.Windows.Forms.ContextMenuStrip()
            {
                Name = "Tray Icon Context Menu",
                Size = new System.Drawing.Size(153, 50)
            };
            trayIconContextMenu.SuspendLayout();
            System.Windows.Forms.ToolStripItem[] menuItems = new System.Windows.Forms.ToolStripItem[]
            {
                stopSynchronizationMenuItem,
                startSynchronizationMenuItem,
                exitMenuItem,
            };
            trayIconContextMenu.Items.AddRange(menuItems);

            // TrayIcon
            trayIcon = new System.Windows.Forms.NotifyIcon
            {
                BalloonTipIcon = System.Windows.Forms.ToolTipIcon.Info,
                BalloonTipText = "Cliquer avec le bouton de droite pour avoir les options.",
                BalloonTipTitle = "FileSystemChangeReplicator",
                Text = "FileSystemChangeReplicator",
                Visible = true,
                ContextMenuStrip = trayIconContextMenu,
            };
            trayIcon.DoubleClick += TrayIcon_DoubleClick;

            if (properties.ContainsKey("TRAY_ICON"))
            {
                string applicationPath = System.Windows.Forms.Application.StartupPath;
                string iconPath = System.IO.Path.Combine(applicationPath, "Graphics", properties["TRAY_ICON"].ToString());
                if (System.IO.File.Exists(iconPath))
                {
                    trayIcon.Icon = new System.Drawing.Icon(iconPath);
                }
                else
                {
                    trayIcon.Icon = System.Drawing.SystemIcons.Application;
                }
            }
            else
            {
                trayIcon.Icon = System.Drawing.SystemIcons.Application;
            }

            trayIconContextMenu.ResumeLayout(true);
        }

        private void TrayIcon_DoubleClick(object sender, System.EventArgs myEvent)
        {
            //Here you can do stuff if the tray icon is doubleclicked.
            trayIcon.ShowBalloonTip(10000);
        }

        private void StopSynchronizationMenuItem_Click(object sender, System.EventArgs myEvent)
        {
            StopFileSystemWatcher();
        }

        private void StartSynchronizationMenuItem_Click(object sender, System.EventArgs myEvent)
        {
            StartFileSystemWatcher();
        }

        private void ExitMenuItem_Click(object sender, System.EventArgs myEvent)
        {
            string message = $"Voulez-vous vraiment quitter le programme de synchronisation entre les répertoires \"{SourcePath}\" et \"{DestinationPath}\" ?";
            string title = "Confirmation";
            System.Windows.Forms.MessageBoxButtons buttons = System.Windows.Forms.MessageBoxButtons.YesNo;
            System.Windows.Forms.MessageBoxIcon icon = System.Windows.Forms.MessageBoxIcon.Exclamation;
            System.Windows.Forms.MessageBoxDefaultButton defaultButton = System.Windows.Forms.MessageBoxDefaultButton.Button2;

            if (System.Windows.Forms.MessageBox.Show(message, title, buttons, icon, defaultButton) == System.Windows.Forms.DialogResult.Yes)
            {
                StopFileSystemWatcher();
                System.Windows.Forms.Application.Exit();
            }
        }

        private void StartFileSystemWatcher()
        {
            fileSystemWatcher.Start();
            startSynchronizationMenuItem.Visible = false;
            stopSynchronizationMenuItem.Visible = true;

        }

        private void StopFileSystemWatcher()
        {
            fileSystemWatcher.Stop();
            stopSynchronizationMenuItem.Visible = false;
            startSynchronizationMenuItem.Visible = true;
        }

        private void OnApplicationExit(object sender, System.EventArgs myEvent)
        {
            //Cleanup so that the icon will be removed when the application is closed
            trayIcon.Visible = false;
        }

        public string SourcePath { get; }
        public string DestinationPath { get; }
    }
}
