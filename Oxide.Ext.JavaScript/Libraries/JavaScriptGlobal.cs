using Oxide.Core.Libraries;
using Oxide.Core.Logging;

using System.Collections.Generic;
using System.Dynamic;

namespace Oxide.Ext.JavaScript.Libraries
{
    /// <summary>
    /// A global library containing game-agnostic JavaScript utilities
    /// </summary>
    public class JavaScriptGlobal : Library
    {
        /// <summary>
        /// Returns if this library should be loaded into the global namespace
        /// </summary>
        public override bool IsGlobal { get { return true; } }

        /// <summary>
        /// Gets the logger that this library writes to
        /// </summary>
        public Logger Logger { get; private set; }

        /// <summary>
        /// Initializes a new instance of the JavaScriptGlobal library
        /// </summary>
        /// <param name="logger"></param>
        public JavaScriptGlobal(Logger logger)
        {
            Logger = logger;
        }

        /// <summary>
        /// Prints a message
        /// </summary>
        /// <param name="message"></param>
        [LibraryFunction("print")]
        public void Print(object message)
        {
            Logger.Write(LogType.Info, message != null ? message.ToString() : "null");
        }
        
        // convert js object to localization dictionary
        [LibraryFunction("LangDict")]
        public Dictionary<string, Dictionary<string, string>> LangDict(ExpandoObject table)
        {
            var messages = new Dictionary<string, Dictionary<string, string>>();
            
            foreach (KeyValuePair<string, object> kvp in table) {
                var lang = kvp.Key as string;
                if (lang!=null && kvp.Value is ExpandoObject) {
                    messages[lang] = new Dictionary<string, string>();
                    var tbl = (ExpandoObject)kvp.Value;
                    foreach (KeyValuePair<string, object> kvl in tbl) {
                        var msg = kvl.Key as string;
                        if (msg!=null) {
                            var val = kvl.Value as string;
                            if (val!=null) messages[lang][msg] = val;
                        }
                    }
                }
            }
            return messages;
        }
        
        // convert localization dictionary to js object
        [LibraryFunction("GetLangDict")]
        public ExpandoObject GetLangDict(Dictionary<string, Dictionary<string, string>> table)
        {
            var rmessages = new ExpandoObject();
            var messages = rmessages as IDictionary<string, object>;
            foreach (KeyValuePair<string,Dictionary<string, string>> kvp in table)
            {
                var rmsgs = new ExpandoObject();
                var msgs = rmsgs as IDictionary<string, object>;
                foreach (KeyValuePair<string,string> kvl in kvp.Value) {
                    msgs[kvl.Key] = kvl.Value;
                }
                messages[kvp.Key] = rmsgs;
            }
            return rmessages;
        } 
    }
}
