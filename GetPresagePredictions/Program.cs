using JuliusSweetland.OptiKey.Enums;
using JuliusSweetland.OptiKey.Properties;
using JuliusSweetland.OptiKey.Static;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows;
using System.Text;
using System.Threading.Tasks;
using presage;
using JuliusSweetland.OptiKey.Services.Suggestions;
using System.Windows.Forms;

namespace GetPresagePredictions
{
    class Program
    {
        static void Exit()
        {
            Console.WriteLine("Exitting");
            Console.ReadLine();
        }

        [STAThreadAttribute]
        static void Main(string[] args)
        {
            Settings.Initialise();

            //1.Check the install path to detect if the wrong bitness of Presage is installed
            string presagePath = null;
            string presageStartMenuFolder = null;
            string osBitness = DiagnosticInfo.OperatingSystemBitness;

            try
            {
                presagePath = Registry.GetValue("HKEY_CURRENT_USER\\Software\\Presage", "", string.Empty).ToString();
                presageStartMenuFolder = Registry.GetValue("HKEY_CURRENT_USER\\Software\\Presage", "Start Menu Folder", string.Empty).ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format("Caught exception: {0}", ex));
            }

            Console.WriteLine(String.Format("Presage path: {0} \n Presage start menu folder: {1} \n OS bitness: {2}", presagePath, presageStartMenuFolder, osBitness));

            if (string.IsNullOrEmpty(presagePath)
                || string.IsNullOrEmpty(presageStartMenuFolder))
            {
                Settings.Default.SuggestionMethod = SuggestionMethods.NGram;
                Console.WriteLine("Invalid Presage installation detected (path(s) missing). Must install 'presage-0.9.1-32bit' or 'presage-0.9.2~beta20150909-32bit'. Changed SuggesionMethod to NGram.");
                Exit();
            }

            if (presageStartMenuFolder != "presage-0.9.2~beta20150909"
                && presageStartMenuFolder != "presage-0.9.1")
            {
                Settings.Default.SuggestionMethod = SuggestionMethods.NGram;
                Console.WriteLine("Invalid Presage installation detected (valid version not detected). Must install 'presage-0.9.1-32bit' or 'presage-0.9.2~beta20150909-32bit'. Changed SuggesionMethod to NGram.");
                Exit();
            }

            if ((osBitness == "64-Bit" && presagePath != @"C:\Program Files (x86)\presage")
                || (osBitness == "32-Bit" && presagePath != @"C:\Program Files\presage"))
            {
                Settings.Default.SuggestionMethod = SuggestionMethods.NGram;
                Console.WriteLine("Invalid Presage installation detected (incorrect bitness? Install location is suspect). Must install 'presage-0.9.1-32bit' or 'presage-0.9.2~beta20150909-32bit'. Changed SuggesionMethod to NGram.");
                Exit();
            }

            if (!Directory.Exists(presagePath))
            {
                Settings.Default.SuggestionMethod = SuggestionMethods.NGram;
                Console.WriteLine("Invalid Presage installation detected (install directory does not exist). Must install 'presage-0.9.1-32bit' or 'presage-0.9.2~beta20150909-32bit'. Changed SuggesionMethod to NGram.");
                Exit();
            }

            //2.Attempt to construct a Presage object, which can fail for a few reasons, including BadImageFormatExceptions (64-bit version installed)
            Presage presageTestInstance = null;
            try
            {
                //Test Presage constructor call for DllNotFoundException or BadImageFormatException
                presageTestInstance = new Presage(() => "", () => "");
            }
            catch (Exception ex)
            {
                //If the installed version of Presage is the wrong format (i.e. 64 bit) then a BadImageFormatException can occur.
                //If Presage has been deleted, corrupted, or another problem, then a DllLoadException can occur.
                //These causes an additional problem as the Presage object will probably be non-deterministically
                //finalised, which will cause this exception again and crash OptiKey. The workaround is to suppress finalisation 
                //if an object is available (which it generally won't be!), or to warn the user and react.

                //Set the suggestion method to NGram so that the IDictionaryService can be instantiated without crashing OptiKey
                Settings.Default.SuggestionMethod = SuggestionMethods.NGram;
                Settings.Default.Save(); //Save as OptiKey can crash as soon as the finaliser is called by the GC
                Console.WriteLine("Presage failed to bootstrap - attempting to suppress finalisation. The suggestion method has been changed to NGram", ex);

                //Attempt to suppress finalisation on the presage instance, or just warn the user
                if (presageTestInstance != null)
                {
                    GC.SuppressFinalize(presageTestInstance);
                }
                else
                {
                    Console.WriteLine("exception");
                }

                Exit();
            }
            // set the config
            // force path:
            Settings.Default.PresageDatabaseLocation = @"C:\Program Files (x86)\presage\share\presage\database_en.db";

            if (File.Exists(Settings.Default.PresageDatabaseLocation))
            {
                Console.WriteLine($"full path: {Path.GetFullPath(Settings.Default.PresageDatabaseLocation)}");
                presageTestInstance.set_config("Presage.Predictors.DefaultSmoothedNgramPredictor.DBFILENAME", Path.GetFullPath(Settings.Default.PresageDatabaseLocation));
            }
            else
            {
                Console.WriteLine("not implemented");
                Exit();
                //try
                //{
                //    var path = DiagnosticInfo.GetAppDataPath(@"Presage");
                //    var database = Path.Combine(path, @"database.db");
                //    if (!File.Exists(database))
                //    {
                //        try
                //        {
                //            using (ZipArchive archive = ZipFile.Open(@".\Resources\Presage\database.zip", ZipArchiveMode.Read, Encoding.UTF8))
                //            {
                //                archive.ExtractToDirectory(path);
                //            }
                //        }
                //        catch (Exception e)
                //        {                            
                //            Console.WriteLine(String.Format("Unpacking the Presage database failed with the following exception: \n{0}", e);
                //        }
                //    }

                //    Settings.Default.PresageDatabaseLocation = database;
                //    presageTestInstance.set_config("Presage.Predictors.DefaultSmoothedNgramPredictor.DBFILENAME", Path.GetFullPath(Settings.Default.PresageDatabaseLocation));
                //}
                //catch (Exception ex)
                //{
                //    Console.WriteLine("Presage failed to bootstrap. The database (database.db file) was not found, and the database.zip file could not be extracted!", ex);
                //    Exit();
                //}
            }

            presageTestInstance.set_config("Presage.Selector.REPEAT_SUGGESTIONS", "yes");
            presageTestInstance.set_config("Presage.Selector.SUGGESTIONS", "50");
            presageTestInstance.save_config();

            Console.WriteLine("Presage settings set successfully.");

            PresageSuggestions p = new PresageSuggestions();
            int NumberOfSuggestionsToCheck = 50;
            string typedSoFar = "";
            while (true )
            {
                //if (typedSoFar.Length > 0)
                {
                    Console.WriteLine("Sentence so far:");
                    Console.WriteLine(typedSoFar);
                }
                
                var suggestions = p.GetSuggestions(typedSoFar, true).Take(NumberOfSuggestionsToCheck);

                Console.Write("Suggestions: ");
                int repeats = 30;
                string all = "";
                foreach (var s in suggestions)
                {
                    Console.WriteLine(s);
                    for (int i = 0; i < repeats; i++)
                    {
                        all += s + " ";                        
                    }
                    if (repeats > 1)
                    {
                        repeats -= 1;
                    }
                    
                    //Console.Write(" | ");
                }
                Clipboard.SetText(all);

                var input = Console.ReadLine();
                typedSoFar += " " + input;
            }                                    
        }
    }
}
