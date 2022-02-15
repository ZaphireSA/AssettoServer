﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using Serilog;
using YamlDotNet.Serialization;

namespace AssettoServer.Server.Ai
{
    public class FastLaneParser
    {
        private readonly ACServer _server;

        public FastLaneParser(ACServer server)
        {
            _server = server;
        }

        public TrafficMap FromFiles(string folder)
        {
            Dictionary<string, TrafficSpline> splines = new();
            TrafficConfiguration? configuration = null;

            int idOffset = 0;
            // List of files should be ordered to guarantee consistent IDs for junctions etc.

            var aipPath = Path.Join(folder, "fast_lane.aip");
            if (File.Exists(aipPath))
            {
                Log.Information("Loading from AI package {Path}", aipPath);
                
                using var aipFile = ZipFile.OpenRead(aipPath);
                
                foreach (var entry in aipFile.Entries)
                {
                    if (entry.Name == "config.yml")
                    {
                        using var fileStream = entry.Open();
                        using var reader = new StreamReader(fileStream);
                        
                        var deserializer = new DeserializerBuilder().Build();
                        configuration = deserializer.Deserialize<TrafficConfiguration>(reader);
                    }
                    else if(entry.Name.EndsWith(".ai"))
                    {
                        using var fileStream = entry.Open();
                        var spline = FromFile(fileStream, entry.Name, idOffset);
                        splines.Add(entry.Name, spline);
                        
                        Log.Information("Parsed {Path}, id range {MinId} - {MaxId}, min. speed {MinSpeed} km/h", entry, idOffset, idOffset + spline.Points.Length - 1, MathF.Round(spline.MinCorneringSpeed * 3.6f));
                        idOffset += spline.Points.Length;
                    }
                }
            }
            else
            {
                foreach (string file in Directory.EnumerateFiles(folder, "fast_lane*.ai").OrderBy(f => f))
                {
                    string filename = Path.GetFileName(file);
                    
                    using var fileStream = File.OpenRead(file);
                    var spline = FromFile(fileStream, filename, idOffset);
                    splines.Add(filename, spline);

                    Log.Information("Parsed {Path}, id range {MinId} - {MaxId}, min. speed {MinSpeed} km/h", file, idOffset, idOffset + spline.Points.Length - 1,
                        MathF.Round(spline.MinCorneringSpeed * 3.6f));
                    idOffset += spline.Points.Length;
                }

                string configPath = Path.Join(folder, "config.yml");
                if (File.Exists(configPath))
                {
                    using var file = File.OpenText(configPath);
                    var deserializer = new DeserializerBuilder().Build();
                    configuration = deserializer.Deserialize<TrafficConfiguration>(file);
                }
                
            }

            if (splines.Count == 0) 
                throw new InvalidOperationException($"No AI splines found. Please put at least one AI spline (fast_lane.ai) into {Path.GetFullPath(folder)}");

            return new TrafficMap(splines, _server.Configuration.Extra.AiParams.LaneWidthMeters, configuration);
        }

        private TrafficSplinePoint[] FromFileV7(BinaryReader reader, int idOffset)
        {
            int detailCount = reader.ReadInt32();
            reader.ReadInt32(); // LapTime
            reader.ReadInt32(); // SampleCount

            TrafficSplinePoint[] points = new TrafficSplinePoint[detailCount];

            for (var i = 0; i < detailCount; i++)
            {
                var p = new TrafficSplinePoint
                {
                    Id = idOffset + i,
                    Point = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle())
                };

                reader.ReadSingle(); // Length
                reader.ReadInt32(); // ID

                points[i] = p;
            }

            int extraCount = reader.ReadInt32();
            if (extraCount != detailCount)
            {
                throw new ArgumentException("Count of spline points does not match extra spline points");
            }

            for (var i = 0; i < detailCount; i++)
            {
                /*points[i].Speed*/ _ = reader.ReadSingle();
                /*points[i].Gas*/ _ = reader.ReadSingle();
                /*points[i].Brake*/ _ = reader.ReadSingle();
                /*points[i].ObsoleteLatG*/ _ = reader.ReadSingle();
                points[i].Radius = reader.ReadSingle();
                /*points[i].SideLeft*/ _ = reader.ReadSingle();
                /*points[i].SideRight*/ _ = reader.ReadSingle();
                points[i].Camber = reader.ReadSingle() /* camber */ * reader.ReadSingle() /* direction, either 1 or -1 */;
                /*points[i].Normal*/ _ = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                points[i].Length = reader.ReadSingle();
                /*points[i].ForwardVector*/ _ = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                /*points[i].Tag*/ _ = reader.ReadSingle();
                /*points[i].Grade*/ _ = reader.ReadSingle();
            }

            return points;
        }
        
        private TrafficSplinePoint[] FromFileVn1(BinaryReader reader, int idOffset)
        {
            int detailCount = reader.ReadInt32();

            TrafficSplinePoint[] points = new TrafficSplinePoint[detailCount];

            for (var i = 0; i < detailCount; i++)
            {
                points[i] = new TrafficSplinePoint
                {
                    Id = idOffset + i,
                    Point = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
                    Radius = reader.ReadSingle(),
                    Camber = reader.ReadSingle()
                };
            }

            return points;
        }

        private TrafficSpline FromFile(Stream file, string name, int idOffset = 0)
        {
            Log.Debug("Loading AI spline {Path}", name);
            using var reader = new BinaryReader(file);

            float minCorneringSpeed = float.MaxValue;

            int version = reader.ReadInt32();
            TrafficSplinePoint[] points = version switch
            {
                7 => FromFileV7(reader, idOffset),
                -1 => FromFileVn1(reader, idOffset),
                _ => throw new InvalidOperationException($"Unknown spline version {version}")
            };

            for (var i = 0; i < points.Length; i++)
            {
                // For point-to-point splines the last point might be completely off
                if (i == points.Length - 1 && points[i].Radius < 1)
                {
                    Log.Debug("Resetting radius of last spline point");
                    points[i].Radius = 1000;
                }
                
                points[i].MaxCorneringSpeed = PhysicsUtils.CalculateMaxCorneringSpeed(points[i].Radius) * _server.Configuration.Extra.AiParams.CorneringSpeedFactor;
                minCorneringSpeed = Math.Min(minCorneringSpeed, points[i].MaxCorneringSpeed);
                
                points[i].Previous = points[i == 0 ? points.Length - 1 : i - 1];
                points[i].Next = points[i == points.Length - 1 ? 0 : i + 1];

                points[i].Length = Vector3.Distance(points[i].Point, points[i].Next!.Point);
            }

            bool closedLoop = Vector3.Distance(points[0].Point, points[^1].Point) < 50;
            if (!closedLoop)
            {
                points[0].Previous = null;
                points[^1].Next = null;
                points[^1].Length = 1;
                Log.Debug("Distance between spline start and end too big, not closing loop");
            }

            /*using (var writer = new StreamWriter(Path.GetFileName(filename) + ".csv"))
            using (var csv = new CsvWriter(writer, new CultureInfo("de-DE", false)))
            {
                csv.WriteRecords(points);
            }*/

            return new TrafficSpline
            {
                Name = name,
                Points = points,
                MinCorneringSpeed = minCorneringSpeed
            };
        }
    }
}