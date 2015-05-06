﻿using System;

using Oxide.Core.Plugins;

namespace Oxide.TheForest.Plugins
{
    /// <summary>
    /// Responsible for loading The Forest core plugins
    /// </summary>
    public class TheForestPluginLoader : PluginLoader
    {
        public override Type[] CorePlugins => new[] { typeof(TheForestCore) };
    }
}
