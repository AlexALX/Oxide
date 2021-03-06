﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Oxide.Core
{
    /// <summary>
    /// Represents a command line string in managed form
    /// </summary>
    public sealed class CommandLine
    {
        // The flags and variables of this command line
        private Dictionary<string, string> variables = new Dictionary<string, string>();

        /// <summary>
        /// Initializes a new instance of the CommandLine class
        /// </summary>
        /// <param name="cmdline"></param>
        public CommandLine(string[] commandline)
        {
            string cmdline = string.Empty;
            string key = string.Empty;

            foreach (string str in commandline)
                cmdline += "\"" + str.Trim(new[] { '/', '\\' }) + "\"";

            foreach (string str in Split(cmdline))
            {
                if (str.Length > 0)
                {
                    var val = str;
                    if (str[0] == '-' || str[0] == '+')
                    {
                        if (key != string.Empty && variables.ContainsKey(key))
                            variables.Add(key, string.Empty);
                        key = val.Substring(1);
                    }
                    else if (key != string.Empty)
                    {
                        if (!variables.ContainsKey(key))
                        {
                            if (key.Contains("dir"))
                                val = val.Replace('/', '\\');
                            variables.Add(key, val);
                        }
                        key = string.Empty;
                    }
                }
            }

            if (key != string.Empty && !variables.ContainsKey(key))
                variables.Add(key, string.Empty);
        }

        /// <summary>
        /// Split the commandline arguments
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public string[] Split(string input)
        {
            input = input.Replace("\\\"", "&qute;");
            MatchCollection matchs = new Regex("\"([^\"]+)\"|'([^']+)'|\\S+").Matches(input);
            string[] strArray = new string[matchs.Count];
            for (int i = 0; i < matchs.Count; i++)
            {
                char[] trimChars = new char[] { ' ', '"' };
                strArray[i] = matchs[i].Groups[0].Value.Trim(trimChars);
                strArray[i] = strArray[i].Replace("&qute;", "\"");
            }

            return strArray;
        }

        /// <summary>
        /// Returns if this command line has the specified variable (prefixed with +)
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public bool HasVariable(string name)
        {
            // Search
            return variables.Any((v) => v.Key == name);
        }

        /// <summary>
        /// Gets the value for the specified variable
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public string GetVariable(string name)
        {
            try
            {
                return variables.Single((v) => v.Key == name).Value;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
