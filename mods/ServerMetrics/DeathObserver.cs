using System;
using System.IO;

namespace ValheimServerMetrics
{
    // Network observations do not require a local scene instance of a remote player.
    internal sealed class DeathObserver
    {
        private static readonly int PlayerDeathHash = "OnDeath".GetStableHashCode();
        private readonly CharacterLeaderboard leaderboard;
        private readonly Action changed;
        private readonly Action<Exception> fault;
        private bool stopped;

        internal DeathObserver(CharacterLeaderboard leaderboard, Action changed, Action<Exception> fault)
        {
            this.leaderboard = leaderboard;
            this.changed = changed;
            this.fault = fault;
        }


        internal void Stop()
        {
            stopped = true;
        }


        // Prefix on ZRoutedRpc.RPC_RoutedRPC: leave the original reader position intact.
        internal void Observe(ZRpc rpc, ZPackage package)
        {
            if (stopped || package == null) return;
            long sender;
            long targetPeer;
            long objectUser;
            uint objectId;
            int method;
            int position = package.GetPos();
            try
            {
                // Three Int64s, a ZDOID, method hash, and empty argument package.
                if (package.Size() - position != 44) return;
                package.ReadLong(); // Message ID is not an event/life identity.
                sender = package.ReadLong();
                targetPeer = package.ReadLong();
                objectUser = package.ReadLong();
                objectId = package.ReadUInt();
                method = package.ReadInt();
                if (method != PlayerDeathHash || targetPeer != 0 || package.ReadInt() != 0) return;
            }
            catch (Exception error) when (error is IOException || error is ArgumentException || error is IndexOutOfRangeException)
            {
                return; // Untrusted malformed input is never a telemetry failure.
            }
            finally { package.SetPos(position); }

            var peer = FindPeer(rpc);
            if (peer == null || peer.m_uid != sender || peer.m_playerID == 0 ||
                peer.m_characterID.IsNone() || peer.m_characterID.UserID != objectUser || peer.m_characterID.ID != objectId)
                return;
            try
            {
                if (leaderboard.RecordDeath(CharacterKey(peer.m_playerID), EventKey(objectUser, objectId))) changed();
            }
            catch (Exception error) { fault(error); }
        }


        private static ZNetPeer FindPeer(ZRpc rpc)
        {
            var peers = ZNet.instance?.GetPeers();
            if (peers != null)
                for (int i = 0; i < peers.Count; i++)
                    if (peers[i] != null && peers[i].IsReady() && ReferenceEquals(peers[i].m_rpc, rpc)) return peers[i];
            return null;
        }


        private static string CharacterKey(long id) => id.ToString("x16", System.Globalization.CultureInfo.InvariantCulture);
        private static string EventKey(long user, uint id)
            => "d:" + ZNet.instance.GetWorldUID().ToString("x16", System.Globalization.CultureInfo.InvariantCulture) + ":" +
                user.ToString("x16", System.Globalization.CultureInfo.InvariantCulture) + ":" + id.ToString("x8", System.Globalization.CultureInfo.InvariantCulture);
    }
}
