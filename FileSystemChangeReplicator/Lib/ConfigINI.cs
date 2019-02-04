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
    public class ConfigINI
    {
        public static ConfigINI GetInstance()
        {
            if (Instance == null)
            {
                System.Uri applicationDirectoryUri = new System.Uri(System.AppDomain.CurrentDomain.BaseDirectory);
                System.Uri applicationIniFileUri = new System.Uri(applicationDirectoryUri, "config.ini");
                string applicationIniFilePath = System.Uri.UnescapeDataString(applicationIniFileUri.LocalPath);
                Instance = new ConfigINI(applicationIniFilePath);
            }

            return Instance;
        }

        private ConfigINI(string sINIPath)
        {

            if (System.IO.File.Exists(sINIPath))
            {
                ReadINIFile(sINIPath);
            }
            else
            {
                System.Windows.Forms.MessageBox.Show($"File \"{sINIPath}\" not found!");
                System.Environment.Exit(0);
            }
        }


        private void ReadINIFile(string sINIPath)
        {
            using (System.IO.StreamReader objReader = new System.IO.StreamReader(sINIPath))
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
        public System.Collections.Hashtable Items { get; } = new System.Collections.Hashtable();
    }
}
