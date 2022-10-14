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
    public class GroupStreetRacingCurrentRacePacket : IOutgoingNetworkPacket
    {
        public const int Length = 20;
        public byte[] SessionIds { get; init; } = new byte[Length];
        public byte[] HealthOfCars { get; init; } = new byte[Length];
        public byte[] RacersStatus { get; init; } = new byte[Length];
        public StreetRaceStatus RaceStatus { get; set; }        

        public GroupStreetRacingCurrentRacePacket(byte[] sessionIds, byte[] healthOfCars, byte[] racersStatus, StreetRaceStatus raceStatus)
        {
            SessionIds = sessionIds;
            HealthOfCars = healthOfCars;
            RacersStatus = racersStatus;           
            RaceStatus = raceStatus;            
        }

        public void ToWriter(ref PacketWriter writer)
        {
            writer.Write((byte)ACServerProtocol.Extended);
            writer.Write((byte)CSPMessageTypeTcp.ClientMessage);
            writer.Write<byte>(255);
            writer.Write<ushort>(60000);
            writer.Write(0xDF0FDF1E);            
            writer.WriteBytes(HealthOfCars);
            writer.Write<byte>((byte)RaceStatus);
            writer.WriteBytes(RacersStatus);
            writer.WriteBytes(SessionIds);            
        }
    }
}
