using AssettoServer.Network.Packets.Outgoing;
using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Plugin;
using AssettoServer.Utils;
using GroupStreetRacingPlugin.Packets;
using JetBrains.Annotations;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace GroupStreetRacingPlugin
{
    [UsedImplicitly]
    public class GroupStreetRacing : CriticalBackgroundService, IAssettoServerAutostart
    {
        private readonly CSPFeatureManager _cspFeatureManager;
        private readonly EntryCarManager _entryCarManager;
        private readonly ACServerConfiguration _acServerConfiguration;
        public readonly Dictionary<int, EntryCarForStreetRace> EntryCars = new Dictionary<int, EntryCarForStreetRace>();
        private readonly SessionManager _sessionManager;
        private readonly GroupStreetRacingConfiguration _configuration;
        //public List<EntryCarForStreetRace> CarsWithHazardsOn = new List<EntryCarForStreetRace>();

        //public void RemoveCarFromHazardList(EntryCarForStreetRace car)
        //{
        //    CarsWithHazardsOn.Remove(car);
        //    UpdateHazardList();
        //}

        //public void AddCarFromHazardList(EntryCarForStreetRace car)
        //{            
        //    CarsWithHazardsOn.Add(car);
        //    UpdateHazardList();
        //}

        //public void UpdateHazardList()
        //{
        //    byte[] sessionIds = new byte[GroupStreetRacingHazardsPacket.Length];
        //    byte[] healthOfCars = new byte[GroupStreetRacingHazardsPacket.Length];
        //    Array.Fill(sessionIds, (byte)255);
        //    Array.Fill(healthOfCars, (byte)255);

        //    for (var s = 0; s < CarsWithHazardsOn.Count; s++)
        //    {
        //        sessionIds[s] = CarsWithHazardsOn[s].EntryCar.SessionId;
        //        healthOfCars[s] = (byte) CarsWithHazardsOn[s].CarHealth;
        //    }

        //    Log.Debug("First Car S: " + sessionIds[0] + " , health: " + healthOfCars[0]);

        //    var packet = new GroupStreetRacingHazardsPacket(sessionIds, healthOfCars);

        //    foreach(var carInstance in _instances)
        //    {
        //        var client = carInstance.Value.EntryCar.Client;
        //        if (client == null || !client.HasSentFirstUpdate)
        //            continue;

        //        client?.SendPacket(packet);
        //    }
        //}

        public GroupStreetRacing(GroupStreetRacingConfiguration configuration,
            ACServerConfiguration acServerConfiguration,
            EntryCarManager entryCarManager,
            CSPFeatureManager cspFeatureManager,
            CSPServerScriptProvider scriptProvider,
            SessionManager sessionManager,
            IHostApplicationLifetime applicationLifetime) : base(applicationLifetime)
        {
            _acServerConfiguration = acServerConfiguration;
            _cspFeatureManager = cspFeatureManager;
            _entryCarManager = entryCarManager;
            _sessionManager = sessionManager;
            _configuration = configuration;
            Log.Debug("Sample plugin constructor called! Hello: {Hello}", configuration.Hello);

            if (_acServerConfiguration.Extra.EnableClientMessages)
            {
                using var streamReader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("GroupStreetRacingPlugin.lua.groupstreetracing.lua")!);
                scriptProvider.AddScript(streamReader.ReadToEnd(), "groupstreetracing.lua");
            }
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Log.Debug("Sample plugin autostart called");
            if (!_cspFeatureManager.Features.ContainsKey("CLIENT_MESSAGES"))
                throw new InvalidOperationException("EnableClientMessages and CSP 0.1.77+ are required for this plugin");

            foreach (EntryCar entryCar in _entryCarManager.EntryCars)
            {
                EntryCars.Add((int)entryCar.SessionId, new EntryCarForStreetRace(entryCar, this, _sessionManager, _configuration));
                //raceChallengePlugin._instances.Add((int)entryCar.SessionId, raceChallengePlugin._entryCarRaceFactory(entryCar));
            }

            return Task.CompletedTask;
        }        

        private void OnSessionChanged(SessionManager sender, SessionChangedEventArgs args)
        {
         
        }

        //Switch on hazards
        //This creates a session around your car and invites every car within 5meters
        //Flash your indicators to accept the challenge
        //Once the person that initiated the session switches off his hazards, the race starts in 10 seconds

        //Lose conditions
        //Each player starts with X amount of health. Crashing causes you to lose health.
        //If the player is x amount of meters behind then he also loses
    }
}
