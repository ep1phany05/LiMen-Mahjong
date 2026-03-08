using Mirror;
using Mirror.Discovery;
using UnityEngine;

namespace LiMen.Network
{
    public struct DiscoveryRequest : NetworkMessage
    {
        // 暂时为空，后续可加版本号校验
    }

    public struct DiscoveryResponse : NetworkMessage
    {
        public System.Net.IPEndPoint EndPoint { get; set; }
        public string hostName;
        public int playerCount;
        public int maxPlayers;
        public string gameMode;
    }

    public class LiMenNetworkDiscovery : NetworkDiscoveryBase<DiscoveryRequest, DiscoveryResponse>
    {
        protected override DiscoveryResponse ProcessRequest(DiscoveryRequest request, System.Net.IPEndPoint endpoint)
        {
            return new DiscoveryResponse
            {
                hostName = PlayerPrefs.GetString("PlayerName", "Host"),
                playerCount = NetworkServer.connections.Count,
                maxPlayers = LiMenNetworkManager.singleton?.maxPlayers ?? 4,
                gameMode = "Riichi"
            };
        }

        protected override void ProcessResponse(DiscoveryResponse response, System.Net.IPEndPoint endpoint)
        {
            response.EndPoint = endpoint;
            OnServerFound?.Invoke(response);
        }
    }
}
