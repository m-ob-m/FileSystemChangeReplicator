namespace FileSystemChangeReplicator
{
    class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [System.STAThread]
        static void Main(string[] args)
        {
            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                System.Windows.Forms.Application.Run(new AppContext());
            }
            catch (System.Exception exception)
            {
                System.Console.WriteLine(exception.StackTrace);
                using (System.IO.StreamWriter sw = System.IO.File.AppendText("error.txt"))
                {
                    sw.WriteLine(exception.Message + "\n" + exception.StackTrace);
                }
            }
        }
    }
}
