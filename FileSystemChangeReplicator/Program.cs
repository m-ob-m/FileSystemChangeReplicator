namespace FileSystemChangeReplicator
{
    using System.Windows.Forms;
    using System;
    using Logging;

    class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += 
                (sender, arguments) => HandleUnhandledException(arguments.ExceptionObject as Exception);
            Application.ThreadException += (sender, arguments) => HandleUnhandledException(arguments.Exception);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new AppContext());
        }

        static void HandleUnhandledException(Exception exception)
        {
            Console.WriteLine($"The following is an uncaught exception message.\n{exception.Message}: {exception.StackTrace}");
            Logger.Log($"The following is an uncaught exception message.\n{exception.Message}: {exception.StackTrace}");
        }
    }
}
