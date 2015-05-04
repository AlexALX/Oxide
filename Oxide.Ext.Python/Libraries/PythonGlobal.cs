using Oxide.Core.Libraries;
using Oxide.Core.Logging;
using System.Collections.Generic;

using IronPython.Runtime;

namespace Oxide.Ext.Python.Libraries
{
    /// <summary>
    /// A global library containing Python utilities
    /// </summary>
    public class PythonGlobal : Library
    {
        /// <summary>
        /// Returns if this library should be loaded into the global namespace
        /// </summary>
        public override bool IsGlobal { get { return true; } }

        // convert python dictionary to localization dictionary
        [LibraryFunction("LangDict")]
        public Dictionary<string, Dictionary<string, string>> LangDict(PythonDictionary table)
        {
            var messages = new Dictionary<string, Dictionary<string, string>>();
            
            foreach (object key in table.Keys)
            {
                var lang = key as string;
                if (lang!=null && table[key] is PythonDictionary) {
                    messages[lang] = new Dictionary<string, string>();
                    var tbl = (PythonDictionary)table[key];
                    foreach (object key2 in tbl.Keys) {
                        var msg = key2 as string;
                        if (msg!=null) {
                            var val = tbl[key2] as string;
                            if (val!=null) messages[lang][msg] = val;
                        }
                    }
                }
            }
            return messages;
        }
        
        // convert localization dictionary to python dictionary 
        [LibraryFunction("GetLangDict")]
        public PythonDictionary GetLangDict(Dictionary<string, Dictionary<string, string>> table)
        {
            var messages = new PythonDictionary();
            foreach (KeyValuePair<string,Dictionary<string, string>> kvp in table)
            {
                messages[kvp.Key] = new PythonDictionary();
                foreach (KeyValuePair<string,string> kvl in kvp.Value) {
                    ((PythonDictionary)messages[kvp.Key])[kvl.Key] = kvl.Value;
                }
            }
            return messages;
        }        
    }
}
