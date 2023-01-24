// Copyright (c) 2022 OPTIKEY LTD (UK company number 11854839) - All Rights Reserved
using JuliusSweetland.OptiKey.UI.Controls;
using JuliusSweetland.OptiKey.Models;
using System.Windows.Controls;
using System;
using System.Linq;
using System.Windows.Media;
using System.Reflection;

using log4net;
using JuliusSweetland.OptiKey.Properties;
using System.IO;
using JuliusSweetland.OptiKey.Services;
using JuliusSweetland.OptiKey.Extensions;

namespace JuliusSweetland.OptiKey.UI.Views.Keyboards.Common
{

    /// <summary>
    /// Interaction logic for DictionarySelector.xaml
    /// </summary>
    public partial class DictionarySelector : KeyboardView
    {

        #region Private Members
    
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private DynamicKeyboardFolder folder;
        private int pageIndex = 0;

        // TODO: Could be user configurable at some point?
        private int mRows = 3;
        private int mCols = 4;

        #endregion

        public DictionarySelector(int pageIndex)
        {
            InitializeComponent();
            this.pageIndex = pageIndex;

            // Find all possible dic files
            var filePath = Settings.Default.CustomDictionariesLocation;

            if (Directory.Exists(filePath))
            {
                string[] fileArray = Directory.GetFiles(filePath, "*.txt");
                fileArray = new string[0];

                Log.InfoFormat("Found {0} dictionary files", fileArray.Length);

                // Setup grid
                for (int i = 0; i < this.mRows; i++)
                {
                    MainGrid.RowDefinitions.Add(new RowDefinition());
                }

                for (int i = 0; i < this.mCols; i++)
                {
                    MainGrid.ColumnDefinitions.Add(new ColumnDefinition());
                }

                // Add back key, bottom right
                {
                    Key newKey = new Key();
                    newKey.SharedSizeGroup = "SingleKey";
                    newKey.SymbolGeometry = (Geometry)this.Resources["BackIcon"];
                    newKey.Text = JuliusSweetland.OptiKey.Properties.Resources.BACK;
                    newKey.Value = KeyValues.BackFromKeyboardKey;
                    this.AddKey(newKey, this.mRows - 1, this.mCols - 1);
                }

                // "Clear dictionary" bottom left
                {
                    Key newKey = new Key();                    
                    newKey.Text = "Clear all";
                    newKey.Value = new KeyValue(Enums.FunctionKeys.ClearDictionary);
                    this.AddKey(newKey, this.mRows - 1, 0);
                }

                // Load English
                {
                    Key newKey = new Key();
                    newKey.Text = "Reset Dictionary";
                    newKey.Value = new KeyValue(Enums.FunctionKeys.EnglishUK_NoUI);
                    this.AddKey(newKey, this.mRows - 1, 1);                    
                }

                // Load file
                {
                    Key newKey = new Key();
                    newKey.Text = "Pick file";
                    newKey.Value = new KeyValue(Enums.FunctionKeys.AddWordsFromFile);
                    this.AddKey(newKey, this.mRows - 1, 2);
                }


                // Add keyboard keys, or blanks
                int maxDicFilesPerPage = (this.mCols) * (this.mRows - 1);
                int totalNumDicFiles = fileArray.Length;
                int remainingDicFiles = totalNumDicFiles - maxDicFilesPerPage * pageIndex;
                int nKBs = Math.Min(remainingDicFiles, maxDicFilesPerPage);
                int firstKB = maxDicFilesPerPage * pageIndex;

                if (totalNumDicFiles == 0)
                {
                    Key newKey = new Key();
                    newKey.SharedSizeGroup = "ErrorText1";
                    newKey.Text = "No files\nfound";
                    this.AddKey(newKey, 0, 0);
                }
                
                for (int i = 0; i < maxDicFilesPerPage; i++)
                {
                    var r = i / (this.mCols); // integer division
                    var c = (i % (this.mCols));

                    if (i < nKBs)
                    {
                        this.AddDicKey(fileArray[i], r, c);
                    }
                    else
                    {
                        if (i > 0) // for 0 files we used this key for an error dialog
                        {
                            // Add empty/inactive key for consistent aesthetic
                            this.AddKey(new Key(), r, c);
                        }
                    }
                }
                
            }            
            else
            {
                // Setup grid
                for (int i = 0; i < this.mRows; i++)
                {
                    MainGrid.RowDefinitions.Add(new RowDefinition());
                }

                for (int i = 0; i < this.mCols; i++)
                {
                    MainGrid.ColumnDefinitions.Add(new ColumnDefinition());
                }

                // Error layout for when there are no keyboards
                {
                    Key newKey = new Key();
                    newKey.SharedSizeGroup = "ErrorText1";
                    newKey.Text = "No dictionaries";
                    this.AddKey(newKey, 0, 1, 1, 2);
                }
                {
                    Key newKey = new Key();
                    newKey.SharedSizeGroup = "ErrorText2";
                    newKey.Text = DynamicKeyboard.StringWithValidNewlines("Couldn't find any dictionaries in "+ filePath);
                    this.AddKey(newKey, 1, 0, 1, 4);
                }
                {
                    Key newKey = new Key();
                    newKey.SharedSizeGroup = "ErrorText3";
                    newKey.Text = "Change path in Management Console";
                    this.AddKey(newKey, 2, 1, 1, 2);
                }
            }            
        }

        private void AddKey(Key key, int row, int col, int rowspan = 1, int colspan = 1)
        {
            MainGrid.Children.Add(key);
            Grid.SetColumn(key, col);
            Grid.SetRow(key, row);
            Grid.SetColumnSpan(key, colspan);
            Grid.SetRowSpan(key, rowspan);

        }
        

        private void AddDicKey(string filename, int row, int col)
        {
            String filename_simple = Path.GetFileNameWithoutExtension(filename);
            filename_simple = filename_simple.Split('.')[0];
            filename_simple = filename_simple.SplitToTwoLines();

            Key lKey = new Key();            
            lKey.SharedSizeGroup = "KeyboardKey"; 
            lKey.Value = new KeyValue(Enums.FunctionKeys.AddWordsFromFile, filename);
            lKey.Text = filename_simple;            
            this.AddKey(lKey, row, col);
        }

        private int RowFromIndex(int index)
        {
            return index / this.mCols; // integer division
        }

        private int ColFromIndex(int index)
        {
            return index % this.mCols;
        }

    }
}
