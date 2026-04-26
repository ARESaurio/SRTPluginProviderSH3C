using SRTPluginBase;
using System;

namespace SRTPluginProviderSH3C
{
    internal class PluginInfo : IPluginInfo
    {
        public string Name        => "Game Memory Provider (Silent Hill 3 (2003))";
        public string Description => "A game memory provider plugin for Silent Hill 3 (2003) PC.";
        public string Author      => "Ares";
        public Uri    MoreInfoURL => new Uri("https://github.com/ARESaurio/SRTPluginProviderSH3C");

        public int VersionMajor    => assemblyVersion.Major;
        public int VersionMinor    => assemblyVersion.Minor;
        public int VersionBuild    => assemblyVersion.Build;
        public int VersionRevision => assemblyVersion.Revision;

        private readonly Version assemblyVersion =
            System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
    }
}
