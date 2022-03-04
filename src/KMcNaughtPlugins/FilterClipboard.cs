using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;

/**
 * A selection of plugins to filter clipboard text.
 * 
 * For example, extract code comments from source code, or 
 * strip out comments, or apply a regex
 */

namespace JuliusSweetland.OptiKey.Plugins
{
    public class FilterClipboard
    {
        // Extract the context of all comments from Java/C++/C#/etc:
        // - multiline block comments  /** .. .. .. */
        // - single line block comments /* .. */
        // - line comments // ...
        public void ExtractCommentsOnly()  
        {
            string textInput = Clipboard.GetText();

            string pattern = @"\/\*\*?(.*?)\*\/|\/\/([^\n]*$)";
            RegexOptions options = RegexOptions.Multiline | RegexOptions.Singleline;
            string textOutput = "";

            foreach (Match m in Regex.Matches(textInput, pattern, options))
            {
                for (int i = 1; i < m.Groups.Count; i++) // skip 0 which is whole expression
                {
                    String s = m.Groups[i].Value.Trim();
                    if (s.Contains("\n")) // special case -  remove possible leading * from multiline block comments
                    {
                        using (StringReader sr = new StringReader(s))
                        {
                            string line;
                            while ((line = sr.ReadLine()) != null)
                            {
                                line = line.Trim();
                                if (line.StartsWith("*"))
                                {
                                    line = line.TrimStart('*').Trim();
                                }
                                textOutput += line + "\n";
                            }
                        }
                    }
                    else if (!String.IsNullOrEmpty(s))
                    {
                        textOutput += s + "\n";
                    }
                }
            }

            Clipboard.SetText(textOutput);
        }

        // Remove all comments from Java/C++/C#/etc:
        // - multiline block comments  /** .. .. .. */
        // - single line block comments /* .. */
        // - line comments // ...
        public void RemoveComments()
        {
            string textInput = Clipboard.GetText();

            string pattern = @"\/\*\*?(.*?)\*\/|\/\/([^\n]*$)";
            RegexOptions options = RegexOptions.Multiline | RegexOptions.Singleline ;

            Regex regex = new Regex(pattern, options);
            string textOutput = regex.Replace(textInput, "");

            Clipboard.SetText(textOutput);
        }

        
        public void RemoveEmptyLines()
        {
            string textInput = Clipboard.GetText();
            string textOutput = "";

            using (StringReader sr = new StringReader(textInput))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    var trimmedLine = line.Trim();
                    if (!String.IsNullOrEmpty(trimmedLine))
                    {
                        if (!String.IsNullOrEmpty(textOutput))
                        { 
                            textOutput += "\n";
                        }
                        textOutput += line;
                    }
                }
            }

            Clipboard.SetText(textOutput);
        }

        public void RemoveIndentation()
        {
            string textInput = Clipboard.GetText();
            string textOutput = "";

            using (StringReader sr = new StringReader(textInput))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    var trimmedLine = line.Trim();
                    if (!String.IsNullOrEmpty(textOutput))
                    {
                        textOutput += "\n";
                    }
                    textOutput += trimmedLine;                    
                }
            }

            Clipboard.SetText(textOutput);
        }

        public void RemovePrintln()
        {
            string textInput = Clipboard.GetText();

            string pattern = @"^\s*System\.out\.print.*$";
            RegexOptions options = RegexOptions.Multiline;

            Regex regex = new Regex(pattern, options);
            string textOutput = regex.Replace(textInput, "");

            Clipboard.SetText(textOutput);
        }

        public void RemoveCurlyBraces()
        {
            string textInput = Clipboard.GetText();

            string pattern = @"^\s*{.*$|^\s*}.*$";
            RegexOptions options = RegexOptions.Multiline;

            Regex regex = new Regex(pattern, options);
            string textOutput = regex.Replace(textInput, "");

            Clipboard.SetText(textOutput);
        }

    }
}
