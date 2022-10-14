using AssettoServer.Network.Packets;
using AssettoServer.Network.Packets.Outgoing;
using AssettoServer.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GroupStreetRacingPlugin.Packets
{
    public class GroupStreetRacingUpdateEventPacket : IOutgoingNetworkPacket
    {
        public RaceUpdateEvent EventType { get; set; }
        public int EventData { get; set; }
        public GroupStreetRacingUpdateEventPacket(RaceUpdateEvent eventType, int eventData)
        {
            EventType = eventType;
            EventData = eventData;
        }

        public void ToWriter(ref PacketWriter writer)
        {
            writer.Write((byte)ACServerProtocol.Extended);
            writer.Write((byte)CSPMessageTypeTcp.ClientMessage);
            writer.Write<byte>(255);
            writer.Write<ushort>(60000);
            writer.Write(0x768984E4);
            writer.Write<int>(EventData);
            writer.Write<byte>((byte)EventType);
        }
    }
}
