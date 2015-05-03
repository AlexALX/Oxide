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

    public class PluginMessages
    {
        public string MainLang { get; set; }
        public Dictionary<string, Dictionary<string, string>> Msgs { get; set; }
    }

    public class Localization : Library
    {
        public override bool IsGlobal { get { return false; } }

        private Dictionary<Plugin, PluginMessages> messages;
		private Dictionary<string, string> userlangs;
		
		private const string langfile = "oxide.userlangs";
		private const string defaultlang = "en";
		private List<string> languages;

        public bool IsLoaded { get; private set; }

        public Localization()
        {
            messages = new Dictionary<Plugin, PluginMessages>();
			userlangs = new Dictionary<string, string>();
			languages = new List<string>();
			LoadData();
        }

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
		
        private void LoadFromDatafile(Plugin plugin)
        {
			var file = plugin.Name;
            try
            {
				var msgs = Interface.Oxide.LangFileSystem.ReadObject<Dictionary<string, Dictionary<string, string>>>(file);
                messages[plugin] = new PluginMessages(){
					Msgs=msgs,
					MainLang=msgs.Keys.First()
				};
            }
            catch (Exception ex)
            {
				//messages.Remove(plugin);
				messages[plugin] = new PluginMessages(){
					Msgs = new Dictionary<string, Dictionary<string, string>>(),
					MainLang=defaultlang
				};
            }
        }

		[LibraryFunction("SaveMessages")]
        public bool SaveMessages(Plugin plugin)
        {
			if (!messages.ContainsKey(plugin)) return false;
			var file = plugin.Name;
            Interface.GetMod().LangFileSystem.WriteObject(file, messages[plugin].Msgs);
			return true;
        }
		
        private void SaveLangs()
        {
            Interface.GetMod().DataFileSystem.WriteObject(langfile, userlangs);
        }

        #region Library Staff

        [LibraryFunction("RegisterMessages")]
        public void RegisterMessages(Dictionary<string, Dictionary<string, string>> msgs, Plugin plugin)
        {
			LoadFromDatafile(plugin);
            if (messages.ContainsKey(plugin))
            {
				var newmessages = msgs;
				if (!newmessages.ContainsKey(messages[plugin].MainLang)) {
					messages[plugin].MainLang = msgs.Keys.First();
				}
				var mainlang = messages[plugin].MainLang;
				
				// add new messages to all translations
				foreach(KeyValuePair<string,Dictionary<string, string>> kvp in msgs) {
					if (messages[plugin].Msgs.ContainsKey(kvp.Key)) {
						foreach(KeyValuePair<string,string> kvl in kvp.Value) {
							if (!messages[plugin].Msgs[kvp.Key].ContainsKey(kvl.Key)) {
								messages[plugin].Msgs[kvp.Key][kvl.Key] = kvl.Value;
							}
						}
					}
				}
				
				// remove old messages from all translations
				foreach(KeyValuePair<string,Dictionary<string, string>> kvp in messages[plugin].Msgs) {
					if (!newmessages.ContainsKey(kvp.Key)) newmessages[kvp.Key] = new Dictionary<string, string>();
					foreach(KeyValuePair<string,string> kvl in kvp.Value) {
						if (newmessages[kvp.Key].ContainsKey(kvl.Key)) {
							newmessages[kvp.Key][kvl.Key] = kvl.Value;
						}
					}
				}
				
				// needed 2 loops because we need compare them from configs and from plugin.
				
				messages[plugin].Msgs = newmessages;
            } else {
				messages[plugin].Msgs = msgs;
			}
			foreach(KeyValuePair<string,Dictionary<string, string>> kvp in messages[plugin].Msgs) {
				if (!languages.Contains(kvp.Key)) languages.Add(kvp.Key);
			}
			SaveMessages(plugin);
			// later will need to understand how this works
            /*HashSet<string> set;
            if (!permset.TryGetValue(owner, out set))
            {
                set = new HashSet<string>();
                permset.Add(owner, set);
                owner.OnRemovedFromManager += owner_OnRemovedFromManager;
            }
            set.Add(name);*/
			//return messages[plugin].Msgs;
        }
		
		// Manual register if needed
        [LibraryFunction("Register")]
        public void Register(Plugin plugin)
        {
			LoadFromDatafile(plugin);
		}

        #endregion

		// not sure yet how to use this
        private void owner_OnRemovedFromManager(Plugin sender, PluginManager manager)
        {
            //permset.Remove(sender);
        }

        #region Messages Stuff
		
        [LibraryFunction("GetMessages")]
        public Dictionary<string, Dictionary<string, string>> GetMessages(Plugin plugin)
        {
			if (!messages.ContainsKey(plugin)) return new Dictionary<string, Dictionary<string, string>>();
			return messages[plugin].Msgs;
		}
		
        [LibraryFunction("MessageExists")]
        public bool MessageExists(string message, Plugin plugin)
        {
			if (!messages.ContainsKey(plugin)) return false;
            return messages[plugin].Msgs[messages[plugin].MainLang].ContainsKey(message);
        }

        [LibraryFunction("GetMessage")]
        public string GetMessage(string userid, string message, Plugin plugin)
        {
			if (!MessageExists(message,plugin)) return message;
			var lang = GetLanguage(userid);
			return messages[plugin].Msgs[lang][message];
        }
		
        [LibraryFunction("RegisterMessage")]
        public bool RegisterMessage(string lang, string message, string value, Plugin plugin)
        {
			if (!messages.ContainsKey(plugin)) return false;
			if (!messages[plugin].Msgs.ContainsKey(lang)) messages[plugin].Msgs[lang] = new Dictionary<string, string>();
			if (!languages.Contains(lang)) languages.Add(lang);
			messages[plugin].Msgs[lang][message] = value;
			return true;
        }
		
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

        [LibraryFunction("GetLanguage")]
        public string GetLanguage(string userid)
        {
            if (userlangs.ContainsKey(userid)) return userlangs[userid];
			return defaultlang;
		}

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
		
        [LibraryFunction("GetLanguages")]
        public List<string> GetLanguages()
        {
			return languages;
        }

        #endregion
    }
}
