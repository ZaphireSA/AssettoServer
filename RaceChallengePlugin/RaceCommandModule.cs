﻿using AssettoServer.Commands;
using AssettoServer.Commands.Attributes;
using AssettoServer.Network.Tcp;
using Qmmands;

namespace RaceChallengePlugin;

public class RaceCommandModule : ACModuleBase
{
    [Command("race")]
    public void Race(ACTcpClient player)
        => Context.Client.EntryCar.GetRace().ChallengeCar(player.EntryCar);

    [Command("accept")]
    public async ValueTask AcceptRaceAsync()
    {
        Race currentRace = Context.Client.EntryCar.GetRace().CurrentRace;
        if (currentRace == null)
            Reply("You do not have a pending race request.");
        else if (currentRace.HasStarted)
            Reply("This race has already started.");
        else
            await currentRace.StartAsync();
    }
    
    [Command("healthupdate")]
    [RequireAdmin]
    public void HealthUpdate(RaceChallengeEvent eventType, int eventData, float ownHealth, float rivalHealth)
    {
        var packet = new RaceChallengeUpdate
        {
            EventType = eventType,
            EventData = eventData,
            OwnHealth = ownHealth,
            RivalHealth = rivalHealth,
        };

        Context.Client.SendPacket(packet);
    }
}