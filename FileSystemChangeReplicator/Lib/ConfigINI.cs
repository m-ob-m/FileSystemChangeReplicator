/**
 * \name		ConfigINI
* \author    	Mathieu Grenier
* \version		1.0
* \date       	2017-02-27
*
* \brief 		Cette classe Singleton lit le fichier INI et organise les lignes de DB du fichier config.ini
*/
namespace FileSystemChangeReplicator
{
    using System.Collections;
    using System.IO;
    using System.Windows.Forms;
    using System;

    public class ConfigINI
    {
        public static ConfigINI GetInstance()
        {
            if (Instance == null)
            {
                Uri applicationDirectoryUri = new Uri(AppDomain.CurrentDomain.BaseDirectory);
                Uri applicationIniFileUri = new Uri(applicationDirectoryUri, "config.ini");
                string applicationIniFilePath = Uri.UnescapeDataString(applicationIniFileUri.LocalPath);
                Instance = new ConfigINI(applicationIniFilePath);
            }

            return Instance;
        }

        private ConfigINI(string sINIPath)
        {

            if (File.Exists(sINIPath))
            {
                ReadINIFile(sINIPath);
            }
            else
            {
                MessageBox.Show($"File \"{sINIPath}\" not found!");
                Environment.Exit(0);
            }
        }


        private void ReadINIFile(string sINIPath)
        {
            using (StreamReader objReader = new StreamReader(sINIPath))
            {
                while (objReader.Peek() != -1)
                {
                    string str_line = objReader.ReadLine();
                    if (str_line.IndexOf('=') > 0)
                    {
                        string[] str_value = str_line.Split('=');
                        if(str_value.Length == 2)
                        {
                            Items.Add(str_value[0], str_value[1]);
                        }
                    }
                }
            }
        }

        private static ConfigINI Instance = null;
        public Hashtable Items { get; } = new Hashtable();
    }
}
