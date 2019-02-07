namespace FileSystemChangeReplicator
{
    using FileSystemChangeReplicator.Logging;
    using FileSystemChangeReplicator.FileSystemWatcher;
    using System.Windows.Forms;
    using System.IO;
    using System;

    class AppContext : System.Windows.Forms.ApplicationContext
    {
        private readonly System.Collections.Hashtable properties;
        private NotifyIcon trayIcon;
        private FileSystemWatcher2 fileSystemWatcher;
        private ToolStripMenuItem startSynchronizationMenuItem;
        private ToolStripMenuItem stopSynchronizationMenuItem;
        private ToolStripMenuItem exitMenuItem;
        private ContextMenuStrip trayIconContextMenu;

        public AppContext()
        {
            properties = ConfigINI.GetInstance().Items;

            if (properties.ContainsKey("SOURCE_PATH"))
            {
                SourcePath = properties["SOURCE_PATH"].ToString();
                if (!FileFunctions.FileFunctions.IsValidRootedPath(SourcePath))
                {
                    MessageBox.Show($"Source path \"{SourcePath}\" is not a valid rooted path.", "Fermeture du programme");
                    Application.Exit();
                }
            }
            else
            {
                throw new Exception("Source path is undefined.");
            }

            if (properties.ContainsKey("DESTINATION_PATH"))
            {
                DestinationPath = properties["DESTINATION_PATH"].ToString();
                if (!FileFunctions.FileFunctions.IsValidRootedPath(DestinationPath))
                {
                    MessageBox.Show($"Destination path \"{DestinationPath}\" is not a valid rooted path.", "Fermeture du programme");
                    Application.Exit();
                }
            }
            else
            {
                throw new Exception("Destination path is undefined.");
            }

            CreateSystemTrayIcon();
            Application.ApplicationExit += OnApplicationExit;

            FileSystemWatcher2.EventIDs events = FileSystemWatcher2.EventIDs.NONE;
            if (properties.ContainsKey("MANAGE_EVENTS"))
            {
                foreach(string eventName in properties["MANAGE_EVENTS"].ToString().Split(','))
                {
                    switch (eventName)
                    {
                        case "Created":
                            events |= FileSystemWatcher2.EventIDs.CREATED;
                            break;
                        case "Changed":
                            events |= FileSystemWatcher2.EventIDs.CHANGED;
                            break;
                        case "Renamed":
                            events |= FileSystemWatcher2.EventIDs.RENAMED;
                            break;
                        case "Deleted":
                            events |= FileSystemWatcher2.EventIDs.DELETED;
                            break;
                        default:
                            // This is an invalid event name.
                            break;
                    }
                }
            }
            else
            {
                events = FileSystemWatcher2.EventIDs.ALL;
            }
            fileSystemWatcher = new FileSystemWatcher2(SourcePath, DestinationPath, events);
            StartFileSystemWatcher();
        }

        private void CreateSystemTrayIcon()
        {
            // ExitMenuItem
            exitMenuItem = new ToolStripMenuItem
            {
                Name = "Exit",
                Size = new System.Drawing.Size(152, 22),
                Text = "Quitter"
            };
            exitMenuItem.Click += ExitMenuItem_Click;

            // StopSynchronisationMenuItem
            stopSynchronizationMenuItem = new ToolStripMenuItem
            {
                Name = "Stop synchronization",
                Size = new System.Drawing.Size(152, 22),
                Text = "Arrêter la synchronisation"
            };
            stopSynchronizationMenuItem.Click += StopSynchronizationMenuItem_Click;

            // StartSynchronisationMenuItem
            startSynchronizationMenuItem = new ToolStripMenuItem
            {
                Name = "Start synchronization",
                Size = new System.Drawing.Size(152, 22),
                Text = "Démarrer la synchronisation"
            };
            startSynchronizationMenuItem.Click += StartSynchronizationMenuItem_Click;

            // TrayIconContextMenu
            trayIconContextMenu = new ContextMenuStrip()
            {
                Name = "Tray Icon Context Menu",
                Size = new System.Drawing.Size(153, 50)
            };
            trayIconContextMenu.SuspendLayout();
            ToolStripItem[] menuItems = new ToolStripItem[]
            {
                stopSynchronizationMenuItem,
                startSynchronizationMenuItem,
                exitMenuItem,
            };
            trayIconContextMenu.Items.AddRange(menuItems);

            // TrayIcon
            trayIcon = new NotifyIcon
            {
                BalloonTipIcon = ToolTipIcon.Info,
                BalloonTipText = "Cliquer avec le bouton de droite pour avoir les options.",
                BalloonTipTitle = "FileSystemChangeReplicator",
                Text = "FileSystemChangeReplicator",
                Visible = true,
                ContextMenuStrip = trayIconContextMenu,
            };
            trayIcon.DoubleClick += TrayIcon_DoubleClick;

            if (properties.ContainsKey("TRAY_ICON"))
            {
                string applicationPath = Application.StartupPath;
                string iconPath = Path.Combine(applicationPath, "Graphics", properties["TRAY_ICON"].ToString());
                if (File.Exists(iconPath))
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
            string message = $"Voulez-vous vraiment quitter le programme de synchronisation entre les répertoires \"{SourcePath}\"" +
                $" et \"{DestinationPath}\"?";
            string title = "Confirmation";
            MessageBoxButtons buttons = MessageBoxButtons.YesNo;
            MessageBoxIcon icon = MessageBoxIcon.Exclamation;
            MessageBoxDefaultButton defaultButton = MessageBoxDefaultButton.Button2;

            if (MessageBox.Show(message, title, buttons, icon, defaultButton) == DialogResult.Yes)
            {
                StopFileSystemWatcher();
                Application.Exit();
            }
        }

        private void StartFileSystemWatcher()
        {
            fileSystemWatcher.Start();
            startSynchronizationMenuItem.Visible = false;
            stopSynchronizationMenuItem.Visible = true;
            Logger.Log($"La synchronisation entre les répertoires \"{SourcePath}\" et \"{DestinationPath}\" a été démarrée.");
        }

        private void StopFileSystemWatcher()
        {
            fileSystemWatcher.Stop();
            stopSynchronizationMenuItem.Visible = false;
            startSynchronizationMenuItem.Visible = true;
            Logger.Log($"La synchronisation entre les répertoires \"{SourcePath}\" et \"{DestinationPath}\" a été arrêtée.");
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
