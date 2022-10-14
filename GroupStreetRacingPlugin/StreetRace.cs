using AssettoServer.Server;
using GroupStreetRacingPlugin.Packets;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.ConstrainedExecution;
using System.Text;
using System.Threading.Tasks;

namespace GroupStreetRacingPlugin
{
    public class StreetRace
    {
        //private readonly EntryCarManager _entryCarManager;
        private readonly SessionManager _sessionManager;
        private StreetRaceStatus _raceStatus;
        private EntryCarForStreetRace _raceOwner;
        private List<StreetRaceCar> _raceCars;
        private readonly GroupStreetRacingConfiguration _configuration;

        public bool HasStarted { get; private set; }
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        public StreetRace(EntryCarForStreetRace raceOwner, SessionManager sessionManager, GroupStreetRacingConfiguration configuration)
        {
            _sessionManager = sessionManager;
            _configuration = configuration;
            _raceOwner = raceOwner;
            _raceCars = new List<StreetRaceCar>();
            _raceStatus = StreetRaceStatus.Challenging;
            _raceCars.Add(new StreetRaceCar(raceOwner));
            UpdateClientsWithRaceDetails();
        }
        public void Cancel()
        {
            this._cancellationTokenSource.Cancel();

            foreach (var car in _raceCars)
            {
                car.Car.CurrentRace = null;
                if (car?.Car?.EntryCar?.Client != null)
                    car.Car.EntryCar.Client.Collision -= Client_Collision;
            }
            ClearRaceDetailsForCars(StreetRaceStatus.Cancelled);
        }

        public Task StartAsync()
        {
            if (HasStarted)
                return Task.CompletedTask;

            HasStarted = true;            
            Task.Run(new Func<Task>(RaceAsync));
            return Task.CompletedTask;
        }

        private async Task RaceAsync()
        {
            foreach(var car in _raceCars)
            {
                if (car.Car.EntryCar.Client != null && car.Car.EntryCar.Client.HasSentFirstUpdate)
                    car.Car.EntryCar.Client.Collision += Client_Collision;
            }
            _raceStatus = StreetRaceStatus.Countdown;

            SendEventUpdateToAll(RaceUpdateEvent.CountdownStart, _sessionManager.CurrentSession.SessionTimeMilliseconds + 5000);            
            await Task.Delay(5000);

            _raceStatus = StreetRaceStatus.InProgress;

            while (!this._cancellationTokenSource.IsCancellationRequested && _raceStatus == StreetRaceStatus.InProgress)
            {
                //Update race positions
                _raceCars = _raceCars.OrderBy(x => x, new StreetCarPositionComparer()).ToList();

                for(var i = 0; i < _raceCars.Count; i++)
                {
                    _raceCars[i].PositionInRace = i + 1;
                    if (i == 0)
                    {
                        _raceCars[i].DistanceFromRaceLeader = 0;
                    } else
                    {
                        float distanceFromLeader = Vector3.DistanceSquared(_raceCars[0].Car.EntryCar.Status.Position, _raceCars[i].Car.EntryCar.Status.Position);
                        _raceCars[i].DistanceFromRaceLeader = distanceFromLeader;

                        if (distanceFromLeader >= _configuration.RaceEliminationDistance)
                        {
                            if (_raceCars[i]?.Car?.EntryCar?.Client != null)
                            {
                                _raceCars[i].Status = RacerStatus.Eliminated;
                                ClearRaceDetailsForCar(_raceCars[i].Car.EntryCar, StreetRaceStatus.Ended);
                                SendEventUpdateToAll(RaceUpdateEvent.PlayerLeftBehind, _raceCars[i].Car.EntryCar.Client.SessionId);                             
                            }
                        }
                    }
                        
                }

                //Send race detail to clients
                UpdateClientsWithRaceDetails();

                var totalCarsLeftInRace = _raceCars.Count(x => x.Status != RacerStatus.Eliminated && x.Status != RacerStatus.Crashed);
                if (totalCarsLeftInRace <= 1) Cancel();

                await Task.Delay(500);
            }
        }

        public void SendEventUpdateToCar(RaceUpdateEvent challengeEvent, Int32 eventData, EntryCar car)
        {
            var packet = new GroupStreetRacingUpdateEventPacket(challengeEvent, eventData);            

            var client = car.Client;
            if (client == null || !client.HasSentFirstUpdate)
                return;

            client?.SendPacket(packet);
        }

        public void SendEventUpdateToAll(RaceUpdateEvent challengeEvent, Int32 eventData)
        {
            var packet = new GroupStreetRacingUpdateEventPacket(challengeEvent, eventData);

            foreach (var car in _raceCars)
            {                
                var client = car.Car.EntryCar.Client;
                if (client == null || !client.HasSentFirstUpdate)
                    continue;

                client?.SendPacket(packet);
            }
        }

        public void UpdateClientsWithRaceDetails()
        {
            byte[] sessionIds = new byte[GroupStreetRacingCurrentRacePacket.Length];
            byte[] healthOfCars = new byte[GroupStreetRacingCurrentRacePacket.Length];
            byte[] racersStatus = new byte[GroupStreetRacingCurrentRacePacket.Length];            
            Array.Fill(sessionIds, (byte)255);
            Array.Fill(healthOfCars, (byte)255);
            Array.Fill(racersStatus, (byte)RacerStatus.None);

            for (var s = 0; s < _raceCars.Count; s++)
            {
                sessionIds[s] = _raceCars[s].Car.EntryCar.SessionId;
                healthOfCars[s] = (byte)_raceCars[s].Health;
                racersStatus[s] = (byte)_raceCars[s].Status;
            }

            var packet = new GroupStreetRacingCurrentRacePacket(sessionIds, healthOfCars, racersStatus, _raceStatus);

            foreach(var car in _raceCars)
            {
                if (car.Status == RacerStatus.Eliminated || car.Status == RacerStatus.Crashed) continue;

                var client = car.Car.EntryCar.Client;
                if (client == null || !client.HasSentFirstUpdate)
                    continue;

                client?.SendPacket(packet);
            }
        }

        public void ClearRaceDetailsForCar(EntryCar car, StreetRaceStatus status)
        {
            byte[] sessionIds = new byte[GroupStreetRacingCurrentRacePacket.Length];
            byte[] healthOfCars = new byte[GroupStreetRacingCurrentRacePacket.Length];
            byte[] racersStatus = new byte[GroupStreetRacingCurrentRacePacket.Length];
            Array.Fill(sessionIds, (byte)255);
            Array.Fill(healthOfCars, (byte)255);
            Array.Fill(racersStatus, (byte)RacerStatus.None);

            var packet = new GroupStreetRacingCurrentRacePacket(sessionIds, healthOfCars, racersStatus, status);

            var client = car.Client;
            if (client == null || !client.HasSentFirstUpdate)
                return;

            client?.SendPacket(packet);
        }

        public void ClearRaceDetailsForCars(StreetRaceStatus status)
        {
            byte[] sessionIds = new byte[GroupStreetRacingCurrentRacePacket.Length];
            byte[] healthOfCars = new byte[GroupStreetRacingCurrentRacePacket.Length];
            byte[] racersStatus = new byte[GroupStreetRacingCurrentRacePacket.Length];
            Array.Fill(sessionIds, (byte)255);
            Array.Fill(healthOfCars, (byte)255);
            Array.Fill(racersStatus, (byte)RacerStatus.None);

            var packet = new GroupStreetRacingCurrentRacePacket(sessionIds, healthOfCars, racersStatus, status);


            foreach (var car in _raceCars)
            {                
                var client = car.Car.EntryCar.Client;
                if (client == null || !client.HasSentFirstUpdate)
                    continue;

                client?.SendPacket(packet);
            }
        }

        private void Client_Collision(AssettoServer.Network.Tcp.ACTcpClient sender, CollisionEventArgs args)
        {
            var car = _raceCars.FirstOrDefault(x => x.Car.EntryCar.SessionId == sender.SessionId);
            if (car?.Car?.EntryCar?.Client != null)
            {
                car.Health = Math.Clamp(car.Health - (int)(args.Speed / 3), 0, 100);
                Log.Debug("Health Before: " + car.Health + ", after: " + car.Health + ", car: " + car.Car.EntryCar.SessionId);
                if (car.Health <= 0) { 
                    car.Status = RacerStatus.Crashed;
                    ClearRaceDetailsForCar(car.Car.EntryCar, StreetRaceStatus.Ended);
                    SendEventUpdateToAll(RaceUpdateEvent.PlayerCrashed, car.Car.EntryCar.Client.SessionId);
                }
                UpdateClientsWithRaceDetails();
            }
            

            //if (sender.SessionId == EntryCar.SessionId)
            //{
            //    var newHealth = CarHealth - (int)args.Speed;
            //    Log.Debug("Health Before: " + CarHealth + ", after: " + newHealth);
            //    CarHealth = Math.Clamp(newHealth, 0, 100);
            //    if (IsHazardsOn)
            //        _groupStreetRacing.UpdateHazardList();
            //}

            //EventType = (byte)(args.TargetCar == null ? ClientEventType.CollisionWithEnv : ClientEventType.CollisionWithCar),
            //SessionId = sender.SessionId,
            //TargetSessionId = args.TargetCar?.SessionId,
            //Speed = args.Speed,
            //WorldPosition = args.Position,
            //RelPosition = args.RelPosition,
        }

        private async Task FinishRace()
        {
            try
            {
                foreach (var car in _raceCars)
                {      
                    if (car?.Car?.EntryCar?.Client != null)
                        car.Car.EntryCar.Client.Collision -= Client_Collision;
                }

                ClearRaceDetailsForCars(StreetRaceStatus.Ended);
                SendEventUpdateToAll(RaceUpdateEvent.RaceEnded, -1);
            }
            catch(Exception ex)
            {
                Log.Error(ex, "Error finishing race");
            }
            finally
            {
                _cancellationTokenSource.Dispose();
            }
        }

        public void CarToggleHazards(EntryCarForStreetRace car, bool isOn)
        {
            if (car == _raceOwner && !isOn)
            {
                if (_raceCars.Count == 1)
                {
                    Cancel();
                } else if (_raceCars.Count > 1)
                {
                    StartAsync();
                }
            }
        }

        public bool JoinRace(EntryCarForStreetRace car)
        {
            if (_raceCars.Any(x => x.Car.EntryCar == car.EntryCar)) return false;
            if (_raceStatus != StreetRaceStatus.Challenging) return false;
            _raceCars.Add(new StreetRaceCar(car));
            car.CurrentRace = this;
            UpdateClientsWithRaceDetails();
            return true;
        }

        private bool IsInFront(EntryCar car1, EntryCar car2)
        {
            float num1 = (float)(Math.Atan2((double)car1.Status.Position.X - (double)car2.Status.Position.X, (double)car1.Status.Position.Z - (double)car2.Status.Position.Z) * 180.0 / Math.PI);
            if ((double)num1 < 0.0)
                num1 += 360f;
            float rotationAngle1 = car2.Status.GetRotationAngle();
            float num2 = (num1 + rotationAngle1) % 360f;
            float num3 = (float)(Math.Atan2((double)car2.Status.Position.X - (double)car1.Status.Position.X, (double)car2.Status.Position.Z - (double)car1.Status.Position.Z) * 180.0 / Math.PI);
            if ((double)num3 < 0.0)
                num3 += 360f;
            float rotationAngle2 = car1.Status.GetRotationAngle();
            float num4 = (num3 + rotationAngle2) % 360f;
            float num5 = (float)Math.Max(0.07716061728, (double)car2.Status.Velocity.LengthSquared());
            float num6 = (float)Math.Max(0.07716061728, (double)car1.Status.Velocity.LengthSquared());
            float num7 = Vector3.DistanceSquared(car2.Status.Position, car1.Status.Position);
            //EntryCar leader = car1;
            if ((double)num2 > 90.0 && (double)num2 < 275.0 && (double)num5 > (double)num6 && (double)num7 < 2500.0)
            {
                return false;
                //Car2 in front
                //this.Leader = this.Challenger;
                //this.Follower = this.Challenged;
            }
            else if ((double)num4 > 90.0 && (double)num4 < 275.0 && (double)num6 > (double)num5 && (double)num7 < 2500.0)
            {
                //Car1 in front
                return true;
                //this.Leader = this.Challenged;
                //this.Follower = this.Challenger;
            }

            return true;        
        }

        
    }

    public class StreetCarPositionComparer : IComparer<StreetRaceCar>
    {
        public int Compare(StreetRaceCar? x, StreetRaceCar? y)
        {
            if (x == null || y == null) return 0;
            return WhichCarInFront(x.Car.EntryCar, y.Car.EntryCar);
        }

        private int WhichCarInFront(EntryCar car1, EntryCar car2)
        {
            float num1 = (float)(Math.Atan2((double)car1.Status.Position.X - (double)car2.Status.Position.X, (double)car1.Status.Position.Z - (double)car2.Status.Position.Z) * 180.0 / Math.PI);
            if ((double)num1 < 0.0)
                num1 += 360f;
            float rotationAngle1 = car2.Status.GetRotationAngle();
            float num2 = (num1 + rotationAngle1) % 360f;
            float num3 = (float)(Math.Atan2((double)car2.Status.Position.X - (double)car1.Status.Position.X, (double)car2.Status.Position.Z - (double)car1.Status.Position.Z) * 180.0 / Math.PI);
            if ((double)num3 < 0.0)
                num3 += 360f;
            float rotationAngle2 = car1.Status.GetRotationAngle();
            float num4 = (num3 + rotationAngle2) % 360f;
            float num5 = (float)Math.Max(0.07716061728, (double)car2.Status.Velocity.LengthSquared());
            float num6 = (float)Math.Max(0.07716061728, (double)car1.Status.Velocity.LengthSquared());
            float num7 = Vector3.DistanceSquared(car2.Status.Position, car1.Status.Position);
            //EntryCar leader = car1;
            if ((double)num2 > 90.0 && (double)num2 < 275.0 && (double)num5 > (double)num6 && (double)num7 < 2500.0)
            {
                return 1;
                //Car2 in front                
            }
            else if ((double)num4 > 90.0 && (double)num4 < 275.0 && (double)num6 > (double)num5 && (double)num7 < 2500.0)
            {
                //Car1 in front
                return -1;
            }

            return 0;
        }

        
    }

    public class StreetRaceCar
    {
        public EntryCarForStreetRace Car;
        public int Health;
        public int PositionInRace;
        public RacerStatus Status;
        public float DistanceFromRaceLeader;

        public StreetRaceCar(EntryCarForStreetRace car)
        {
            Car = car;
            Health = 100;
            PositionInRace = 0;
            Status = RacerStatus.Ready;
            DistanceFromRaceLeader = 0;
        }

        
    }

    public enum StreetRaceStatus : byte
    {
        Challenging = 1,
        Starting = 2,
        InProgress = 3,
        Ended = 4,
        Cancelled = 5,
        Countdown = 6
    }

    public enum RacerStatus : byte
    {
        None = 0,
        NotReady = 1,
        Ready = 2,
        Racing = 3,
        Eliminated = 4,
        Crashed = 5
    }

    public enum RaceUpdateEvent : byte
    {
        None = 0,
        RacePlayerJoined = 1,
        RacePlayerLeft = 2,
        RaceEnded = 3,
        PlayerCrashed = 4,
        PlayerLeftBehind = 5,
        CountdownStart = 6
    }
}
