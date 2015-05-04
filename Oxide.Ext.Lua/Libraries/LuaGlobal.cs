using Oxide.Core.Libraries;
using Oxide.Core.Logging;
using System.Collections.Generic;

using NLua;

namespace Oxide.Ext.Lua.Libraries
{
    /// <summary>
    /// A global library containing game-agnostic Lua utilities
    /// </summary>
    public class LuaGlobal : Library
    {
        /// <summary>
        /// Returns if this library should be loaded into the global namespace
        /// </summary>
        public override bool IsGlobal { get { return true; } }

        /// <summary>
        /// Gets the Lua environment
        /// </summary>
        public NLua.Lua LuaEnvironment { get; private set; }
        
        /// <summary>
        /// Gets the logger that this library writes to
        /// </summary>
        public Logger Logger { get; private set; }

        /// <summary>
        /// Initializes a new instance of the LuaGlobal library
        /// </summary>
        /// <param name="logger"></param>
        public LuaGlobal(Logger logger, NLua.Lua lua)
        {
            Logger = logger;
            LuaEnvironment = lua;
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
        
        // convert lua table to localization dictionary
        [LibraryFunction("LangDict")]
        public Dictionary<string, Dictionary<string, string>> LangDict(LuaTable table)
        {
            var messages = new Dictionary<string, Dictionary<string, string>>();
            foreach (object key in table.Keys)
            {
                var lang = key as string;
                if (lang!=null && table[key] is LuaTable) {
                    messages[lang] = new Dictionary<string, string>();
                    var tbl = (LuaTable)table[key];
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
        
        // convert localization dictionary to lua table 
        [LibraryFunction("GetLangDict")]
        public LuaTable GetLangDict(Dictionary<string, Dictionary<string, string>> table)
        {
            LuaEnvironment.NewTable("TempLangTable");
            var messages = LuaEnvironment.GetTable("TempLangTable");
            LuaEnvironment["TempLangTable"] = null;
            foreach (KeyValuePair<string,Dictionary<string, string>> kvp in table)
            {
                LuaEnvironment.NewTable("TempLangTable");
                messages[kvp.Key] = LuaEnvironment.GetTable("TempLangTable");
                LuaEnvironment["TempLangTable"] = null;
                foreach (KeyValuePair<string,string> kvl in kvp.Value) {
                    ((LuaTable)messages[kvp.Key])[kvl.Key] = kvl.Value;
                }
            }
            return messages;
        }
    }
}
