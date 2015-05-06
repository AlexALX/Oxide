using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.IO;
using System;

using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using Oxide.Core.Configuration;

using Oxide.Rust.Libraries;

using Rust;

using UnityEngine;

namespace Oxide.Rust.Plugins
{
    /// <summary>
    /// The core Rust plugin
    /// </summary>
    public class RustCore : CSPlugin
    {
        // The pluginmanager
        private readonly PluginManager pluginmanager = Interface.Oxide.RootPluginManager;

        // The permission lib
        private readonly Permission permission = Interface.Oxide.GetLibrary<Permission>();
        private static readonly string[] DefaultGroups = { "player", "moderator", "admin" };

        // The localization lib
        private readonly Localization localization = Interface.Oxide.GetLibrary<Localization>();
        
        // The localization messages
        // message codes should be changed to more understable for people, its just example
        private Dictionary<string, Dictionary<string, string>> Messages = new Dictionary<string, Dictionary<string, string>>{
            ["en"] = new Dictionary<string, string>{
                ["LANG.DESC"] = "By this command you can choose server language.",
                ["LANG.CUR"] = "Current language: {LANG}",
                ["LANG.SYN"] = "Syntax: /lang <language>",
                ["LANG.AVA"] = "Available languages:",
                ["LANG.NO"] = "No languages yet.",
                ["LANG.SV"] = "Server language succesfully set to \"{LANG}\"."
            }
        };
        // i think here should be only english messages
        // all other should be in file what will come with oxide package. 
        
        // The command lib
        private readonly Command cmdlib = Interface.Oxide.GetLibrary<Command>();

        // Track when the server has been initialized
        private bool ServerInitialized;

        // Cache the serverInput field info
        private readonly FieldInfo serverInputField = typeof(BasePlayer).GetField("serverInput", BindingFlags.Instance | BindingFlags.NonPublic);

        // Track if a BasePlayer.OnAttacked call is in progress
        private bool isPlayerTakingDamage;

        /// <summary>
        /// Initializes a new instance of the RustCore class
        /// </summary>
        public RustCore()
        {
            // Set attributes
            Name = "rustcore";
            Title = "Rust Core";
            Author = "Oxide Team";
            Version = new VersionNumber(1, 0, 0);
        }

        /// <summary>
        /// Called when the plugin is initialising
        /// </summary>
        [HookMethod("Init")]
        private void Init()
        {
        
            // read main localization from oxide.lang.json in root directory 
            /* This code works, but due to restriction in DynamicConfigFile wont load file in root directory
            So i disabled it for now, i tested it without restriction and it was working fine.
            var langFile = Path.Combine(Environment.CurrentDirectory, "oxide.lang.json");
            if (File.Exists(langFile)) {
                Dictionary<string, Dictionary<string, string>> rootmessages = null;
                var langConfig = ConfigFile.Load<DynamicConfigFile>(langFile);
                if (langConfig!=null) {
                    var newmessages = Messages;
                    foreach(KeyValuePair<string,object> kvp in langConfig) {
                        if (kvp.Key=="en") continue; // ignore english, oxide translation file may be always up-to-date
                        if (kvp.Value is Dictionary<string, object>) {
                            var tbl = kvp.Value as Dictionary<string, object>;
                            newmessages[kvp.Key] = new Dictionary<string, string>();
                            foreach(KeyValuePair<string,object> kvl in tbl) {
                                // add only exists messages in english translation
                                if (Messages["en"].ContainsKey(kvl.Key)) {
                                    newmessages[kvp.Key][kvl.Key] = kvl.Value as string;
                                }
                            }
                        }
                    }
                    Messages = newmessages;
                }
            }*/
        
            // Register localization messages, it will store in rustcore.json file.
            // also all custom changes in rustcore.json will loaded automatically.
            Messages = localization.RegisterMessages(Messages,this);
            
            // Right now there is only one problem - how to update already register messages, they will stay same...
            // Like solution - if message updated set new key name, then it will be added automatically and old message removed.
        
            // Add our commands
            cmdlib.AddConsoleCommand("oxide.plugins", this, "cmdPlugins");
            cmdlib.AddConsoleCommand("oxide.load", this, "cmdLoad");
            cmdlib.AddConsoleCommand("oxide.unload", this, "cmdUnload");
            cmdlib.AddConsoleCommand("oxide.reload", this, "cmdReload");
            cmdlib.AddConsoleCommand("oxide.version", this, "cmdVersion");
            cmdlib.AddConsoleCommand("global.version", this, "cmdVersion");

            cmdlib.AddConsoleCommand("oxide.group", this, "cmdGroup");
            cmdlib.AddConsoleCommand("oxide.usergroup", this, "cmdUserGroup");
            cmdlib.AddConsoleCommand("oxide.grant", this, "cmdGrant");
            cmdlib.AddConsoleCommand("oxide.revoke", this, "cmdRevoke");
            
            // Reload language command, so no need to restart server for update rustcode.json file.
            // later probably will need file watcher for rustcore.json or so (file should be also renamed)
            cmdlib.AddConsoleCommand("oxide.langreload", this, "cmdLang");
            // chat /lang command
            cmdlib.AddChatCommand("lang", this, "chatLang");

            if (permission.IsLoaded)
            {
                var rank = 0;
                for (var i = DefaultGroups.Length - 1; i >= 0; i--)
                {
                    var defaultGroup = DefaultGroups[i];
                    if (!permission.GroupExists(defaultGroup)) permission.CreateGroup(defaultGroup, defaultGroup, rank++);
                }
            }
            // Configure remote logging
            RemoteLogger.SetTag("game", "rust");
            RemoteLogger.SetTag("protocol", typeof(Protocol).GetField("network").GetValue(null).ToString());
        }

        /// <summary>
        /// Checks if the permission system has loaded, shows an error if it failed to load
        /// </summary>
        /// <returns></returns>
        private bool PermissionsLoaded(ConsoleSystem.Arg arg)
        {
            if (permission.IsLoaded) return true;
            arg.ReplyWith("Unable to load permission files! Permissions will not work until the error has been resolved.\r\n => " + permission.LastException.Message);
            return false;
        }

        /// <summary>
        /// Called when the server is first initialized
        /// </summary>
        [HookMethod("OnServerInitialized")]
        private void OnServerInitialized()
        {
            if (ServerInitialized) return;
            ServerInitialized = true;
            // Configure the hostname after it has been set
            RemoteLogger.SetTag("hostname", server.hostname);
        }

        /// <summary>
        /// Called when the server is shutting down
        /// </summary>
        [HookMethod("OnServerShutdown")]
        private void OnServerShutdown()
        {
            Interface.Oxide.OnShutdown();
        }

        /// <summary>
        /// Called when another plugin has been loaded
        /// </summary>
        /// <param name="plugin"></param>
        [HookMethod("OnPluginLoaded")]
        private void OnPluginLoaded(Plugin plugin)
        {
            if (ServerInitialized) plugin.CallHook("OnServerInitialized");
        }

        /// <summary>
        /// Called when the "oxide.plugins" command has been executed
        /// </summary>
        [HookMethod("cmdPlugins")]
        private void cmdPlugins(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null && !arg.Player().IsAdmin()) return;

            var loaded_plugins = pluginmanager.GetPlugins().Where(pl => !pl.IsCorePlugin).ToArray();
            var loaded_plugin_names = new HashSet<string>(loaded_plugins.Select(pl => pl.Name));
            var unloaded_plugin_errors = new Dictionary<string, string>();
            foreach (var loader in Interface.Oxide.GetPluginLoaders())
            {
                foreach (var name in loader.ScanDirectory(Interface.Oxide.PluginDirectory).Except(loaded_plugin_names))
                {
                    string msg;
                    unloaded_plugin_errors[name] = (loader.PluginErrors.TryGetValue(name, out msg)) ? msg : "Unloaded";
                }
            }

            var total_plugin_count = loaded_plugins.Length + unloaded_plugin_errors.Count;
            if (total_plugin_count < 1)
            {
                arg.ReplyWith($"[Oxide] No plugins are currently available");
                return;
            }

            var output = $"[Oxide] Listing {loaded_plugins.Length + unloaded_plugin_errors.Count} plugins:";
            var number = 1;
            foreach (var plugin in loaded_plugins)
                output += $"\n  {number++:00} \"{plugin.Title}\" ({plugin.Version}) by {plugin.Author}";
            foreach (var plugin_name in unloaded_plugin_errors.Keys)
                output += $"\n  {number++:00} {plugin_name} - {unloaded_plugin_errors[plugin_name]}";
            arg.ReplyWith(output);
        }

        /// <summary>
        /// Called when the "oxide.load" command has been executed
        /// </summary>
        /// <param name="arg"></param>
        [HookMethod("cmdLoad")]
        private void cmdLoad(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null && !arg.Player().IsAdmin()) return;
            // Check arg 1 exists
            if (!arg.HasArgs(1))
            {
                arg.ReplyWith("Syntax: oxide.load *|<pluginname>+");
                return;
            }

            if (arg.GetString(0).Equals("*"))
            {
                Interface.Oxide.LoadAllPlugins();
                return;
            }

            foreach (var name in arg.Args)
            {
                if (string.IsNullOrEmpty(name)) continue;
                // Load
                Interface.Oxide.LoadPlugin(name);
                pluginmanager.GetPlugin(name);
            }
        }

        /// <summary>
        /// Called when the "oxide.unload" command has been executed
        /// </summary>
        /// <param name="arg"></param>
        [HookMethod("cmdUnload")]
        private void cmdUnload(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null && !arg.Player().IsAdmin()) return;
            // Check arg 1 exists
            if (!arg.HasArgs(1))
            {
                arg.ReplyWith("Syntax: oxide.unload *|<pluginname>+");
                return;
            }

            if (arg.GetString(0).Equals("*"))
            {
                Interface.Oxide.UnloadAllPlugins();
                return;
            }

            foreach (var name in arg.Args)
            {
                if (string.IsNullOrEmpty(name)) continue;

                // Unload
                Interface.Oxide.UnloadPlugin(name);
            }
        }

        /// <summary>
        /// Called when the "oxide.reload" command has been executed
        /// </summary>
        /// <param name="arg"></param>
        [HookMethod("cmdReload")]
        private void cmdReload(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null && !arg.Player().IsAdmin()) return;
            // Check arg 1 exists
            if (!arg.HasArgs(1))
            {
                arg.ReplyWith("Syntax: oxide.reload *|<pluginname>+");
                return;
            }

            if (arg.GetString(0).Equals("*"))
            {
                Interface.Oxide.ReloadAllPlugins();
                return;
            }

            foreach (var name in arg.Args)
            {
                if (string.IsNullOrEmpty(name)) continue;

                // Reload
                Interface.Oxide.ReloadPlugin(name);
            }
        }

        /// <summary>
        /// Called when the "version" command has been executed
        /// </summary>
        /// <param name="arg"></param>
        [HookMethod("cmdVersion")]
        private void cmdVersion(ConsoleSystem.Arg arg)
        {
            // Get the Rust network protocol version at runtime
            var protocol = typeof(Protocol).GetField("network").GetValue(null).ToString();

            // Get the Oxide Core version
            var oxide = OxideMod.Version.ToString();

            // Show the versions
            if (!string.IsNullOrEmpty(protocol) && !string.IsNullOrEmpty(oxide))
            {
                arg.ReplyWith("Oxide Version: " + oxide + ", Rust Protocol: " + protocol);
            }
        }

        /// <summary>
        /// Called when the "group" command has been executed
        /// </summary>
        /// <param name="arg"></param>
        [HookMethod("cmdGroup")]
        private void cmdGroup(ConsoleSystem.Arg arg)
        {
            if (!PermissionsLoaded(arg)) return;

            if (arg.Player() != null && !arg.Player().IsAdmin()) return;
            // Check 2 args exists
            if (!arg.HasArgs(2))
            {
                arg.ReplyWith("Syntax: oxide.group <add|remove|set> <name> [title] [rank]");
                return;
            }

            var mode = arg.GetString(0);
            var name = arg.GetString(1);
            var title = arg.GetString(2);
            var rank = arg.GetInt(3);

            if (mode.Equals("add"))
            {
                if (permission.GroupExists(name))
                {
                    arg.ReplyWith("Group '" + name + "' already exist");
                    return;
                }
                permission.CreateGroup(name, title, rank);
                arg.ReplyWith("Group '" + name + "' created");
            }
            else if (mode.Equals("remove"))
            {
                if (!permission.GroupExists(name))
                {
                    arg.ReplyWith("Group '" + name + "' doesn't exist");
                    return;
                }
                permission.RemoveGroup(name);
                arg.ReplyWith("Group '" + name + "' deleted");
            }
            else if (mode.Equals("set"))
            {
                if (!permission.GroupExists(name))
                {
                    arg.ReplyWith("Group '" + name + "' doesn't exist");
                    return;
                }
                permission.SetGroupTitle(name, title);
                permission.SetGroupRank(name, rank);
                arg.ReplyWith("Group '" + name + "' changed");
            }
        }

        /// <summary>
        /// Called when the "group" command has been executed
        /// </summary>
        /// <param name="arg"></param>
        [HookMethod("cmdUserGroup")]
        private void cmdUserGroup(ConsoleSystem.Arg arg)
        {
            if (!PermissionsLoaded(arg)) return;

            if (arg.Player() != null && !arg.Player().IsAdmin()) return;
            // Check 3 args exists
            if (!arg.HasArgs(3))
            {
                arg.ReplyWith("Syntax: oxide.usergroup <add|remove> <username> <groupname>");
                return;
            }

            var mode = arg.GetString(0);
            var name = arg.GetString(1);
            var group = arg.GetString(2);

            var player = FindPlayer(name);
            if (player == null && !permission.UserExists(name))
            {
                arg.ReplyWith("User '" + name + "' not found");
                return;
            }
            var userId = name;
            if (player != null)
            {
                userId = player.userID.ToString();
                name = player.displayName;
                permission.GetUserData(userId).LastSeenNickname = name;
            }

            if (!permission.GroupExists(group))
            {
                arg.ReplyWith("Group '" + group + "' doesn't exist");
                return;
            }

            if (mode.Equals("add"))
            {
                permission.AddUserGroup(userId, group);
                arg.ReplyWith("User '" + name + "' assigned group: " + group);
            }
            else if (mode.Equals("remove"))
            {
                permission.RemoveUserGroup(userId, group);
                arg.ReplyWith("User '" + name + "' removed from group: " + group);
            }
        }

        /// <summary>
        /// Called when the "grant" command has been executed
        /// </summary>
        /// <param name="arg"></param>
        [HookMethod("cmdGrant")]
        private void cmdGrant(ConsoleSystem.Arg arg)
        {
            if (!PermissionsLoaded(arg)) return;

            if (arg.Player() != null && !arg.Player().IsAdmin()) return;
            // Check 3 args exists
            if (!arg.HasArgs(3))
            {
                arg.ReplyWith("Syntax: oxide.grant <group|user> <name|id> <permission>");
                return;
            }

            var mode = arg.GetString(0);
            var name = arg.GetString(1);
            var perm = arg.GetString(2);

            if (mode.Equals("group"))
            {
                if (!permission.GroupExists(name))
                {
                    arg.ReplyWith("Group '" + name + "' doesn't exist");
                    return;
                }
                permission.GrantGroupPermission(name, perm, null);
                arg.ReplyWith("Group '" + name + "' granted permission: " + perm);
            }
            else if (mode.Equals("user"))
            {
                var player = FindPlayer(name);
                if (player == null && !permission.UserExists(name))
                {
                    arg.ReplyWith("User '" + name + "' not found");
                    return;
                }
                var userId = name;
                if (player != null)
                {
                    userId = player.userID.ToString();
                    name = player.displayName;
                    permission.GetUserData(name).LastSeenNickname = name;
                }
                permission.GrantUserPermission(userId, perm, null);
                arg.ReplyWith("User '" + name + "' granted permission: " + perm);
            }
        }

        /// <summary>
        /// Called when the "grant" command has been executed
        /// </summary>
        /// <param name="arg"></param>
        [HookMethod("cmdRevoke")]
        private void cmdRevoke(ConsoleSystem.Arg arg)
        {
            if (!PermissionsLoaded(arg)) return;

            if (arg.Player() != null && !arg.Player().IsAdmin()) return;
            // Check 3 args exists
            if (!arg.HasArgs(3))
            {
                arg.ReplyWith("Syntax: oxide.revoke <group|user> <name|id> <permission>");
                return;
            }

            var mode = arg.GetString(0);
            var name = arg.GetString(1);
            var perm = arg.GetString(2);

            if (mode.Equals("group"))
            {
                if (!permission.GroupExists(name))
                {
                    arg.ReplyWith("Group '" + name + "' doesn't exist");
                    return;
                }
                permission.RevokeGroupPermission(name, perm);
                arg.ReplyWith("Group '" + name + "' revoked permission: " + perm);
            }
            else if (mode.Equals("user"))
            {
                var player = FindPlayer(name);
                if (player == null && !permission.UserExists(name))
                {
                    arg.ReplyWith("User '" + name + "' not found");
                    return;
                }
                var userId = name;
                if (player != null)
                {
                    userId = player.userID.ToString();
                    name = player.displayName;
                    permission.GetUserData(name).LastSeenNickname = name;
                }
                permission.RevokeUserPermission(userId, perm);
                arg.ReplyWith("User '" + name + "' revoked permission: " + perm);
            }
        }

        private BasePlayer FindPlayer(string nameOrIdOrIp)
        {
            var player = BasePlayer.Find(nameOrIdOrIp);
            if (player == null)
            {
                ulong id;
                if (ulong.TryParse(nameOrIdOrIp, out id))
                    player = BasePlayer.FindSleeping(id);
            }
            return player;
        }

        [HookMethod("chatLang")]
        private void chatLang(BasePlayer player, string command, string[] args)
        {
            // all oxide functions probably need to be translated later
            var sid = player.userID.ToString();
            var plang = localization.GetLanguage(sid,this);
            if (args.Length==0)
            {
                var langs = localization.GetLanguages();
                player.ChatMessage(
                    Messages[plang]["LANG.DESC"]+"\n"+
                    Messages[plang]["LANG.CUR"].Replace("{LANG}",localization.GetLanguage(sid))+"\n"+
                    Messages[plang]["LANG.SYN"]+"\n"+
                    Messages[plang]["LANG.AVA"]+"\n"+
                    (langs.Count>0?string.Join(", ", langs.ToArray()):Messages[plang]["LANG.NO"])
                );
                return;
            }

            var lang = args[0];
            localization.SetLanguage(sid,lang);
            // update language just after you choosed new
            plang = localization.GetLanguage(sid,this);
            player.ChatMessage(Messages[plang]["LANG.SV"].Replace("{LANG}",lang));            
        }
        
        [HookMethod("cmdLang")]
        private void cmdLang(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null && !arg.Player().IsAdmin()) return;
            // register messages again - it will act like reload.
            Messages = localization.RegisterMessages(Messages,this);
            arg.ReplyWith("Oxide core translations successfully reloaded.");            
        }

        /// <summary>
        /// Called when the server wants to know what tags to use
        /// </summary>
        /// <param name="oldtags"></param>
        /// <returns></returns>
        [HookMethod("ModifyTags")]
        private string ModifyTags(string oldtags)
        {
            // We're going to call out and build a list of all tags to use
            var taglist = new List<string>(oldtags.Split(','));
            Interface.CallHook("BuildServerTags", taglist);
            return string.Join(",", taglist.ToArray());
        }

        /// <summary>
        /// Called when it's time to build the tags list
        /// </summary>
        /// <param name="taglist"></param>
        [HookMethod("BuildServerTags")]
        private void BuildServerTags(IList<string> taglist)
        {
            // Add modded and oxide
            taglist.Add("modded");
            taglist.Add("oxide");
        }

        /// <summary>
        /// Called when a user is attempting to connect
        /// </summary>
        /// <param name="connection"></param>
        /// <returns></returns>
        [HookMethod("OnUserApprove")]
        private object OnUserApprove(Network.Connection connection)
        {
            // Call out and see if we should reject
            object canlogin = Interface.CallHook("CanClientLogin", connection);
            if (canlogin != null)
            {
                // If it's a bool and it's true, let them in
                if (canlogin is bool && (bool)canlogin) return null;

                // If it's a string, reject them with a message
                if (canlogin is string)
                {
                    ConnectionAuth.Reject(connection, (string)canlogin);
                    return true;
                }

                // We don't know what type it is, reject them with it anyway
                ConnectionAuth.Reject(connection, canlogin.ToString());
                return true;
            }
            return null;
        }

        /// <summary>
        /// Called when a console command was run
        /// </summary>
        /// <param name="arg"></param>
        /// <returns></returns>
        [HookMethod("OnRunCommand")]
        private object OnRunCommand(ConsoleSystem.Arg arg)
        {
            if (arg == null || arg.cmd == null) return null;

            if (arg.cmd.namefull == "chat.say")
            {
                // Get the args
                string str = arg.GetString(0, "text");
                if (str.Length == 0) return null;

                // Is it a chat command?
                if (str[0] == '/' || str[0] == '!')
                {
                    // Get the message
                    string message = str.Substring(1);

                    // Parse it
                    string cmd;
                    string[] args;
                    ParseChatCommand(message, out cmd, out args);
                    if (cmd == null) return null;

                    // Handle it
                    var player = arg.connection.player as BasePlayer;
                    if (player == null)
                    {
                        Interface.Oxide.LogDebug("Player is actually a {0}!", arg.connection.player.GetType());
                    }
                    else
                    {
                        if (!cmdlib.HandleChatCommand(player, cmd, args))
                        {
                            player.SendConsoleCommand("chat.add", 0, string.Format("Unknown command '{0}'!", cmd));
                        }
                    }

                    // Handled
                    arg.ReplyWith(string.Empty);
                    return true;
                }
            }

            // Default behavior
            return null;
        }

        /// <summary>
        /// Parses the specified chat command
        /// </summary>
        /// <param name="argstr"></param>
        /// <param name="cmd"></param>
        /// <param name="args"></param>
        private void ParseChatCommand(string argstr, out string cmd, out string[] args)
        {
            List<string> arglist = new List<string>();
            StringBuilder sb = new StringBuilder();
            bool inlongarg = false;
            for (int i = 0; i < argstr.Length; i++)
            {
                char c = argstr[i];
                if (c == '"')
                {
                    if (inlongarg)
                    {
                        string arg = sb.ToString().Trim();
                        if (!string.IsNullOrEmpty(arg)) arglist.Add(arg);
                        sb = new StringBuilder();
                        inlongarg = false;
                    }
                    else
                    {
                        inlongarg = true;
                    }
                }
                else if (char.IsWhiteSpace(c) && !inlongarg)
                {
                    string arg = sb.ToString().Trim();
                    if (!string.IsNullOrEmpty(arg)) arglist.Add(arg);
                    sb = new StringBuilder();
                }
                else
                {
                    sb.Append(c);
                }
            }
            if (sb.Length > 0)
            {
                string arg = sb.ToString().Trim();
                if (!string.IsNullOrEmpty(arg)) arglist.Add(arg);
            }
            if (arglist.Count == 0)
            {
                cmd = null;
                args = null;
                return;
            }
            cmd = arglist[0];
            arglist.RemoveAt(0);
            args = arglist.ToArray();
        }

        /// <summary>
        /// Called when the player has been initialized
        /// </summary>
        /// <param name="player"></param>
        [HookMethod("OnPlayerInit")]
        private void OnPlayerInit(BasePlayer player)
        {
            var authLevel = player.net.connection.authLevel;
            if (authLevel <= DefaultGroups.Length && permission.IsLoaded)
            {
                var userId = player.userID.ToString();
                permission.GetUserData(userId).LastSeenNickname = player.displayName;

                // Remove player from old groups if auth level changed
                for (var i = 0; i < DefaultGroups.Length; i++)
                    if (i != authLevel) permission.RemoveUserGroup(userId, DefaultGroups[i]);

                // Add player to default group
                permission.AddUserGroup(userId, DefaultGroups[authLevel]);
            }
        }

        /// <summary>
        /// Called when a player tick is received from a client
        /// </summary>
        /// <param name="player"></param>
        /// <param name="msg"></param>
        /// <returns></returns>
        [HookMethod("OnPlayerTick")]
        private object OnPlayerTick(BasePlayer player, PlayerTick msg)
        {
            return Interface.CallHook("OnPlayerInput", player, serverInputField.GetValue(player));
        }

        /// <summary>
        /// Called when the player has been melee attacked
        /// </summary>
        /// <param name="basemelee"></param>
        /// <param name="hit"></param>
        [HookMethod("OnMeleeAttack")]
        private object OnMeleeAttack(BaseMelee melee, HitInfo hitinfo)
        {
            return Interface.CallHook("OnPlayerAttack", melee.ownerPlayer, hitinfo);
        }

        /// <summary>
        /// Called when the entity has spawned
        /// This is used to handle the deprecated hook OnEntitySpawn
        /// </summary>
        /// <param name="entity"></param>
        [HookMethod("OnEntitySpawned")]
        private object OnEntitySpawned(BaseNetworkable entity)
        {
            return Interface.Oxide.CallDeprecatedHook("OnEntitySpawn", entity);
        }

        /// <summary>
        /// Called when the player has been respawned
        /// This is used to handle the deprecated hook OnPlayerSpawn
        /// </summary>
        /// <param name="player"></param>
        [HookMethod("OnPlayerRespawned")]
        private object OnPlayerRespawned(BasePlayer player)
        {
            return Interface.Oxide.CallDeprecatedHook("OnPlayerSpawn", player);
        }

        /// <summary>
        /// Called when the player upgrades a BuildingBlock
        /// </summary>
        /// <param name="block"></param>
        /// <param name="msg"></param>
        /// <param name="grade"></param>
        [HookMethod("OnBuildingBlockDoUpgradeToGrade")]
        private object OnBuildingBlockDoUpgradeToGrade(BuildingBlock block, BaseEntity.RPCMessage msg, BuildingGrade.Enum grade)
        {
            return Interface.CallHook("OnBuildingBlockUpgrade", block, msg.player, grade);
        }

        /// <summary>
        /// Called when the player demolishes a BuildingBlock
        /// </summary>
        /// <param name="block"></param>
        /// <param name="msg"></param>
        [HookMethod("OnBuildingBlockDoImmediateDemolish")]
        private object OnBuildingBlockDoImmediateDemolish(BuildingBlock block, BaseEntity.RPCMessage msg)
        {
            return Interface.CallHook("OnBuildingBlockDemolish", block, msg.player);
        }

        /// <summary>
        /// Called when the player rotates a BuildingBlock
        /// </summary>
        /// <param name="block"></param>
        /// <param name="msg"></param>
        [HookMethod("OnBuildingBlockDoRotation")]
        private object OnBuildingBlockDoRotation(BuildingBlock block, BaseEntity.RPCMessage msg)
        {
            return Interface.CallHook("OnBuildingBlockRotate", block, msg.player);
        }

        /// <summary>
        /// Called when a player locks a sign
        /// </summary>
        /// <param name="sign"></param>
        /// <param name="msg"></param>
        /// <returns></returns>
        [HookMethod("LockSign")]
        private object LockSign(Signage sign, BaseEntity.RPCMessage msg)
        {
            return Interface.CallHook("OnSignLocked", sign, msg.player);
        }

        /// <summary>
        /// Called when a player has changed a sign
        /// </summary>
        /// <param name="sign"></param>
        /// <param name="msg"></param>
        [HookMethod("UpdateSign")]
        private object UpdateSign(Signage sign, BaseEntity.RPCMessage msg)
        {
            return Interface.CallHook("OnSignUpdated", sign, msg.player);
        }

        /// <summary>
        /// Called when an item loses durability
        /// </summary>
        /// <param name="item"></param>
        /// <param name="amount"></param>
        [HookMethod("LoseCondition")]
        private object LoseCondition(Item item, float amount)
        {
            var arguments = new object[] { item, amount };
            Interface.Oxide.CallHook("OnLoseCondition", arguments);
            amount = (float)arguments[1];
            float condition = item.condition;
            item.condition -= amount;

            if ((item.condition <= 0f) && (item.condition < condition))
            {
                item.OnBroken();
            }

            return true;
        }

        /// <summary>
        /// Called when a BasePlayer is attacked
        /// This is used to call OnEntityTakeDamage for a BasePlayer when attacked
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="info"></param>
        [HookMethod("OnBasePlayerAttacked")]
        private object OnBasePlayerAttacked(BasePlayer player, HitInfo info)
        {
            if (isPlayerTakingDamage) return null;
            if (Interface.Oxide.CallHook("OnEntityTakeDamage", player, info) != null) return true;
            isPlayerTakingDamage = true;
            player.OnAttacked(info);
            isPlayerTakingDamage = false;
            return true;
        }

        /// <summary>
        /// Called when a BasePlayer is hurt
        /// This is used to call OnEntityTakeDamage when a player was hurt without being attacked
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="info"></param>
        /// <returns></returns>
        [HookMethod("OnBasePlayerHurt")]
        private object OnBasePlayerHurt(BasePlayer entity, HitInfo info)
        {
            if (isPlayerTakingDamage) return null;
            return Interface.Oxide.CallHook("OnEntityTakeDamage", entity, info);
        }

        /// <summary>
        /// Called when a BaseCombatEntity takes damage
        /// This is used to call OnEntityTakeDamage for anything other than a BasePlayer
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="info"></param>
        [HookMethod("OnBaseCombatEntityHurt")]
        private object OnBaseCombatEntityHurt(BaseCombatEntity entity, HitInfo info)
        {
            if (entity is BasePlayer) return null;
            return Interface.Oxide.CallHook("OnEntityTakeDamage", entity, info);
        }

        /// <summary>
        /// Called when a BaseCombatEntity takes damage
        /// This is used to handle the deprecated hook OnEntityAttacked
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="info"></param>
        [HookMethod("OnEntityTakeDamage")]
        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            return Interface.Oxide.CallDeprecatedHook("OnEntityAttacked", entity, info);
        }

        /// <summary>
        /// Called when a player uses a door
        /// This is used to handle the deprecated hook CanOpenDoor
        /// </summary>
        /// <param name="player"></param>
        /// <param name="doorlock"></param>
        [HookMethod("CanUseDoor")]
        private object CanUseDoor(BasePlayer player, BaseLock doorlock)
        {
            return Interface.Oxide.CallDeprecatedHook("CanOpenDoor", player, doorlock);
        }

        /// <summary>
        /// Called when a player throws a weapon like a Timed Explosive Charge or a F1 Grenade
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="entity"></param>
        [HookMethod("OnWeaponDoThrow")]
        private object OnWeaponDoThrow(BaseEntity.RPCMessage msg, BaseEntity entity)
        {
            return Interface.CallHook("OnWeaponThrown", msg.player, entity);
        }

    }
}
