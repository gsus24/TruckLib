﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace TruckLib.ScsMap
{
    /// <summary>
    /// Draws a polygon onto the UI map.
    /// </summary>
    public class MapArea : PolygonItem
    {
        /// <inheritdoc/>
        public override ItemType ItemType => ItemType.MapArea;

        /// <inheritdoc/>
        public override ItemFile DefaultItemFile => ItemFile.Base;

        /// <inheritdoc/>
        protected override ushort DefaultViewDistance => KdopItem.ViewDistanceClose;

        /// <summary>
        /// The map area type.
        /// </summary>
        public MapAreaType Type
        {
            get => Kdop.Flags[3] ? MapAreaType.Navigation : MapAreaType.Visual;
            set => Kdop.Flags[3] = (Type == MapAreaType.Navigation);
        }

        /// <summary>
        /// The color of the map area.
        /// </summary>
        public MapAreaColor Color { get; set; }

        public byte DlcGuard
        {
            get => Kdop.Flags.GetByte(1);
            set => Kdop.Flags.SetByte(1, value);
        }

        /// <summary>
        /// TODO: What does this even do?
        /// </summary>
        public bool DrawOutline
        {
            get => Kdop.Flags[1];
            set => Kdop.Flags[1] = value;
        }

        /// <summary>
        /// Gets or sets if this map area is drawn on top of other items.
        /// </summary>
        public bool DrawOver
        {
            get => Kdop.Flags[0];
            set => Kdop.Flags[0] = value;
        }

        /// <summary>
        /// Gets or sets if this area is only visible in the UI map once discovered.
        /// </summary>
        public bool Secret
        {
            get => Kdop.Flags[4];
            set => Kdop.Flags[4] = value;
        }

        public MapArea() : base() { }

        internal MapArea(bool initFields) : base(initFields)
        {
            if (initFields) Init();
        }

        /// <inheritdoc/>
        protected override void Init()
        {
            base.Init();
            Color = MapAreaColor.Road;
        }

        /// <summary>
        /// Adds a map area to the map.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="nodePositions">The points of the polygon.</param>
        /// <param name="type">The map area type.</param>
        /// <returns>The newly created map area.</returns>
        public static MapArea Add(IItemContainer map, IList<Vector3> nodePositions, MapAreaType type)
        {
            var ma = Add<MapArea>(map, nodePositions);
            ma.Type = type;
            return ma;
        }
    }
}
