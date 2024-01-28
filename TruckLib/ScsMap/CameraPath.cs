﻿using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace TruckLib.ScsMap
{
    /// <summary>
    /// Defines a tracking shot through the map for use in cutscenes.
    /// </summary>
    public class CameraPath : PathItem
    {
        /// <inheritdoc/>
        public override ItemType ItemType => ItemType.CameraPath;

        /// <inheritdoc/>
        public override ItemFile DefaultItemFile => ItemFile.Aux;

        /// <inheritdoc/>
        protected override ushort DefaultViewDistance => KdopItem.ViewDistanceFar;

        /// <summary>
        /// Tags of this item.
        /// </summary>
        public List<Token> Tags { get; set; }

        /// <summary>
        /// <para>Track points the camera will look at. If null or empty, it will interpolate between
        /// the rotations of the nodes instead.</para>
        /// <para>Note that, while the map format itself supports multiple track points, the game
        /// does not. Only the first node in this list will be used, and the rest are ignored.</para>
        /// </summary>
        public List<INode> TrackPoints { get; set; }

        /// <summary>
        /// Control points of the spline. If null or empty, the defaults will be used. If any of them
        /// need to be different, all nodes must be explicitly created, not just the ones you
        /// wish to change.
        /// </summary>
        public List<INode> ControlNodes { get; set; }

        /// <summary>
        /// Keyframe properties; one per node.
        /// </summary>
        public List<Keyframe> Keyframes { get; set; }

        /// <summary>
        /// The main camera speed.
        /// </summary>
        public float CameraSpeed { get; set; }

        public CameraPath() : base() { }

        internal CameraPath(bool initFields) : base(initFields)
        {
            if (initFields) Init();
        }

        /// <inheritdoc/>
        protected override void Init()
        {
            base.Init();
            Tags = new List<Token>();
            TrackPoints = new List<INode>();
            Keyframes = new List<Keyframe>();
            CameraSpeed = 1f;
        }

        /// <inheritdoc/>
        internal override void UpdateNodeReferences(Dictionary<ulong, INode> allNodes)
        {
            base.UpdateNodeReferences(allNodes);
            ResolveNodeReferences(TrackPoints, allNodes);
        }

        protected override void SetNodeRotations()
        {
            return; // Do nothing - camera path nodes are 0y, 0p, 0r by default
        }

        public static CameraPath Add(IItemContainer map, IList<Vector3> positions)
        {
            var path = Add<CameraPath>(map, positions);

            path.Keyframes.EnsureCapacity(positions.Count);
            for (int i = 0; i < positions.Count; i++)
            {
                path.Keyframes.Add(new Keyframe());
            }

            return path;
        }
    }
}
