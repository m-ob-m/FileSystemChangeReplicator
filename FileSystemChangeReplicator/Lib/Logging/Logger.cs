namespace FileSystemChangeReplicator.Logging
{
    public static class Logger
    {
        private static string text = "";

        public static void Log(string text)
        {
            System.Uri applicationDirectoryUri = new System.Uri(System.AppDomain.CurrentDomain.BaseDirectory);
            System.Uri applicationLogFileUri = new System.Uri(applicationDirectoryUri, "log.txt");
            string applicationLogFilePath = System.Uri.UnescapeDataString(applicationLogFileUri.LocalPath);
            string currentDateTime = System.DateTime.Now.ToString("F");
            Logger.text += $"{currentDateTime}\t{text}\n";
            try
            {
                using (System.IO.StreamWriter logFile = new System.IO.StreamWriter(applicationLogFilePath, true))
                {
                    logFile.WriteLine(Logger.text);
                    logFile.Close();
                }
                text = "";
            }
            catch (System.Exception)
            {
                Logger.text += $"Cannot access logfile. This error will be logged when possible.\n" ;
            }
        }
    }
}