﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Runtime.InteropServices;

namespace CompilePalX.Compiling
{
    internal delegate Run? LogWrite(string s, Brush b);
    internal delegate Run? LogWriteURL(string s, string url);
    internal delegate void LogBacktrack(List<Run> l);
    internal delegate void CompileErrorLogWrite(string errorText, Error e);

    internal delegate void CompileErrorFound(Error e);


    static class CompilePalLogger
    {
        private static readonly string logFile = "./debug.log";
        static CompilePalLogger()
        {
            File.Delete(logFile);

            // print debug information
            LogLine($"--- Compile Pal {UpdateManager.CurrentVersion} ---");
            LogLine($"Runtime: {RuntimeInformation.RuntimeIdentifier}");
            LogLine($"Locale: {CultureInfo.CurrentCulture.Name}");
        }
        public static event LogWrite OnWrite;
        public static event LogWriteURL OnWriteURL;
        public static event LogBacktrack OnBacktrack;
        public static event CompileErrorLogWrite OnErrorLog;

        public static event CompileErrorFound OnErrorFound;


        public static Run LogColor(string s, Brush b, params object[] formatStrings)
        {
            string text = s;
            if (formatStrings.Length != 0)
            {
                text = string.Format(s, formatStrings);
            }

            try
            {
                File.AppendAllText(logFile, text);

            }
            catch { }

            return OnWrite?.Invoke(text, b);
        }


        public static Run LogLineColor(string s, Brush b, params object[] formatStrings)
        {
            return LogColor(s + Environment.NewLine, b, formatStrings);
        }

        public static Run? Log(string s = "", params object[] formatStrings)
        {

            // listen for variable updates for plugins
            if (s.StartsWith("COMPILE_PAL_SET"))
            {
                GameConfigurationManager.ModifyCurrentContext(s);
                return null;

            }

            return LogColor(s, null, formatStrings);
        }

        public static Run? LogLine(string s = "", params object[] formatStrings)
        {
            return Log(s + Environment.NewLine, formatStrings);
        }

        public static Run? LogLineFileLocation(string s, string url)
        {
            return OnWriteURL.Invoke(s + Environment.NewLine, url);
        }

        public static void LogDebug(string s)
        {
            // log in debug, no op in release
#if DEBUG
            try
            {
                File.AppendAllText(logFile, s);
            } catch { }
#endif
        }

        public static void LogLineDebug(string s)
        {
            Trace.WriteLine(s);
            LogDebug(s + Environment.NewLine);
        }


        public static void LogCompileError(string errorText, Error e)
        {
            if (errorsFound.ContainsKey(e))
                errorsFound[e]++;
            else
                errorsFound.Add(e, 1);

            if (errorsFound[e] < 128)
                OnErrorLog(errorText, e);
            else
                Log(errorText); //Stop hyperlinking errors if we see over 128 of them
            
            File.AppendAllText(logFile, errorText);
            OnErrorFound(e);
        }
        public static void LogLineCompileError(string errorText, Error e)
        {
            LogCompileError(errorText + Environment.NewLine, e);
        }

        private static Dictionary<Error, int> errorsFound = [];

        private static StringBuilder lineBuffer = new ();
        private static List<Run> tempText = [];
        public static void LogProgressive(string s)
        {
            lineBuffer.Append(s);

            if (!s.Contains("\n"))
            {
                Run? log = Log(s);
                if (log != null)
                    tempText.Add(log);
            }

            // Log has completed at least 1 line, process it further
            List<string> lines = lineBuffer.ToString().Split("\r\n").ToList();

            string suffixText = lines.Last();

            lineBuffer = new StringBuilder(suffixText);

            OnBacktrack(tempText);

            for (int i = 0; i < lines.Count - 1; i++)
            {
                string line = lines[i];
                Error? error = ErrorFinder.GetError(line);

                if (error == null)
                    LogLine(line);
                else
                    LogLineCompileError(line, error);
            }

            if (suffixText.Length > 0)
            {
                Run? log = Log(suffixText);
                if (log != null)
                    tempText = [log];
            }
        }
    }
}
