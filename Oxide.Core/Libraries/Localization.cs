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
        // first language is always english, but you can set you own
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
        
        // load language messages from lang folder for some plugin
        private void LoadFromDatafile(Plugin plugin)
        {
            Dictionary<string, Dictionary<string, string>> msgs = null;
            try
            {
                // plugin.Name of chsparn plugin is always baseplugin if call inside constuctor class,
                // may need to prevent use this function if plugin not loaded yet
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
                    MainLang=defaultlang
                };
            }
        }

        // Save plugin messages to lang file
        [LibraryFunction("SaveMessages")]
        public bool SaveMessages(Plugin plugin)
        {
            if (!messages.ContainsKey(plugin)) return false;
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
        // also return them back if developer want use it by its own way
        [LibraryFunction("RegisterMessages")]
        public Dictionary<string, Dictionary<string, string>> RegisterMessages(Dictionary<string, Dictionary<string, string>> msgs, Plugin plugin, string deflang = null)
        {
            LoadFromDatafile(plugin);
            if (messages.ContainsKey(plugin))
            {
                var newmessages = msgs;
                var mainlang = messages[plugin].MainLang;
                if (deflang!=null) mainlang = deflang;
                if (!newmessages.ContainsKey(mainlang)) {
                    //messages[plugin].MainLang = msgs.Keys.First();
                    // hm, now i'll return error.
                    Interface.Oxide.LogError("Unable to load plugin language data - missing main '"+mainlang+"' language!");
                    return null;
                }
                
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
        public void Register(Plugin plugin, string deflang = null)
        {
            LoadFromDatafile(plugin);
            // maybe with manual register way default language useless
            if (deflang!=null) SetMainLanguage(deflang, plugin);

            // clean messages on unload
            plugin.OnRemovedFromManager += owner_OnRemovedFromManager;
        }
        
        [LibraryFunction("SetMainLanguage")]
        public bool SetMainLanguage(string lang, Plugin plugin)
        {
            if (!messages.ContainsKey(plugin)) return false;
            //if (!messages[plugin].Msgs.ContainsKey(lang)) return false;
            messages[plugin].MainLang = lang;
            return true;
        }
        
        [LibraryFunction("GetMainLanguage")]
        public string GetMainLanguage(Plugin plugin)
        {
            if (!messages.ContainsKey(plugin)) return defaultlang;
            return messages[plugin].MainLang;
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
            // return message in english language if doesn't exists in player language
            if (messages[plugin].Msgs.ContainsKey(defaultlang) && messages[plugin].Msgs[defaultlang].ContainsKey(message)) return messages[plugin].Msgs[defaultlang][message];
            // return message in plugin main language if doesn't exists in english
            return messages[plugin].Msgs?[messages[plugin].MainLang]?[message] ?? message;
            // also itself if message doesn't exists at all
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
        // optional parameter plugin is needed to get language only what exists in some plugin
        // if it not set - it will return actual player language
        [LibraryFunction("GetLanguage")]
        public string GetLanguage(string userid, Plugin plugin = null)
        {
            var userlang = defaultlang;
            if (userlangs.ContainsKey(userid)) userlang = userlangs[userid];
            // if plugin is set then check for language
            if (plugin!=null && messages.ContainsKey(plugin)) {
                // if plugin don't have player language - return english or main language.
                if (!messages[plugin].Msgs.ContainsKey(userlang)) {
                    // if plugin contains english language return it
                    if (messages[plugin].Msgs.ContainsKey(defaultlang)) return defaultlang;
                    // if not return main language (if its not english)
                    return messages[plugin].MainLang;
                    // we don't need check for main language 
                    // because translation wont load if main language missing.
                }
            }
            return userlang;
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
