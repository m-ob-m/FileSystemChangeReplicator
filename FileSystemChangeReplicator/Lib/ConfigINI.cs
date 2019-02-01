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
                Instance = new ConfigINI(System.Windows.Forms.Application.StartupPath + "\\config.ini");
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
