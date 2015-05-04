/*
	Localization API for Oxide 2
	Created By AlexALX (c) 2015
	----------------------------
	This is my first C# library so may not be optimized.
	Sorry for my english.
*/
using System;
using System.Collections.Generic;
using System.Linq;

using Oxide.Core.Logging;
using Oxide.Core.Plugins;

namespace Oxide.Core.Libraries
{

    // language messages class
    public class PluginMessages
    {
        // this is main language from where all messages should be updated
        // first language in file is always main
        // not sure if it works correct, maybe we should do so englisg always main and required?
        // also in lua tables have no sort so it useless in this case... need to thing
        public string MainLang { get; set; }
        // messages dictionary, not sure how to do this better
        public Dictionary<string, Dictionary<string, string>> Msgs { get; set; }
    }

    public class Localization : Library
    {
        public override bool IsGlobal { get { return false; } }

        // contains all plugin messages
        private Dictionary<Plugin, PluginMessages> messages;
        // contains all user languages
        private Dictionary<string, string> userlangs;
        
        // constants
        private const string langfile = "oxide.userlangs";
        private const string defaultlang = "en";
        
        public bool IsLoaded { get; private set; }

        public Localization()
        {
            messages = new Dictionary<Plugin, PluginMessages>();
            userlangs = new Dictionary<string, string>();
            LoadData();
        }

        // load user languages from langfile
        private void LoadData()
        {
            try
            {
                userlangs = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string,string>>(langfile);
                IsLoaded = true;
            }
            catch (Exception ex)
            {
                IsLoaded = false;
                LastException = ex;
                Interface.Oxide.LogError("Unable to load players language data! Localizations disabled.\r\n => " + ex.Message);
            }
        }
        
        // load language messages from l10n folder for some plugin
        private void LoadFromDatafile(Plugin plugin)
        {
            Dictionary<string, Dictionary<string, string>> msgs = null;
            try
            {
                // POSSIBLE BUG: plugin.Name for chsparn plugin is always baseplugin
                // not sure whats wrong
                msgs = Interface.Oxide.LangFileSystem.ReadObject<Dictionary<string, Dictionary<string, string>>>(plugin.Name);
            }
            catch (Exception ex)
            {
                //messages.Remove(plugin);
            }
            finally
            {
                if (msgs==null) msgs = new Dictionary<string, Dictionary<string, string>>();
                // init plugin data
                messages[plugin] = new PluginMessages(){
                    Msgs=msgs,
                    MainLang=(msgs.Count>0?msgs.Keys.First():defaultlang)
                };
            }
        }

        // Save plugin messages to lang file
        [LibraryFunction("SaveMessages")]
        public bool SaveMessages(Plugin plugin)
        {
            if (!messages.ContainsKey(plugin)) return false;
            // BUG: plugin.Name for chsparn plugin is always baseplugin
            Interface.GetMod().LangFileSystem.WriteObject(plugin.Name, messages[plugin].Msgs);
            return true;
        }
        
        // Save user languages
        private void SaveLangs()
        {
            Interface.GetMod().DataFileSystem.WriteObject(langfile, userlangs);
        }

        #region Library Staff

        // register all plugin messages with automatically add/remove new/old messages for all translations.
        // also return them back if developer wantuse it by its own way
        [LibraryFunction("RegisterMessages")]
        public Dictionary<string, Dictionary<string, string>> RegisterMessages(Dictionary<string, Dictionary<string, string>> msgs, Plugin plugin)
        {
            LoadFromDatafile(plugin);
            if (messages.ContainsKey(plugin))
            {
                var newmessages = msgs;
                // in case if new messages don't have previous default lang (first one in file)
                if (!newmessages.ContainsKey(messages[plugin].MainLang)) {
                    messages[plugin].MainLang = msgs.Keys.First();
                }
                var mainlang = messages[plugin].MainLang;
                
                // add/remove new/old messages in all translations
                foreach(KeyValuePair<string,Dictionary<string, string>> kvp in messages[plugin].Msgs) {
                    if (!newmessages.ContainsKey(kvp.Key)) newmessages[kvp.Key] = new Dictionary<string, string>();
                    // add only exists messages in main language (so it cleanup translation from old messages)
                    foreach(KeyValuePair<string,string> kvl in kvp.Value) {
                        if (newmessages[mainlang].ContainsKey(kvl.Key)) {
                            newmessages[kvp.Key][kvl.Key] = kvl.Value;
                        }
                    }
                    // add new messages from main language
                    foreach(KeyValuePair<string,string> kvl in newmessages[mainlang]) {
                        if (!newmessages[kvp.Key].ContainsKey(kvl.Key)) {
                            newmessages[kvp.Key][kvl.Key] = kvl.Value;
                        }
                    }
                }
                
                messages[plugin].Msgs = newmessages;
            } else {
                messages[plugin].Msgs = msgs;
            }
            // save messages to lang file
            SaveMessages(plugin);
            
            // clean messages on unload
            plugin.OnRemovedFromManager += owner_OnRemovedFromManager;
            
            return messages[plugin].Msgs;
        }
        
        // Manual register if needed, no automatic at all
        [LibraryFunction("Register")]
        public void Register(Plugin plugin)
        {
            LoadFromDatafile(plugin);
        }

        #endregion

        // clean messages on plugin unload
        private void owner_OnRemovedFromManager(Plugin sender, PluginManager manager)
        {
            messages.Remove(sender);
        }

        #region Messages Stuff
        
        // Return all plugin messages
        [LibraryFunction("GetMessages")]
        public Dictionary<string, Dictionary<string, string>> GetMessages(Plugin plugin)
        {
            if (!messages.ContainsKey(plugin)) return new Dictionary<string, Dictionary<string, string>>();
            return messages[plugin].Msgs;
        }
        
        // Check if messages exists in some language
        [LibraryFunction("MessageExists")]
        public bool MessageExists(string lang, string message, Plugin plugin)
        {
            if (!messages.ContainsKey(plugin)) return false;
            // not sure if "?" method is better in performance that ContainsKey in c# 6
            return messages[plugin].Msgs?[lang].ContainsKey(message) ?? false;
        }

        // Get message in user language by message key name
        // Too much checks in this simple functions... not very optimized...
        // And because this function in fact can be called everywhere and multiple times
        // it should be rewritten to get maximum performance.
        [LibraryFunction("GetMessage")]
        public string GetMessage(string userid, string message, Plugin plugin)
        {
            //if (!MessageExists(message,plugin)) return message;
            var lang = GetLanguage(userid);
            // return message if in exists in player language
            if (messages[plugin].Msgs.ContainsKey(lang) && messages[plugin].Msgs[lang].ContainsKey(message)) return messages[plugin].Msgs[lang][message];
            // return message in main language if doesn't exists in player language
            // also return message itself if message doesn't exists at all
            return messages[plugin].Msgs?[messages[plugin].MainLang]?[message] ?? message;
        }
        
        // Manualy register message, only do this if message already not registered (prevent override if already exists in file)
        [LibraryFunction("RegisterMessage")]
        public bool RegisterMessage(string lang, string message, string value, Plugin plugin)
        {
            if (!messages.ContainsKey(plugin)) return false;
            // If language don't exists - create new one
            if (!messages[plugin].Msgs.ContainsKey(lang)) messages[plugin].Msgs[lang] = new Dictionary<string, string>();
            // abort if message already registered
            if (messages[plugin].Msgs[lang].ContainsKey(message)) return false;
            messages[plugin].Msgs[lang][message] = value;
            return true;
        }
        
        // Override some message in ALL translations, needed for example if phases get updated and you need reset it in all translations
        [LibraryFunction("OverrideMessage")]
        public bool OverrideMessage(string message, string value, Plugin plugin)
        {
            if (!messages.ContainsKey(plugin)) return false;
            foreach(KeyValuePair<string,Dictionary<string, string>> kvp in messages[plugin].Msgs) {
                messages[plugin].Msgs[kvp.Key][message] = value;
            }
            return true;
        }

        #endregion

        #region User Stuff

        // Return client language (using server data)
        [LibraryFunction("GetLanguage")]
        public string GetLanguage(string userid)
        {
            if (userlangs.ContainsKey(userid)) return userlangs[userid];
            return defaultlang;
        }

        // Set client language, for now just short codes like "en", "de", "ru" etc.
        [LibraryFunction("SetLanguage")]
        public void SetLanguage(string userid, string lang)
        {
            // store only if player have non-english language to save memory.
            if (lang==defaultlang) {
                if (userlangs.ContainsKey(userid)) userlangs.Remove(userid); 
            } else {
                userlangs[userid] = lang;
            }
            // maybe this should be used only in OnServerSave
            SaveLangs();
        }
        
        // Get all registered languages by plugins
        // Probably not optimized, maybe add some caching?
        [LibraryFunction("GetLanguages")]
        public List<string> GetLanguages()
        {
            var languages = new List<string>();
            foreach(PluginMessages msgs in messages.Values) {
                foreach(string lang in msgs.Msgs.Keys) {
                    if (!languages.Contains(lang)) languages.Add(lang);
                }
            }
            return languages;
        }

        #endregion
    }
}
