using AssettoServer.Network.Packets;
using AssettoServer.Network.Packets.Incoming;
using AssettoServer.Network.Packets.Outgoing;
using AssettoServer.Server;
using Microsoft.Extensions.Configuration;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;



namespace GroupStreetRacingPlugin
{
    public class EntryCarForStreetRace
    {
        public readonly EntryCar EntryCar;
        private int LightFlashCount { get; set; }
        private long LastLightFlashTime { get; set; }
        private long LastRaceChallengeTime { get; set; }
        private readonly GroupStreetRacingConfiguration _configuration;

        public bool IsHazardsOn = false;        

        public StreetRace? CurrentRace { get; set; }
        private readonly SessionManager _sessionManager;

        private GroupStreetRacing _groupStreetRacing;
        //internal Race? CurrentRace { get; set; }

        public EntryCarForStreetRace(EntryCar entryCar, GroupStreetRacing groupStreetRacing, SessionManager sessionManager, GroupStreetRacingConfiguration configuration)
        {
            EntryCar = entryCar;
            _groupStreetRacing = groupStreetRacing;
            _sessionManager = sessionManager;
            _configuration = configuration;
            // ISSUE: method pointer
            //EntryCar.PositionUpdateReceived += //new EventHandlerIn<EntryCar, PositionUpdateIn>(EntryCar, _entryCar_PositionUpdateReceived);
            EntryCar.PositionUpdateReceived += new EventHandlerIn<EntryCar, PositionUpdateIn>(_entryCar_PositionUpdateReceived);            

            //EntryCar.PositionUpdateReceived += _entryCar_PositionUpdateReceived;
            //EntryCar.ResetInvoked += _entryCar_ResetInvoked;            
        }

        //private void Client_Collision(AssettoServer.Network.Tcp.ACTcpClient sender, CollisionEventArgs args)
        //{                        
            //if (sender.SessionId == EntryCar.SessionId)
            //{
                //var newHealth = CarHealth - (int)args.Speed;
                //Log.Debug("Health Before: " + CarHealth + ", after: " + newHealth);
                //CarHealth = Math.Clamp(newHealth, 0, 100);
                //if (IsHazardsOn)
                //    _groupStreetRacing.UpdateHazardList();
            //}
            
            //EventType = (byte)(args.TargetCar == null ? ClientEventType.CollisionWithEnv : ClientEventType.CollisionWithCar),
            //SessionId = sender.SessionId,
            //TargetSessionId = args.TargetCar?.SessionId,
            //Speed = args.Speed,
            //WorldPosition = args.Position,
            //RelPosition = args.RelPosition,
        //}

        private void _entryCar_ResetInvoked(EntryCar sender, EventArgs args)
        {
            //throw new NotImplementedException();
        }

        private void _entryCar_PositionUpdateReceived(EntryCar sender, in PositionUpdateIn positionUpdate)
        {
            //throw new NotImplementedException();
            //if ((this._entryCar.Status.StatusFlag & 8192) == null && (positionUpdate.StatusFlag & 8192) != null)
            if ((EntryCar.Status.StatusFlag & CarStatusFlags.HazardsOn) != 0)
            {
                //if (IsHazardsOn) return;
                if (!IsHazardsOn)
                {
                    IsHazardsOn = true;
                    if (CurrentRace == null)
                    {
                        CurrentRace = new StreetRace(this, _sessionManager, _configuration);
                    }
                }

                //if (EntryCar.Client != null && EntryCar.Client.HasSentFirstUpdate)
                //    EntryCar.Client.Collision += Client_Collision;
                //_groupStreetRacing.AddCarFromHazardList(this);
            } else if ((EntryCar.Status.StatusFlag & CarStatusFlags.HazardsOn) == 0)
            {
                //if (!IsHazardsOn) return;
                if (IsHazardsOn)
                {
                    IsHazardsOn = false;
                    if (CurrentRace != null)
                    {
                        CurrentRace.CarToggleHazards(this, false);
                    }
                }
                //if (EntryCar.Client != null && EntryCar.Client.HasSentFirstUpdate)
                    //EntryCar.Client.Collision -= Client_Collision;
                //_groupStreetRacing.RemoveCarFromHazardList(this);
            }
            

            long timeMilliseconds = _sessionManager.ServerTimeMilliseconds;
            if (((EntryCar.Status.StatusFlag & CarStatusFlags.LightsOn) == 0 
                && (positionUpdate.StatusFlag & CarStatusFlags.LightsOn) != 0)
                || ((EntryCar.Status.StatusFlag & CarStatusFlags.HighBeamsOff) == 0
                && (positionUpdate.StatusFlag & CarStatusFlags.HighBeamsOff) != 0)
                )
            {
                LastLightFlashTime = timeMilliseconds;
                ++LightFlashCount;
                Log.Debug((sender?.Client?.Name ?? "") + " flashed " + LightFlashCount.ToString() + " times.");
            }

            if (timeMilliseconds - LastLightFlashTime > 5000L && LightFlashCount > 0)
            {
                LightFlashCount = 0;
            }
            

            if (LightFlashCount != 3)
                return;
            LightFlashCount = 0;
            
            if (timeMilliseconds - LastRaceChallengeTime <= 5000L)
                return;

            Task.Run(new Action(ChallengeNearbyCar));
            LastRaceChallengeTime = timeMilliseconds;

            //if ((EntryCar.Status.StatusFlag & 32) == null && (positionUpdate.StatusFlag & 32) != null || (this._entryCar.Status.StatusFlag & 16384) == null && (positionUpdate.StatusFlag & 16384) != null)
            //{
            //    this.LastLightFlashTime = timeMilliseconds;
            //    ++this.LightFlashCount;
            //}
            //if ((this._entryCar.Status.StatusFlag & 8192) == null && (positionUpdate.StatusFlag & 8192) != null)
            //{
            //    Race currentRace = this.CurrentRace;
            //    if (currentRace != null && !currentRace.HasStarted && !currentRace.LineUpRequired && this.CurrentRace.Challenged == sender)
            //        this.CurrentRace.StartAsync();
            //}
            //if (timeMilliseconds - this.LastLightFlashTime > 3000L && this.LightFlashCount > 0)
            //    this.LightFlashCount = 0;

            //if (this.LightFlashCount != 3)
            //    return;
            //this.LightFlashCount = 0;
            //if (timeMilliseconds - this.LastRaceChallengeTime <= 20000L)
            //    return;
            //Task.Run(new Action(this.ChallengeNearbyCar));
            //this.LastRaceChallengeTime = timeMilliseconds;

        }

        public void ChallengeCar(EntryCarForStreetRace car)
        {
            Log.Debug("Car has been challenged. Is in race? " + (CurrentRace == null ? "No" : "Yes"));
            if (CurrentRace == null) return;
            CurrentRace.JoinRace(car);
        }

        private void ChallengeNearbyCar()
        {
            Log.Debug("Challenging nearby car.");
            EntryCarForStreetRace? car = null;            

            foreach (KeyValuePair<int,EntryCarForStreetRace> entryCar in _groupStreetRacing.EntryCars)
            {
                if (entryCar.Value.EntryCar.Client != null && entryCar.Value.EntryCar != EntryCar)
                {
                    float num1 = (float)(Math.Atan2((double)EntryCar.Status.Position.X - (double)entryCar.Value.EntryCar.Status.Position.X, (double)EntryCar.Status.Position.Z - (double)entryCar.Value.EntryCar.Status.Position.Z) * 180.0 / Math.PI);
                    if ((double)num1 < 0.0)
                        num1 += 360f;
                    float rotationAngle = entryCar.Value.EntryCar.Status.GetRotationAngle();
                    float num2 = (num1 + rotationAngle) % 360f;
                    if ((double)num2 > 110.0 && (double)num2 < 250.0 && (double)Vector3.DistanceSquared(entryCar.Value.EntryCar.Status.Position, EntryCar.Status.Position) < 900.0)
                        car = entryCar.Value;
                }
            }
            if (car == null)
                return;
            car.ChallengeCar(this);
        }
    }
}
