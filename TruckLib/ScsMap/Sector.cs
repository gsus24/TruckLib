﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using TruckLib.ScsMap.Serialization;

namespace TruckLib.ScsMap
{
    /// <summary>
    /// A map sector.
    /// </summary>
    public class Sector
    {
        /// <summary>
        /// The X coordinate of this sector. 
        /// </summary>
        public int X { get; set; }

        /// <summary>
        /// The Z coordinate of this sector.
        /// </summary>
        public int Z { get; set; }

        /// <summary>
        /// The map the sector belongs to.
        /// </summary>
        public Map Map { get; set; }

        /// <summary>
        /// The path of the .base file.
        /// </summary>
        internal string BasePath { get; set; }

        /// <summary>
        /// A list of all map items in this sector.
        /// </summary>
        public Dictionary<ulong, MapItem> MapItems { get; set; }
            = new Dictionary<ulong, MapItem>();

        /// <summary>
        /// The climate of this sector.
        /// </summary>
        public Token Climate { get; set; } = "default";

        /// <summary>
        /// The header of this sector.
        /// </summary>
        private Header header = new();

        // Always 2 in both ETS2 and ATS.
        private uint SectorDescVersion = 2;

        public Vector2 MinBoundary { get; set; } = new Vector2(0, 0);

        public Vector2 MaxBoundary { get; set; } = new Vector2(4000, 4000);

        internal FlagField Flags = new();

        /// <summary>
        /// EOF marker of .data and .layer files.
        /// </summary>
        private const ulong EofMarker = ulong.MaxValue;

        private const string BaseExtension = "base";
        private const string DataExtension = "data";
        private const string SndExtension = "snd";
        private const string AuxExtenstion = "aux";
        private const string DescExtension = "desc";
        private const string LayerExtension = "layer";

        public Sector() { }

        /// <summary></summary>
        /// <param name="x">The X coordinate.</param>
        /// <param name="z">The Z coordinate.</param>
        /// <param name="map">The map this sector belongs to.</param>
        public Sector(int x, int z, Map map)
        {
            X = x;
            Z = z;
            Map = map;
        }

        /// <summary></summary>
        /// <param name="basePath">The path of the .base file of this sector.</param>
        /// <param name="map">The map this sector belongs to.</param>
        public Sector(string basePath, Map map)
        {
            BasePath = basePath;
            GetSectorCoordsFromBasePath(basePath);
            Map = map;
        }

        /// <summary>
        /// Reads the sector from the specified .base path.
        /// </summary>
        public void Read() => Open(BasePath);

        /// <summary>
        /// Reads the sector.
        /// </summary>
        /// <param name="basePath">The .base file of the sector.</param>
        public void Open(string basePath)
        {
            BasePath = basePath;

            GetSectorCoordsFromBasePath(basePath);

            ReadBase(basePath);
            ReadData(Path.ChangeExtension(basePath, DataExtension));
            ReadAux(Path.ChangeExtension(basePath, AuxExtenstion));
            ReadSnd(Path.ChangeExtension(basePath, SndExtension));
            ReadDesc(Path.ChangeExtension(basePath, DescExtension));
            ReadLayer(Path.ChangeExtension(basePath, LayerExtension));
        }

        /// <summary>
        /// Reads the .base file of the sector.
        /// </summary>
        /// <param name="path">The .base file of the sector.</param>
        private void ReadBase(string path)
        {
            using var r = new BinaryReader(new MemoryStream(File.ReadAllBytes(path)));
            header = new Header();
            header.Deserialize(r);
            ReadItems(r, ItemFile.Base);
            ReadNodes(r);
            ReadVisArea(r);
        }

        /// <summary>
        /// Reads the .aux file of the sector.
        /// </summary>
        /// <param name="path">The .aux file of the sector.</param>
        private void ReadAux(string path)
        {
            using var r = new BinaryReader(new MemoryStream(File.ReadAllBytes(path)));
            var header = new Header();
            header.Deserialize(r);
            ReadItems(r, ItemFile.Aux);
            ReadNodes(r);
            ReadVisArea(r);
        }

        /// <summary>
        /// Reads the .data file of the sector.
        /// </summary>
        /// <param name="path">The .data file of the sector.</param>
        private void ReadData(string path)
        {
            using var r = new BinaryReader(new MemoryStream(File.ReadAllBytes(path)));

            // Header
            var header = new Header();
            header.Deserialize(r);

            // Items
            while (r.BaseStream.Position < r.BaseStream.Length)
            {
                var uid = r.ReadUInt64();
                if (uid == EofMarker)
                    break;

                if (!MapItems.TryGetValue(uid, out MapItem item))
                {
                    throw new KeyNotFoundException($"{ToString()}.{DataExtension} contains " +
                        $"unknown UID {uid} - can't continue.");
                }
                var serializer = (IDataPayload)MapItemSerializerFactory.Get(item.ItemType);
                serializer.DeserializeDataPayload(r, item);
            }
        }

        /// <summary>
        /// Reads the .snd file of the sector.
        /// </summary>
        /// <param name="path">The .snd file of the sector.</param>
        private void ReadSnd(string path)
        {
            if (!File.Exists(path))
                return;

            using var r = new BinaryReader(new MemoryStream(File.ReadAllBytes(path)));
            var header = new Header();
            header.Deserialize(r);
            ReadItems(r, ItemFile.Snd);
            ReadNodes(r);
            ReadVisArea(r);
        }

        private const float boundaryFactor = 256f;

        /// <summary>
        /// Reads the .desc file of the sector.
        /// </summary>
        /// <param name="path">The .desc file of the sector.</param>
        private void ReadDesc(string path)
        {
            // TODO: 
            // - figure out if there are any desc flags (ets2 & ats 
            //   base maps don't have any)
            // - figure out what exactly the boundaries are and how
            //   they work, because they seem to relate to items
            //   at the borders of the sector

            using var r = new BinaryReader(new MemoryStream(File.ReadAllBytes(path)));

            SectorDescVersion = r.ReadUInt32();

            MinBoundary = new Vector2(
                r.ReadInt32() / boundaryFactor,
                r.ReadInt32() / boundaryFactor
                );
            MaxBoundary = new Vector2(
                r.ReadInt32() / boundaryFactor,
                r.ReadInt32() / boundaryFactor
                );

            Flags = new FlagField(r.ReadUInt32());

            Climate = r.ReadToken();
        }

        private void ReadLayer(string path)
        {
            if (!File.Exists(path))
                return;

            using var r = new BinaryReader(new MemoryStream(File.ReadAllBytes(path)));

            var header = new Header();
            header.Deserialize(r);

            while (r.BaseStream.Position < r.BaseStream.Length)
            {
                var uid = r.ReadUInt64();
                if (uid == EofMarker)
                    break;

                if (!MapItems.TryGetValue(uid, out MapItem item))
                {
                    throw new KeyNotFoundException($"{ToString()}.{LayerExtension} contains " +
                        $"unknown UID {uid} - can't continue.");
                }
                var layer = r.ReadByte();
                item.Layer = layer;
            }
        }

        /// <summary>
        /// Reads items from a .base/.aux/.snd file.
        /// </summary>
        /// <param name="r">The reader.</param>
        /// <param name="file">The file which is being read.</param>
        private void ReadItems(BinaryReader r, ItemFile file)
        {
            var itemCount = r.ReadUInt32();
            MapItems.EnsureCapacity(MapItems.Count + (int)itemCount);
            for (int i = 0; i < itemCount; i++)
            {
                var itemType = (ItemType)r.ReadInt32();

                var serializer = MapItemSerializerFactory.Get(itemType);
                var item = serializer.Deserialize(r);

                // deal with signs which can be in aux *and* base
                if (item is Sign sign && file != sign.DefaultItemFile)
                {
                    sign.ItemFile = file;
                }
                else if (item.DefaultItemFile != file)
                {
                    Trace.WriteLine($"{itemType} {item.Uid} in {file}?");
                }

                MapItems.Add(item.Uid, item);
            }
        }

        /// <summary>
        /// Reads the node section of a .base/.aux/.snd file.
        /// </summary>
        /// <param name="r">The reader. (Position must be the start of the footer)</param>
        private void ReadNodes(BinaryReader r)
        {
            var nodeCount = r.ReadUInt32();
            for (int i = 0; i < nodeCount; i++)
            {
                var node = new Node(false);
                node.Deserialize(r);
                if (Map is not null && Map.Nodes.TryGetValue(node.Uid, out var existingNode))
                {
                    existingNode.Sectors = existingNode.Sectors.Push(this);
                }
                else
                {
                    node.Sectors = new[] { this };
                    Map?.Nodes.Add(node.Uid, node);
                }
            }
        }

        private void ReadVisArea(BinaryReader r)
        {
            // I think we can safely ignore this when deserializing
            var visAreaChildCount = r.ReadUInt32();
            for (int i = 0; i < visAreaChildCount; i++)
            {
                r.ReadUInt64();
            }
        }

        /// <summary>
        /// Saves the sector as binary files to the specified directory.
        /// </summary>
        /// <param name="sectorDirectory">The sector directory.</param>
        public void Save(string sectorDirectory, List<INode> sectorNodes, HashSet<ulong> visAreaShowObjectsChildren)
        {
            WriteBase(GetFilename(BaseExtension), MapItems, sectorNodes, visAreaShowObjectsChildren);
            WriteData(GetFilename(DataExtension), MapItems);
            WriteAux(GetFilename(AuxExtenstion), MapItems, sectorNodes, visAreaShowObjectsChildren);
            WriteSnd(GetFilename(SndExtension), MapItems, sectorNodes, visAreaShowObjectsChildren);
            WriteDesc(GetFilename(DescExtension));
            WriteLayer(GetFilename(LayerExtension));

            string GetFilename(string ext) => 
                Path.Combine(sectorDirectory, $"{ToString()}.{ext}");
        }

        /// <summary>
        /// Writes the .base part of this sector.
        /// </summary>
        /// <param name="path">The path of the output file.</param>
        /// <param name="allItems">A list of all items in the sector.</param>
        /// <param name="sectorNodes">A list of all nodes in the sector.</param>
        private void WriteBase(string path, Dictionary<ulong, MapItem> allItems,
            List<INode> sectorNodes, HashSet<ulong> visAreaShowObjectsChildren)
        {
            using var stream = new FileStream(path, FileMode.Create);
            using var w = new BinaryWriter(stream);
            header.Serialize(w);
            WriteItems(allItems, ItemFile.Base, w);
            WriteNodes(w, ItemFile.Base, sectorNodes);
            WriteVisAreaChildren(w, ItemFile.Base, visAreaShowObjectsChildren);
        }

        /// <summary>
        /// Writes the .aux part of the sector.
        /// </summary>
        /// <param name="path">The path of the output file.</param>
        /// <param name="allItems">A list of all items in the sector.</param>
        /// <param name="sectorNodes">A list of all nodes in the sector.</param>
        private void WriteAux(string path, Dictionary<ulong, MapItem> allItems,
            List<INode> sectorNodes, HashSet<ulong> visAreaShowObjectsChildren)
        {
            using var stream = new FileStream(path, FileMode.Create);
            using var w = new BinaryWriter(stream);
            header.Serialize(w);
            WriteItems(allItems, ItemFile.Aux, w);
            WriteNodes(w, ItemFile.Aux, sectorNodes);
            WriteVisAreaChildren(w, ItemFile.Aux, visAreaShowObjectsChildren);
        }

        /// <summary>
        /// Writes the .snd part of the sector.
        /// </summary>
        /// <param name="path">The path of the output file.</param>
        /// <param name="allItems">A list of all items in the sector.</param>
        /// <param name="sectorNodes">A list of all nodes in the sector.</param>
        private void WriteSnd(string path, Dictionary<ulong, MapItem> allItems,
            List<INode> sectorNodes, HashSet<ulong> visAreaShowObjectsChildren)
        {
            using var stream = new FileStream(path, FileMode.Create);
            using var w = new BinaryWriter(stream);
            header.Serialize(w);
            WriteItems(allItems, ItemFile.Snd, w);
            WriteNodes(w, ItemFile.Snd, sectorNodes);
            WriteVisAreaChildren(w, ItemFile.Snd, visAreaShowObjectsChildren);
        }

        /// <summary>
        /// Writes the .data part of this sector.
        /// </summary>
        /// <param name="path">The path of the output file.</param>
        /// <param name="allItems">A list of all items in the sector.</param>
        private void WriteData(string path, Dictionary<ulong, MapItem> allItems)
        {
            using var stream = new FileStream(path, FileMode.Create);
            using var w = new BinaryWriter(stream);

            header.Serialize(w);

            foreach (var itemKvp in allItems.Where(x => x.Value.HasDataPayload))
            {
                var item = itemKvp.Value;
                w.Write(item.Uid);
                var serializer = (IDataPayload)MapItemSerializerFactory.Get(item.ItemType);
                serializer.SerializeDataPayload(w, item);
            }

            w.Write(EofMarker);
        }

        /// <summary>
        /// Writes the .desc part of the sector.
        /// </summary>
        /// <param name="path">The path of the output file.</param>
        private void WriteDesc(string path)
        {
            using var stream = new FileStream(path, FileMode.Create);
            using var w = new BinaryWriter(stream);

            w.Write(SectorDescVersion);
            w.Write((int)(MinBoundary.X * boundaryFactor));
            w.Write((int)(MinBoundary.Y * boundaryFactor));
            w.Write((int)(MaxBoundary.X * boundaryFactor));
            w.Write((int)(MaxBoundary.Y * boundaryFactor));
            w.Write(Flags.Bits);
            w.Write(Climate);
        }

        /// <summary>
        /// Writes the .layer part of the sector.
        /// </summary>
        /// <param name="path">The path of the output file.</param>
        private void WriteLayer(string path)
        {
            var itemsWithSetLayers = MapItems.Where(x => x.Value.Layer != MapItem.DefaultLayer);
            if (!itemsWithSetLayers.Any())
                return;

            using var stream = new FileStream(path, FileMode.Create);
            using var w = new BinaryWriter(stream);

            header.Serialize(w);
            foreach (var item in itemsWithSetLayers)
            {
                w.Write(item.Value.Uid);
                w.Write(item.Value.Layer);
            }
            w.Write(EofMarker);
        }

        /// <summary>
        /// Writes the node part of a .base/.aux/.snd file.
        /// </summary>
        /// <param name="w">The writer.</param>
        private void WriteNodes(BinaryWriter w, ItemFile file, List<INode> sectorNodes)
        {
            // get base nodes only || get aux nodes only
            var nodes = new List<INode>(32);
            foreach (var node in sectorNodes)
            {
                if (node.ForwardItem is not UnresolvedItem
                    && node.ForwardItem is MapItem fwItem
                    && fwItem.ItemFile == file)
                {
                    nodes.Add(node);
                }
                else if (node.BackwardItem is not UnresolvedItem
                    && node.BackwardItem is MapItem bwItem
                    && bwItem.ItemFile == file)
                {
                    nodes.Add(node);
                }
            }

            w.Write(nodes.Count);
            foreach (var node in nodes)
            {
                node.Serialize(w);
            }
        }

        /// <summary>
        /// Writes base/aux/snd items to a base/aux/snd file.
        /// </summary>
        /// <param name="allItems">All items in the sector.</param>
        /// <param name="w">The writer.</param>
        private void WriteItems(Dictionary<ulong, MapItem> allItems, ItemFile file, BinaryWriter w)
        {
            var items = allItems.Where(x => x.Value.ItemFile == file);

            w.Write(items.Count());
            foreach (var item in items)
            {
                w.Write((int)item.Value.ItemType);
                var serializer = MapItemSerializerFactory.Get(item.Value.ItemType);
                serializer.Serialize(w, item.Value);
            }
        }

        private void WriteVisAreaChildren(BinaryWriter w, ItemFile file, HashSet<ulong> visAreaShowObjectsChildren)
        {
            if (visAreaShowObjectsChildren.Count == 0)
            {
                w.Write(0L);
                return;
            }

            var uids = new List<ulong>();
            foreach (var childUid in visAreaShowObjectsChildren)
            {
                if (MapItems.TryGetValue(childUid, out var child) && child.ItemFile == file)
                {
                    uids.Add(childUid);
                }
            }
            uids.Sort();

            w.Write(uids.Count);
            foreach (var childUid in uids)
            {
                w.Write(childUid);
            }
        }

        internal void GetSectorCoordsFromBasePath(string basePath)
        {
            var sectorName = Path.GetFileNameWithoutExtension(basePath);
            X = int.Parse(sectorName.Substring(3, 5));
            Z = int.Parse(sectorName.Substring(8, 5));
        }

        /// <summary>
        /// Returns the name of this sector as used in filenames and the editor's map overlay.
        /// </summary>
        /// <returns>The name of this sector.</returns>
        public override string ToString() => 
            $"sec{X:+0000;-0000;+0000}{Z:+0000;-0000;+0000}";

    }
}
