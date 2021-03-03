﻿using Impostor.Server.Utils;

namespace Impostor.Server.Config
{
    internal class AnnouncementsServerConfig
    {
        private string? _resolvedListenIp;

        public const string Section = "AnnouncementsServer";

        public bool Enabled { get; set; } = true;

        public string ListenIp { get; set; } = "0.0.0.0";

        public ushort ListenPort { get; set; } = 22024;

        public string ResolveListenIp()
        {
            return _resolvedListenIp ??= IpUtils.ResolveIp(ListenIp);
        }
    }
}
