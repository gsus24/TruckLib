﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using TruckLib.ScsMap.Serialization;

namespace TruckLib.ScsMap
{
    /// <summary>
    /// A map for Euro Truck Simulator 2 or American Truck Simulator.
    /// </summary>
    public class Map : IItemContainer
    {
        private string name;
        /// <summary>
        /// The name of the map, which is used for file and directory names.
        /// </summary>
        public string Name
        {
            get => name;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new ArgumentNullException(nameof(Name),
                        "The map name must not be null or just whitespace.");
                }
                if (value.IndexOfAny(Path.GetInvalidFileNameChars()) != -1)
                {
                    throw new ArgumentException("The map name must not contain characters which are " +
                        "not allowed in filenames", nameof(Name));
                }
                name = value;
            }
        }

        /// <summary>
        /// Metadata of the map's sectors.
        /// </summary>
        public Dictionary<(int X, int Z), Sector> Sectors { get; set; } 
            = new Dictionary<(int X, int Z), Sector>();

        /// <summary>
        /// Contains the map's nodes.
        /// </summary>
        public Dictionary<ulong, INode> Nodes { get; internal set; } = new();

        /// <summary>
        /// Contains the map's items.
        /// </summary>
        public Dictionary<ulong, MapItem> MapItems { get; internal set; } = new();

        /// <summary>
        /// Scale and time compression of the game outside cities.
        /// </summary>
        public float NormalScale { get; set; } = 19;

        /// <summary>
        /// Scale and time compression of the game inside cities.
        /// </summary>
        public float CityScale { get; set; } = 3;

        /// <summary>
        /// Editor start position. TODO: Figure out these values
        /// </summary>
        private Vector3 StartPlacementPosition = new(0, 0, 0);

        /// <summary>
        /// Editor start position. TODO: Figure out these values
        /// </summary>
        private uint StartPlacementSectorOrSomething = 0x4B000800;

        /// <summary>
        /// Editor start rotation.
        /// </summary>
        private Quaternion StartPlacementRotation = Quaternion.Identity;

        /// <summary>
        /// <para>Gets or sets if SCS's Europe map UI corrections are enabled.</para>
        /// <para>Nobody seems to know definitively what this does, but it might have
        /// something to do with the scale of the UK in <c>europe.mbd.</c></para>
        /// </summary>
        public bool EuropeMapUiCorrections { get; set; } = false;

        public ulong EditorMapId { get; set; }

        // This value is used in both ETS2 and ATS.
        private uint gameTag = 2998976734; //TODO: What is this?

        /// <summary>
        /// The map's header.
        /// </summary>
        private Header header = new();

        /// <summary>
        /// The size of a sector in engine units (= meters).
        /// </summary>
        public static readonly int SectorSize = 4000;

        /// <summary>
        /// EOF marker of .data and .layer files.
        /// </summary>
        internal const ulong EofMarker = ulong.MaxValue;

        /// <summary>
        /// Creates an empty map.
        /// </summary>
        /// <param name="name">The name of the map.</param>
        public Map(string name)
        {
            Name = name;
            EditorMapId = Utils.GenerateUuid();
        }

        /// <summary>
        /// Opens a map.
        /// </summary>
        /// <param name="mbdPath">Path to the .mbd file of the map.</param>
        /// <param name="sectors">If set, only the given sectors will be loaded.</param>
        /// <returns>A Map object.</returns>
        public static Map Open(string mbdPath, (int,int)[] sectors = null)
        {
            Trace.WriteLine("Loading map " + mbdPath);
            var name = Path.GetFileNameWithoutExtension(mbdPath);
            var mapDirectory = Directory.GetParent(mbdPath).FullName;
            var sectorDirectory = Path.Combine(mapDirectory, name);

            var map = new Map(name);
            map.ReadMbd(mbdPath);
            Trace.WriteLine("Parsing sectors");
            map.ReadSectors(sectorDirectory, sectors);

            Trace.WriteLine("Updating references");
            map.UpdateReferences();

            return map;
        }

        /// <summary>
        /// Creates a new sector.
        /// </summary>
        /// <param name="x">The X coordinate of the sector.</param>
        /// <param name="z">The Z coordinate of the sector.</param>
        /// <returns>The new sector.</returns>
        public Sector AddSector(int x, int z)
        {
            if (Sectors.TryGetValue((x, z), out var existing))
                return existing;
            
             var sector = new Sector(x, z, this);
             Sectors.Add((x, z), sector);
             return sector;
        }

        /// <summary>
        /// Creates a new sector.
        /// </summary>
        /// <param name="x">The X coordinate of the sector.</param>
        /// <param name="z">The Z coordinate of the sector.</param>
        /// <returns>The new sector.</returns>
        public Sector AddSector((int X, int Z) coords)
        {
            return AddSector(coords.X, coords.Z);
        }

        /// <summary>
        /// Creates a new node.
        /// </summary>
        /// <param name="position">The position of the node.</param>
        /// <returns>The new node.</returns>
        public Node AddNode(Vector3 position)
        {
            return AddNode(position, false);
        }

        /// <summary>
        /// Creates a new node. 
        /// </summary>
        /// <param name="position">The position of the node.</param>
        /// <param name="isRed">Whether the node is red.</param>
        /// <returns>The new node.</returns>
        public Node AddNode(Vector3 position, bool isRed)
        {
            var sectorIdx = GetSectorOfCoordinate(position);
            if (!Sectors.ContainsKey(sectorIdx))
            {
                AddSector(sectorIdx.X, sectorIdx.Z);
            }

            var node = new Node
            {
                Position = position,
                IsRed = isRed,
                Parent = this,
            };
            Nodes.Add(node.Uid, node);
            return node;
        }

        /// <summary>
        /// Creates a new node.
        /// </summary>
        /// <param name="position">The position of the node.</param>
        /// <param name="isRed">Whether the node is red.</param>
        /// <param name="forwardItem">The forward item which the node will be assigned to.</param>
        /// <returns>The new node.</returns>
        public Node AddNode(Vector3 position, bool isRed, MapItem forwardItem)
        {
            var node = AddNode(position, isRed);
            node.ForwardItem = forwardItem;
            return node;
        }

        /// <summary>
        /// Adds an item to the map. This is the final step in the Add() method of an item
        /// and should not be called on its own.
        /// </summary>
        /// <param name="item">The item.</param>
        void IItemContainer.AddItem(MapItem item)
        {
            AddSector(GetSectorOfCoordinate(item.GetCenter()));
            MapItems.Add(item.Uid, item);
        }

        /// <summary>
        /// Returns the index of the sector the given coordinate falls into.
        /// </summary>
        /// <param name="c">The coordinate to check.</param>
        /// <returns>The index of the sector the coordinate is in.</returns>
        public static (int X, int Z) GetSectorOfCoordinate(Vector3 c)
        {
            return (
                (int)Math.Floor(c.X / SectorSize), 
                (int)Math.Floor(c.Z / SectorSize)
                );
        }

        /// <summary>
        /// Deletes an item. Nodes that are only used by this item will also be deleted.
        /// </summary>
        /// <param name="item">The item to delete.</param>
        public void Delete(MapItem item)
        {
            MapItems.Remove(item.Uid);

            List<IRecalculatable> recalculatables = null;
            List<INode> prefabNodes = null;
            if (item is PolylineItem poly)
            {
                recalculatables = new();
                prefabNodes = new();
                if (poly.BackwardItem is IRecalculatable)
                    recalculatables.Add((IRecalculatable)poly.BackwardItem);
                // if the backward item is a prefab, the rotation of that node
                // needs to be reset
                else if (poly.BackwardItem is Prefab)
                    prefabNodes.Add(poly.Node);
                if (poly.ForwardItem is IRecalculatable)
                    recalculatables.Add((IRecalculatable)poly.ForwardItem);
            }

            // remove item from its nodes, 
            // and delete them if they're orphaned now
            foreach (var node in item.GetItemNodes())
            {
                if (node.ForwardItem == item)
                {
                    node.ForwardItem = null;
                    node.IsRed = false;
                }
                if (node.BackwardItem == item)
                {
                    node.BackwardItem = null;
                }
                if (node.IsOrphaned())
                {
                    Delete(node);
                }
            }

            for (int i = 0; i < recalculatables?.Count; i++)
                recalculatables[i].Recalculate();

            for (int i = 0; i < prefabNodes?.Count; i++)
                prefabNodes[i].Rotation *= Quaternion.CreateFromYawPitchRoll((float)Math.PI, 0, 0);

            // delete dependent items
            if (item is Prefab pf)
            {
                foreach (var slaveItem in pf.SlaveItems)
                {
                    if (slaveItem is MapItem mapItem)
                        Delete(mapItem);
                    // if Unresolved, just ignore it
                }
            }
        }

        /// <summary>
        /// Deletes a node and the items attached to it.
        /// </summary>
        /// <param name="node">The node to delete.</param>
        public void Delete(INode node)
        {
            Nodes.Remove(node.Uid);
            
            if (node.ForwardItem is MapItem fw)
            {
                node.ForwardItem = null;
                Delete(fw);
            }

            if (node.BackwardItem is MapItem bw)
            {
                node.BackwardItem = null;
                Delete(bw);
            }
        }

        /// <summary>
        /// Imports the contents of a Selection (.sbd) file into this map.
        /// </summary>
        /// <param name="selection">The Selection to import.</param>
        /// <param name="position">The point relative to which the items will be inserted.</param>
        public void Import(Selection selection, Vector3 position)
        {
            // deep cloning everything the lazy way

            var clonedItems = selection.MapItems.Select(x => (x.Key, x.Value.CloneItem()))
                .ToDictionary(k => k.Item1, v => v.Item2);
            var clonedNodes = selection.Nodes.Select(x => (x.Key, (x.Value as Node).Clone()))
                .ToDictionary(k => k.Item1, v => (INode)(v.Item2));

            foreach (var (_, node) in clonedNodes)
            {
                node.Position += position - selection.Origin;
                node.UpdateItemReferences(clonedItems);
                var (X, Z) = GetSectorOfCoordinate(node.Position);
                AddSector(X, Z);
                Nodes.Add(node.Uid, node);
            }

            foreach (var (_, item) in clonedItems)
            {
                item.UpdateNodeReferences(clonedNodes);
                if (item is IItemReferences itemRefs)
                    itemRefs.UpdateItemReferences(clonedItems);
                (this as IItemContainer).AddItem(item);
            }
        }

        /// <summary>
        /// Reads the .mbd file of a map.
        /// </summary>
        /// <param name="mbdPath">The path to the .mbd file.</param>
        private void ReadMbd(string mbdPath)
        {
            using var r = new BinaryReader(new MemoryStream(File.ReadAllBytes(mbdPath)));

            var header = new Header();
            header.Deserialize(r);

            EditorMapId = r.ReadUInt64();

            StartPlacementPosition = r.ReadVector3();
            StartPlacementSectorOrSomething = r.ReadUInt32();

            StartPlacementRotation = r.ReadQuaternion();

            gameTag = r.ReadUInt32();

            NormalScale = r.ReadSingle();
            CityScale = r.ReadSingle();

            EuropeMapUiCorrections = (r.ReadByte() == 1);
        }

        /// <summary>
        /// Reads the sectors of this map.
        /// </summary>
        /// <param name="mapDirectory">The main map directory.</param>
        /// <param name="sectors">If set, only the given sectors will be loaded.</param>
        private void ReadSectors(string mapDirectory, (int X, int Z)[] sectors = null)
        {
            var baseFiles = Directory.GetFiles(mapDirectory, "*.base");

            // create itemless instances for all the sectors first;
            // this is so we have references to the sectors
            // before we read them to deal with sectors containing
            // nodes from other sectors
            foreach (var baseFile in baseFiles)
            {
                var coords = Sector.SectorCoordsFromSectorFilePath(baseFile);
                if (sectors != null && !sectors.Contains(coords))
                    continue;

                var sector = new Sector(coords.X, coords.Z, this);
                sector.BasePath = baseFile;
                Sectors.Add(coords, sector);
            }

            // now read in the sectors
            foreach (var (_, sector) in Sectors)
            {
                Trace.WriteLine($"Reading sector {sector}");
                sector.ReadDesc(Path.ChangeExtension(sector.BasePath, Sector.DescExtension));
                ReadSector(sector.BasePath);
            }
        }

        /// <summary>
        /// Reads the .layer file of the sector.
        /// </summary>
        /// <param name="path">The .layer file of the sector.</param>
        /// <exception cref="KeyNotFoundException"></exception>
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
                    throw new KeyNotFoundException($"{ToString()}.{Sector.LayerExtension} contains " +
                        $"unknown UID {uid} - can't continue.");
                }
                var layer = r.ReadByte();
                item.Layer = layer;
            }
        }

        /// <summary>
        /// Reads the sector from disk.
        /// </summary>
        /// <param name="basePath">The path to the .base file of the sector. The paths of the other
        /// sector files will be derived automatically.</param>
        private void ReadSector(string basePath)
        {
            ReadBase(basePath);
            ReadData(Path.ChangeExtension(basePath, Sector.DataExtension));
            ReadAux(Path.ChangeExtension(basePath, Sector.AuxExtenstion));
            ReadSnd(Path.ChangeExtension(basePath, Sector.SndExtension));
            ReadLayer(Path.ChangeExtension(basePath, Sector.LayerExtension));
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
                    throw new KeyNotFoundException($"{ToString()}.{Sector.DataExtension} contains " +
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

        /// <summary>
        /// Reads items from a .base/.aux/.snd file.
        /// </summary>
        /// <param name="r">A BinaryReader at the start of the item section.</param>
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
        /// <param name="r">A BinaryReader at the start of the node section.</param>
        private void ReadNodes(BinaryReader r)
        {
            var nodeCount = r.ReadUInt32();
            for (int i = 0; i < nodeCount; i++)
            {
                var node = new Node(false);
                node.Deserialize(r);
                node.Parent = this;
                if (!Nodes.ContainsKey(node.Uid))
                {
                    Nodes.Add(node.Uid, node);
                }
            }
        }

        /// <summary>
        /// Reads the VisAreaChild section of a .base/.aux/.snd file.
        /// </summary>
        /// <param name="r">A BinaryReader at the start of the VisAreaChild section.</param>
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
        /// Fills in the Node and Forward/BackwardItem fields in map item and node objects
        /// by searching the Node or Item list for the UID references.
        /// </summary>
        private void UpdateReferences()
        {
            // first of all, find map items referenced in nodes
            Trace.WriteLine("Updating item references in nodes");
            foreach (var (_, node) in Nodes)
            {
                node.UpdateItemReferences(MapItems);
            }

            // then find nodes referenced in map items
            // and map items referenced in map items
            Trace.WriteLine("Updating node & item references in items");
            foreach (var (_, item) in MapItems)
            {
                item.UpdateNodeReferences(Nodes);
                if (item is IItemReferences hasItemRef)
                {
                    hasItemRef.UpdateItemReferences(MapItems);
                }
            }
        }

        /// <summary>
        /// Saves the map in binary format. If the sector directory does not yet exist, it will be created.
        /// </summary>
        /// <param name="mapDirectory">The path of the directory to save the map into.</param>
        /// <param name="cleanSectorDirectory">If true, the sector directory will be emptied
        /// before saving the map.</param>
        public void Save(string mapDirectory, bool cleanSectorDirectory = true)
        {
            var sectorDirectory = Path.Combine(mapDirectory, Name);
            Directory.CreateDirectory(sectorDirectory);
            if (cleanSectorDirectory)
            {
                new DirectoryInfo(sectorDirectory).GetFiles().ToList()
                    .ForEach(f => f.Delete());
            }

            var sectorItems = GetSectorItems();
            var sectorNodes = GetSectorNodes(sectorItems);
            foreach (var sectorCoord in sectorNodes.Keys)
            {
                AddSector(sectorCoord);
            }
            var visAreaShowObjectsChildren = GetVisAreaShowObjectsChildUids();

            foreach (var (sectorCoord, sector) in Sectors)
            {
                sectorItems.TryGetValue(sectorCoord, out var theItems);
                theItems ??= new();
                sectorNodes.TryGetValue(sectorCoord, out var theNodes);
                theNodes ??= new();
                if (theItems.Count > 0 || theNodes.Count > 0)
                {
                    Trace.WriteLine($"Writing sector {sector}");
                    sector.WriteDesc(GetSectorFilename(sectorCoord, sectorDirectory, Sector.DescExtension));
                    SaveSector(sectorCoord, sectorDirectory, theItems, theNodes, visAreaShowObjectsChildren);
                }
            }

            var mbdPath = Path.Combine(mapDirectory, $"{Name}.mbd");
            SaveMbd(mbdPath);

            HashSet<ulong> GetVisAreaShowObjectsChildUids()
            {
                var children = new HashSet<ulong>();
                foreach (var visArea in MapItems.OfType<VisibilityArea>()
                    .Where(x => x.Behavior == VisibilityAreaBehavior.ShowObjects))
                {
                    foreach (var child in visArea.Children)
                    {
                        children.Add(child.Uid);
                    }
                }
                return children;
            }
        }

        internal Dictionary<(int X, int Z), List<MapItem>> GetSectorItems()
        {
            var items = new Dictionary<(int X, int Z), List<MapItem>>();
            foreach (var (_, item) in MapItems)
            {
                var center = item.GetCenter();
                var sectorCoord = GetSectorOfCoordinate(center);
                if (items.TryGetValue(sectorCoord, out var list))
                {
                    list.Add(item);
                } 
                else
                {
                    items.Add(sectorCoord, new List<MapItem>() { item });
                }
            }
            return items;
        }

        internal Dictionary<(int X, int Z), List<INode>> GetSectorNodes(Dictionary<(int X, int Z), List<MapItem>> items)
        {
            var nodes = new Dictionary<(int X, int Z), List<INode>>();
            foreach (var (_, node) in Nodes)
            {
                if (node.ForwardItem is null && node.BackwardItem is null)
                    continue;
                if (node.ForwardItem is UnresolvedItem && node.BackwardItem is null)
                    continue;
                if (node.ForwardItem is null && node.BackwardItem is UnresolvedItem)
                    continue;
                if (node.ForwardItem is UnresolvedItem && node.BackwardItem is UnresolvedItem)
                    continue;

                // this may blow up in my face one day
                if (node.BackwardItem is UnresolvedItem)
                    node.BackwardItem = null;
                if (node.ForwardItem is UnresolvedItem)
                    node.ForwardItem = null;

                var sectorCoord = GetSectorOfCoordinate(node.Position);
                if (nodes.TryGetValue(sectorCoord, out var list))
                {
                    list.Add(node);
                }
                else
                {
                    nodes.Add(sectorCoord, new List<INode>() { node });
                }
            }
            return nodes;
        }

        /// <summary>
        /// Writes the .mbd file of this map.
        /// </summary>
        /// <param name="mbdPath">The path of the .mbd file.</param>
        private void SaveMbd(string mbdPath)
        {
            var stream = new FileStream(mbdPath, FileMode.Create);
            using var w = new BinaryWriter(stream);

            header.Serialize(w);

            w.Write(EditorMapId);

            w.Write(StartPlacementPosition);
            w.Write(StartPlacementSectorOrSomething);

            w.Write(StartPlacementRotation);

            w.Write(gameTag);

            w.Write(NormalScale);
            w.Write(CityScale);

            w.Write(EuropeMapUiCorrections.ToByte());
        }

        /// <summary>
        /// Saves the sector in binary format to the specified directory.
        /// </summary>
        /// <param name="sectorCoords">The coordinates of the sector to write.</param>
        /// <param name="sectorDirectory">The sector directory.</param>
        /// <param name="sectorItems">A list of all items in this sector.</param>
        /// <param name="sectorNodes">A list of all nodes in this sector.</param>
        /// <param name="visAreaShowObjectsChildren">UIDs of VisAreaChildren for this sector.</param>
        public void SaveSector((int X, int Z) sectorCoords, string sectorDirectory, 
            List<MapItem> sectorItems, List<INode> sectorNodes,
            HashSet<ulong> visAreaShowObjectsChildren)
        {
            WriteBase(GetSectorFilename(sectorCoords, sectorDirectory, Sector.BaseExtension), 
                sectorItems, sectorNodes, visAreaShowObjectsChildren);
            WriteData(GetSectorFilename(sectorCoords, sectorDirectory, Sector.DataExtension), 
                sectorItems);
            WriteAux(GetSectorFilename(sectorCoords, sectorDirectory, Sector.AuxExtenstion), 
                sectorItems, sectorNodes, visAreaShowObjectsChildren);
            WriteSnd(GetSectorFilename(sectorCoords, sectorDirectory, Sector.SndExtension), 
                sectorItems, sectorNodes, visAreaShowObjectsChildren);
            WriteLayer(GetSectorFilename(sectorCoords, sectorDirectory, Sector.LayerExtension),
                sectorItems);
        }
        private string GetSectorFilename((int X, int Z) sectorCoords, string sectorDirectory, 
            string ext) =>
            Path.Combine(sectorDirectory, $"{Sector.SectorFileNameFromSectorCoords(sectorCoords)}.{ext}");

        /// <summary>
        /// Writes the .base part of this sector.
        /// </summary>
        /// <param name="path">The path of the output file.</param>
        /// <param name="sectorItems">A list of all items in this sector.</param>
        /// <param name="sectorNodes">A list of all nodes in this sector.</param>
        /// <param name="visAreaShowObjectsChildren">UIDs of VisAreaChildren for this sector.</param>
        private void WriteBase(string path, List<MapItem> sectorItems,
            List<INode> sectorNodes, HashSet<ulong> visAreaShowObjectsChildren)
        {
            using var stream = new FileStream(path, FileMode.Create);
            using var w = new BinaryWriter(stream);
            header.Serialize(w);
            WriteItems(w, ItemFile.Base, sectorItems);
            WriteNodes(w, ItemFile.Base, sectorNodes);
            WriteVisAreaChildren(w, ItemFile.Base, visAreaShowObjectsChildren);
        }

        /// <summary>
        /// Writes the .aux part of the sector.
        /// </summary>
        /// <param name="path">The path of the output file.</param>
        /// <param name="sectorItems">A list of all items in this sector.</param>
        /// <param name="sectorNodes">A list of all nodes in this sector.</param>
        /// <param name="visAreaShowObjectsChildren">UIDs of VisAreaChildren for this sector.</param>
        private void WriteAux(string path, List<MapItem> sectorItems,
            List<INode> sectorNodes, HashSet<ulong> visAreaShowObjectsChildren)
        {
            using var stream = new FileStream(path, FileMode.Create);
            using var w = new BinaryWriter(stream);
            header.Serialize(w);
            WriteItems(w, ItemFile.Aux, sectorItems);
            WriteNodes(w, ItemFile.Aux, sectorNodes);
            WriteVisAreaChildren(w, ItemFile.Aux, visAreaShowObjectsChildren);
        }

        /// <summary>
        /// Writes the .snd part of the sector.
        /// </summary>
        /// <param name="path">The path of the output file.</param>
        /// <param name="sectorItems">A list of all items in this sector.</param>
        /// <param name="sectorNodes">A list of all nodes in this sector.</param>
        /// <param name="visAreaShowObjectsChildren">UIDs of VisAreaChildren for this sector.</param>
        private void WriteSnd(string path, List<MapItem> sectorItems,
            List<INode> sectorNodes, HashSet<ulong> visAreaShowObjectsChildren)
        {
            using var stream = new FileStream(path, FileMode.Create);
            using var w = new BinaryWriter(stream);
            header.Serialize(w);
            WriteItems(w, ItemFile.Snd, sectorItems);
            WriteNodes(w, ItemFile.Snd, sectorNodes);
            WriteVisAreaChildren(w, ItemFile.Snd, visAreaShowObjectsChildren);
        }

        /// <summary>
        /// Writes the .data part of this sector.
        /// </summary>
        /// <param name="path">The path of the output file.</param>
        /// <param name="sectorItems">A list of all items in this sector.</param>
        private void WriteData(string path, List<MapItem> sectorItems)
        {
            using var stream = new FileStream(path, FileMode.Create);
            using var w = new BinaryWriter(stream);

            header.Serialize(w);

            foreach (var item in sectorItems.Where(x => x.HasDataPayload))
            {
                w.Write(item.Uid);
                var serializer = (IDataPayload)MapItemSerializerFactory.Get(item.ItemType);
                serializer.SerializeDataPayload(w, item);
            }

            w.Write(EofMarker);
        }

        /// <summary>
        /// Writes the .layer part of the sector.
        /// </summary>
        /// <param name="path">The path of the output file.</param>
        /// <param name="sectorItems">A list of all items in this sector.</param>
        private void WriteLayer(string path, List<MapItem> sectorItems)
        {
            var itemsWithSetLayers = sectorItems.Where(x => x.Layer != MapItem.DefaultLayer);
            if (!itemsWithSetLayers.Any())
                return;

            using var stream = new FileStream(path, FileMode.Create);
            using var w = new BinaryWriter(stream);

            header.Serialize(w);
            foreach (var item in itemsWithSetLayers)
            {
                w.Write(item.Uid);
                w.Write(item.Layer);
            }
            w.Write(EofMarker);
        }

        /// <summary>
        /// Writes the node part of a .base/.aux/.snd file.
        /// </summary>
        /// <param name="w">The writer.</param>
        /// <param name="file">The sector file to write.</param>
        /// <param name="sectorNodes">A list of all nodes in this sector.</param>
        private void WriteNodes(BinaryWriter w, ItemFile file, List<INode> sectorNodes)
        {
            // get base nodes only || get aux nodes only
            var nodes = new List<INode>(32);
            foreach (var node in sectorNodes)
            {
                if ((node.ForwardItem is MapItem fw && fw.ItemFile == file)
                    || (node.BackwardItem is MapItem bw && bw.ItemFile == file))
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
        /// Writes .base/.aux/.snd items to a .base/.aux/.snd file.
        /// </summary>
        /// <param name="file">The sector file to write.</param>
        /// <param name="w">The writer.</param>
        /// <param name="sectorItems">A list of all items in this sector.</param>
        private void WriteItems(BinaryWriter w, ItemFile file, List<MapItem> items)
        {
            var fileItems = items.Where(x => x.ItemFile == file);

            w.Write(fileItems.Count());
            foreach (var item in fileItems)
            {
                w.Write((int)item.ItemType);
                var serializer = MapItemSerializerFactory.Get(item.ItemType);
                serializer.Serialize(w, item);
            }
        }

        /// <summary>
        /// Writes the VisAreaChildren part of a .base/.aux/.snd file.
        /// </summary>
        /// <param name="w">A BinaryWriter.</param>
        /// <param name="file">The item file which is being written.</param>
        /// <param name="visAreaShowObjectsChildren">UIDs of the map items which need to be written.</param>
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
    }
}
