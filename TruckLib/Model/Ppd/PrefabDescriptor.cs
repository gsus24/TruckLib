﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace TruckLib.Model.Ppd
{
    /// <summary>
    /// Represents a prefab descriptor (.ppd) file.
    /// </summary>
    /// <remarks>This class supports ppd versions 21, 22 and 23.</remarks>
    public class PrefabDescriptor : IBinarySerializable
    {
        public List<ControlNode> Nodes { get; set; } = new();

        public List<NavCurve> NavCurves { get; set; } = new ();

        public List<Sign> Signs { get; set; } = new();

        public List<Semaphore> Semaphores { get; set; } = new();

        public List<SpawnPoint> SpawnPoints { get; set; } = new();

        public List<Vector3> TerrainPointPositions { get; set; } = new();

        public List<Vector3> TerrainPointNormals { get; set; } = new();

        public List<TerrainPointVariant> TerrainPointVariants { get; set; } = new();

        public List<MapPoint> MapPoints { get; set; } = new();

        public List<TriggerPoint> TriggerPoints { get; set; } = new();

        public List<Intersection> Intersections { get; set; } = new();

        public List<uint[]> Unknown { get; set; } = new();


        /// <summary>
        /// Reads a .ppd file from disk.
        /// </summary>
        /// <param name="path">The path to the file.</param>
        /// <returns>The prefab descriptor.</returns>
        /// <exception cref="NotSupportedException">Thrown if the descriptor version
        /// is not supported.</exception>
        public static PrefabDescriptor Open(string path)
        {
            var ppd = new PrefabDescriptor();
            using (var r = new BinaryReader(new FileStream(path, FileMode.Open)))
            {
                ppd.Deserialize(r);
            }
            return ppd;
        }

        /// <summary>
        /// Reads a .ppd file from memory.
        /// </summary>
        /// <param name="file">The file as byte array.</param>
        /// <returns>The prefab descriptor.</returns>
        /// <exception cref="NotSupportedException">Thrown if the descriptor version
        /// is not supported.</exception>
        public static PrefabDescriptor Load(byte[] file)
        {
            var ppd = new PrefabDescriptor();
            using (var r = new BinaryReader(new MemoryStream(file)))
            {
                ppd.Deserialize(r);
            }
            return ppd;
        }

        /// <inheritdoc/>
        /// <exception cref="NotSupportedException">Thrown if the descriptor version
        /// is not supported.</exception>
        public void Deserialize(BinaryReader r, uint? version = null)
        {
            version = r.ReadUInt32();
            switch (version)
            {
                case 0x15:
                    Deserialize15(r);
                    break;
                case 0x16:
                    Deserialize16(r);
                    break;
                case 0x17:
                    Deserialize17(r);
                    break;
                default:
                    throw new NotSupportedException($"Version {version} is not supported.");
            }
        }

        private void Deserialize15(BinaryReader r)
        {
            const int version = 0x15;
            var nodeCount = r.ReadUInt32();
            var navCurveCount = r.ReadUInt32();
            var signCount = r.ReadUInt32();
            var semaphoreCount = r.ReadUInt32();
            var spawnPointCount = r.ReadUInt32();
            var terrainPointCount = r.ReadUInt32();
            var terrainPointVariantCount = r.ReadUInt32();
            var mapPointCount = r.ReadUInt32();
            var triggerPointCount = r.ReadUInt32();
            var intersectionCount = r.ReadUInt32();

            // offsets; we can probably ignore this
            for (int i = 0; i < 11; i++)
                r.ReadUInt32();

            Nodes = r.ReadObjectList<ControlNode>(nodeCount);
            NavCurves = r.ReadObjectList<NavCurve>(navCurveCount, version);
            Signs = r.ReadObjectList<Sign>(signCount);
            Semaphores = r.ReadObjectList<Semaphore>(semaphoreCount);
            SpawnPoints = r.ReadObjectList<SpawnPoint>(spawnPointCount);
            TerrainPointPositions = r.ReadObjectList<Vector3>(terrainPointCount);
            TerrainPointNormals = r.ReadObjectList<Vector3>(terrainPointCount);
            TerrainPointVariants = r.ReadObjectList<TerrainPointVariant>(terrainPointVariantCount);
            MapPoints = r.ReadObjectList<MapPoint>(mapPointCount);
            TriggerPoints = r.ReadObjectList<TriggerPoint>(triggerPointCount);
            Intersections = r.ReadObjectList<Intersection>(intersectionCount);
        }

        private void Deserialize16(BinaryReader r)
        {
            const int version = 0x16;
            var nodeCount = r.ReadUInt32();
            var navCurveCount = r.ReadUInt32();
            var signCount = r.ReadUInt32();
            var semaphoreCount = r.ReadUInt32();
            var spawnPointCount = r.ReadUInt32();
            var terrainPointCount = r.ReadUInt32();
            var terrainPointVariantCount = r.ReadUInt32();
            var mapPointCount = r.ReadUInt32();
            var triggerPointCount = r.ReadUInt32();
            var intersectionCount = r.ReadUInt32();
            var newdata1Count = r.ReadUInt32();

            // offsets; we can probably ignore this
            for (int i = 0; i < 12; i++)
                r.ReadUInt32();

            Nodes = r.ReadObjectList<ControlNode>(nodeCount);
            NavCurves = r.ReadObjectList<NavCurve>(navCurveCount, version);
            Signs = r.ReadObjectList<Sign>(signCount);
            Semaphores = r.ReadObjectList<Semaphore>(semaphoreCount);
            SpawnPoints = r.ReadObjectList<SpawnPoint>(spawnPointCount);
            TerrainPointPositions = r.ReadObjectList<Vector3>(terrainPointCount);
            TerrainPointNormals = r.ReadObjectList<Vector3>(terrainPointCount);
            TerrainPointVariants = r.ReadObjectList<TerrainPointVariant>(terrainPointVariantCount);
            MapPoints = r.ReadObjectList<MapPoint>(mapPointCount);
            TriggerPoints = r.ReadObjectList<TriggerPoint>(triggerPointCount);
            Intersections = r.ReadObjectList<Intersection>(intersectionCount);

            // TODO: What is this?
            for (int i = 0; i < newdata1Count; i++)
            {
                var newdata = new uint[24];
                for (int j = 0; j < newdata.Length; j++)
                {
                    newdata[j] = r.ReadUInt32();
                }
                Unknown.Add(newdata);
            }
        }

        private void Deserialize17(BinaryReader r)
        {
            const int version = 0x17;
            var nodeCount = r.ReadUInt32();
            var navCurveCount = r.ReadUInt32();
            var signCount = r.ReadUInt32();
            var semaphoreCount = r.ReadUInt32();
            var spawnPointCount = r.ReadUInt32();
            var terrainPointCount = r.ReadUInt32();
            var terrainPointVariantCount = r.ReadUInt32();
            var mapPointCount = r.ReadUInt32();
            var triggerPointCount = r.ReadUInt32();
            var intersectionCount = r.ReadUInt32();
            var newdata1Count = r.ReadUInt32();

            // offsets; we can probably ignore this
            for (int i = 0; i < 12; i++)
                r.ReadUInt32();

            Nodes = r.ReadObjectList<ControlNode>(nodeCount);
            NavCurves = r.ReadObjectList<NavCurve>(navCurveCount, version);
            Signs = r.ReadObjectList<Sign>(signCount);
            Semaphores = r.ReadObjectList<Semaphore>(semaphoreCount);
            SpawnPoints = r.ReadObjectList<SpawnPoint>(spawnPointCount);
            TerrainPointPositions = r.ReadObjectList<Vector3>(terrainPointCount);
            TerrainPointNormals = r.ReadObjectList<Vector3>(terrainPointCount);
            TerrainPointVariants = r.ReadObjectList<TerrainPointVariant>(terrainPointVariantCount);
            MapPoints = r.ReadObjectList<MapPoint>(mapPointCount);
            TriggerPoints = r.ReadObjectList<TriggerPoint>(triggerPointCount);
            Intersections = r.ReadObjectList<Intersection>(intersectionCount);

            // TODO: What is this?
            for (int i = 0; i < newdata1Count; i++)
            {
                var newdata = new uint[47];
                for (int j = 0; j < newdata.Length; j++)
                {
                    newdata[j] = r.ReadUInt32();
                }
                Unknown.Add(newdata);
            }
        }

        public void Serialize(BinaryWriter w)
        {
            throw new NotImplementedException();
        }
    }
}
