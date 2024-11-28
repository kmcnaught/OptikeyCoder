// Copyright (c) 2022 OPTIKEY LTD (UK company number 11854839) - All Rights Reserved
using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml.Serialization;
using JuliusSweetland.OptiKey.Extensions;
using JuliusSweetland.OptiKey.Properties;
using JuliusSweetland.OptiKey.UI.Views.Keyboards.Common;
using System.Linq;

namespace JuliusSweetland.OptiKey.Models
{
    struct KeyboardInfo
    {
        public string fullPath;
        public string keyboardName;
        public string symbolString;
        public bool isHidden; // default false
        public bool isDirectory; // default false

        public override string ToString()
        {
            string str = fullPath +  ", ";
            str += keyboardName + ", ";
            str += symbolString + ", ";
            str += isHidden + ", ";
            return str;
        }
    }

    class DynamicKeyboardFolder
    {
        #region Private Members

        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        #endregion

        public List<KeyboardInfo> keyboards;
        
        public List<KeyboardInfo> VisibleKeyboards
        {
            get
            {
                return keyboards.FindAll(keyb => !keyb.isHidden);
            }
        }

        public DynamicKeyboardFolder(String filePath)
        {
            if (String.IsNullOrEmpty(filePath))
            {
                filePath = Settings.Default.DynamicKeyboardsLocation;
            }

            keyboards = new List<KeyboardInfo>();

            // Find all possible xml files
            if (Directory.Exists(filePath))
            {
                string[] fileArray = Directory.GetFiles(filePath, "*.xml");

                Log.InfoFormat("Found {0} keyboard files", fileArray.Length);

                // Read in keyboard name, symbol, hidden state from each file
                // Note that ordering is currently undefined
                foreach (string fullName in fileArray)
                {
                    KeyboardInfo info = GetKeyboardInfo(fullName);
                    if (null != info.fullPath)
                    {
                        if (!info.isHidden)
                        {
                            keyboards.Add(info);
                            Log.InfoFormat("Found keyboard file: {0}", info.fullPath);
                        }
                        else
                        {
                            Log.InfoFormat("Ignoring keyboard file: {0}", info.fullPath);
                        }
                    }
                }

                // Look for any subdirs 
                string[] folderArray = Directory.GetDirectories(filePath);
                foreach (string fullPath in folderArray)
                {
                    string folderName = new DirectoryInfo(fullPath).Name;
                    if (!folderName.StartsWith(".") && Directory.GetFiles(fullPath, "*.xml", SearchOption.AllDirectories).Any())
                    {
                        KeyboardInfo info = GetKeyboardInfo(fullPath);
                        if (null != info.fullPath)
                        {
                            keyboards.Add(info);
                            Log.InfoFormat("Found keyboard folder with XML files: {0}", info.fullPath);
                        }
                    }
                }
            }
        }

        #region Private Methods

        // get name from XML if present
        private KeyboardInfo GetKeyboardInfo(string keyboardPath)
        {
            KeyboardInfo info = new KeyboardInfo();
            info.fullPath = keyboardPath;

            if (Directory.Exists(keyboardPath) && !keyboardPath.StartsWith("."))
            {
                info.isDirectory = true;
                return info;
            }
            
            try
            {
                XmlKeyboard spec = XmlKeyboard.ReadFromFile(keyboardPath);
                
                info.keyboardName = DynamicKeyboard.StringWithValidNewlines(spec.Name);
                info.symbolString = spec.Symbol;
                info.isHidden = spec.Hidden;

                // default to filename if no name given
                if (info.keyboardName.Length == 0)
                {
                    info.keyboardName = Path.GetFileName(keyboardPath);
                }      
            }
            catch (Exception e)
            {
                Log.Error(e.ToString());

                // replace info with default (based on filename only - didn't manage to read contents)
                // We keep these available so you can load a "broken" file and get an error message
                info = new KeyboardInfo();
                info.fullPath = keyboardPath;
                info.keyboardName = Path.GetFileName(keyboardPath);
                
            }

            return info;
        }

        #endregion

    }
}
