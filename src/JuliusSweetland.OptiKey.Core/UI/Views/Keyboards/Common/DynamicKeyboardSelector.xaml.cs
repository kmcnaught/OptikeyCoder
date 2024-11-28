// Copyright (c) 2022 OPTIKEY LTD (UK company number 11854839) - All Rights Reserved
using JuliusSweetland.OptiKey.UI.Controls;
using JuliusSweetland.OptiKey.Models;
using System.Windows.Controls;
using System;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Reflection;
using System.Windows;
using JuliusSweetland.OptiKey.Enums;
using log4net;
using JuliusSweetland.OptiKey.Properties;

namespace JuliusSweetland.OptiKey.UI.Views.Keyboards.Common
{

    /// <summary>
    /// Interaction logic for DynamicKeyboardSelector.xaml
    /// </summary>
    public partial class DynamicKeyboardSelector : KeyboardView
    {

        #region Private Members
    
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private DynamicKeyboardFolder folder;
        private int pageIndex = 0;

        // TODO: Could be user configurable at some point?
        private int mRows = 3;
        private int mCols = 4;

        #endregion

        public DynamicKeyboardSelector(int pageIndex, string keyboardsPath=null)
        {
            InitializeComponent();
            this.pageIndex = pageIndex;

            // Populate model
            folder = new DynamicKeyboardFolder(keyboardsPath);

            // Setup grid
            for (int i = 0; i < this.mRows; i++)
            {
                MainGrid.RowDefinitions.Add(new RowDefinition());
            }

            for (int i = 0; i < this.mCols; i++)
            {
                MainGrid.ColumnDefinitions.Add(new ColumnDefinition());
            }

            // Add back/quit key, bottom right
            if (keyboardsPath == Settings.Default.DynamicKeyboardsLocation)
            {
                Key newKey = new Key();
                newKey.SharedSizeGroup = "SingleKey";
                newKey.SymbolGeometry = (Geometry)this.Resources["QuitIcon"];
                newKey.Text = JuliusSweetland.OptiKey.Properties.Resources.QUIT;
                newKey.Value = KeyValues.QuitKey;
                newKey.SymbolMargin = 15;
                this.AddKey(newKey, this.mRows - 1, this.mCols - 1);
            }
            else
            {
                Key newKey = new Key();
                newKey.SharedSizeGroup = "SingleKey";
                newKey.SymbolGeometry = (Geometry)this.Resources["BackIcon"];
                newKey.Text = JuliusSweetland.OptiKey.Properties.Resources.BACK;
                newKey.Value = KeyValues.BackFromKeyboardKey;
                newKey.SymbolMargin = 15;
                this.AddKey(newKey, this.mRows - 1, this.mCols - 1);
            }

            // Sleep key, mid right
            {
                Key newKey = new Key();
                newKey.SharedSizeGroup = "SingleKey";
                newKey.SymbolGeometry = (Geometry)this.Resources["SleepIcon"];
                newKey.Text = JuliusSweetland.OptiKey.Properties.Resources.SLEEP;
                newKey.Value = KeyValues.SleepKey;
                newKey.SymbolMargin = 15;
                this.AddKey(newKey, this.mRows - 2, this.mCols - 1);
            }

            // Minimise key for bottom left
            {
                Key newKey = new Key();
                newKey.SymbolGeometry = (Geometry)this.Resources["MinimiseIcon"];
                newKey.SymbolMargin = 15;
                newKey.SharedSizeGroup = "SingleKey";
                newKey.Text = JuliusSweetland.OptiKey.Properties.Resources.MINIMISE;
                newKey.Value = KeyValues.MinimiseKey;
                this.AddKey(newKey, this.mRows - 1, 0);
            }

            // Size/position key, mid left
            {
                Key newKey = new Key();
                newKey.SymbolGeometry = (Geometry)this.Resources["SizeAndPositionIcon"];
                newKey.Text = JuliusSweetland.OptiKey.Properties.Resources.SIZE_AND_POSITION_SPLIT_WITH_NEWLINE;
                newKey.Value = KeyValues.SizeAndPositionKeyboardKey;
                newKey.SharedSizeGroup = "SingleKey";
                newKey.SymbolMargin = 15;
                this.AddKey(newKey, this.mRows - 2, 0);
            }

            // Add keyboard keys, or blanks
            int maxKeyboardsPerPage = (this.mCols - 2) * this.mRows;
            int totalNumKeyboards = folder.keyboards.Count;
            int remainingKeyboards = totalNumKeyboards - maxKeyboardsPerPage * pageIndex;
            int nKBs = Math.Min(remainingKeyboards, maxKeyboardsPerPage);
            int firstKB = maxKeyboardsPerPage * pageIndex;

            // Look for quiz/spelling tests to append
            string quizzesFolderPath = Path.Combine(keyboardsPath, "quizzes");
            string quizFile = null;
            string spellingFile = null;
            if (Directory.Exists(quizzesFolderPath)) {
                var quizFiles = Directory.GetFiles(quizzesFolderPath, "quiz*.json");
                var spellingFiles = Directory.GetFiles(quizzesFolderPath, "spelling*.json");

                quizFile = quizFiles.Count() > 0 ? quizFiles[0] : null;
                spellingFile = spellingFiles.Count() > 0 ? spellingFiles[0] : null;
            }

            if (totalNumKeyboards > 0 || quizFile != null || spellingFile != null)
            {
                for (int i = 0; i < maxKeyboardsPerPage; i++)
                {
                    var r = i / (this.mCols - 2); // integer division
                    var c = 1 + (i % (this.mCols - 2));

                    if (i < nKBs)
                    {
                        KeyboardInfo kbInfo = folder.keyboards.ElementAt(firstKB + i);
                        if (kbInfo.isDirectory)
                        {
                            this.AddDirectoryKey(kbInfo, r, c);
                        }

                        else
                        {
                            // Add key to link to keyboard
                            this.AddKeyboardKey(kbInfo, r, c);
                        }
                    }
                    else if (i == nKBs && quizFile != null) // quiz key if present
                    {
                        this.AddQuizKey(quizFile, r, c);
                    }
                    else if (i == nKBs && quizFile == null && spellingFile != null)
                    {
                        this.AddSpellingKey(spellingFile, r, c);
                    }
                    else if (i == nKBs+1 && quizFile != null && spellingFile != null)
                    { 
                        this.AddSpellingKey(spellingFile, r, c);                                        
                    }
                    else 
                    {
                        // Add empty/inactive key for consistent aesthetic
                        this.AddKey(new Key(), r, c);
                    }
                }
            }
            else
            {
                // Error layout for when there are no keyboards
                {
                    Key newKey = new Key();
                    newKey.SharedSizeGroup = "ErrorText1";
                    newKey.Text = JuliusSweetland.OptiKey.Properties.Resources.NO_KEYBOARDS_FOUND;
                    this.AddKey(newKey, 0, 1, 1, 2);
                }
                {
                    Key newKey = new Key();
                    newKey.SharedSizeGroup = "ErrorText1";
                    newKey.Text = DynamicKeyboard.StringWithValidNewlines(JuliusSweetland.OptiKey.Properties.Resources.KEYBOARD_FOLDER_CAN_BE_CHANGED_SPLIT_LINE);
                    this.AddKey(newKey, 1, 1, 1, 2);
                }
                {
                    Key newKey = new Key();
                    newKey.SharedSizeGroup = "ErrorText2";
                    newKey.Text = JuliusSweetland.OptiKey.Properties.Resources.DYNAMIC_KEYBOARDS_LOCATION_LABEL;
                    newKey.Text += "\n";
                    newKey.Text += Settings.Default.DynamicKeyboardsLocation;
                    this.AddKey(newKey, 2, 1, 1, 2);
                }
            }

            // Add prev/next keys (may be disabled)
            {
                Key newKey = new Key();
                newKey.SharedSizeGroup = "SingleKey";
                newKey.SymbolGeometry = (Geometry)this.Resources["ArrowPointingToLeftIcon"];
                newKey.Text = JuliusSweetland.OptiKey.Properties.Resources.PREV;
                if (this.pageIndex > 0)
                {
                    newKey.Value = KeyValues.DynamicKeyboardPrev;
                }
                this.AddKey(newKey, 0, 0, 1, 1);
            }
            {
                Key newKey = new Key();
                newKey.SharedSizeGroup = "SingleKey";
                newKey.SymbolGeometry = (Geometry)this.Resources["ArrowPointingToRightIcon"];
                newKey.Text = JuliusSweetland.OptiKey.Properties.Resources.NEXT;
                if (nKBs < remainingKeyboards)
                {
                    newKey.Value = KeyValues.DynamicKeyboardNext;
                }
                this.AddKey(newKey, 0, this.mCols - 1, 1, 1);
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

        private void AddQuizKey(string filepath, int row, int col)
        {
            Key newKey = new Key();
            newKey.SharedSizeGroup = "SingleKey";            
            newKey.Text = "quiz";            
            newKey.Value = new KeyValue(FunctionKeys.QuizStart, filepath);
            this.AddKey(newKey, row, col);

        }

        private void AddSpellingKey(string filepath, int row, int col)
        {
            Key newKey = new Key();
            newKey.SharedSizeGroup = "SingleKey";
            newKey.Text = "spelling";
            newKey.Value = new KeyValue(FunctionKeys.SpellingQuizStart, filepath);
            this.AddKey(newKey, row, col);
        }

        private void AddDirectoryKey(KeyboardInfo info, int row, int col)
        {
            Key lKey = new Key();
            lKey.Value = new KeyValue(FunctionKeys.DynamicKeyboard, info.fullPath);
            if (Settings.Default.Theme.Contains("Light"))
                lKey.BackgroundColourOverride = new SolidColorBrush(Color.FromRgb(195, 195, 230));
            else
                lKey.BackgroundColourOverride = new SolidColorBrush(Color.FromRgb(20, 20, 40));
            lKey.Text = String.Format("[{0}]", new DirectoryInfo(info.fullPath).Name);
            lKey.SharedSizeGroup = "DirectoryKey";

            this.AddKey(lKey, row, col);
        }

        private void AddKeyboardKey(KeyboardInfo info, int row, int col)
        {
            Key lKey = new Key();
            lKey.Value = new ChangeKeyboardKeyValue(info.fullPath);
            lKey.SharedSizeGroup = "KeyboardKey";
            lKey.Text = info.keyboardName;
            if (Settings.Default.Theme.Contains("Light"))
                lKey.BackgroundColourOverride = new SolidColorBrush(Color.FromRgb(195, 230, 195));
            else
                lKey.BackgroundColourOverride = new SolidColorBrush(Color.FromRgb(20, 30, 20));
            if (info.symbolString != null)
            {
                Geometry geom = (Geometry)this.Resources[info.symbolString];
                if (null != geom)
                {
                    lKey.SymbolGeometry = geom;
                }
            }
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
