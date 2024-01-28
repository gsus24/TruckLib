﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;

namespace TruckLib.ScsMap
{
    /// <summary>
    /// Base class for map items which define a path that is fully contained in one item rather than
    /// forming a polyline.
    /// </summary>
    public abstract class PathItem : MultiNodeItem
    {
        protected PathItem() : base() { }

        internal PathItem(bool initFields) : base(initFields)
        {
            if (initFields) Init();
        }

        /// <inheritdoc/>
        protected override void Init()
        {
            base.Init();
            Nodes = new List<INode>(2);
        }

        /// <summary>
        /// Moves the item, where the given position will be the new position
        /// of the node at index 0, and all other nodes will be moved relative to it.
        /// </summary>
        /// <param name="newPos">The new position of the node.</param>
        public override void Move(Vector3 newPos)
        {
            var translation = newPos - Nodes[0].Position;
            Translate(translation);
        }

        /// <summary>
        /// Moves the item, where the given position will be the new position
        /// of the node at index <paramref name="n">n/</paramref>, and all other nodes
        /// will be moved relative to it.
        /// </summary>
        /// <param name="newPos">The new position of the node.</param>
        /// <param name="n">The index of the node which will be moved to this position.</param>
        public void Move(Vector3 newPos, int n)
        {
            if (n < 0 || n >= Nodes.Count)
                throw new ArgumentOutOfRangeException(nameof(n));

            var translation = newPos - Nodes[n].Position;
            Translate(translation);
        }

        /// <inheritdoc/>
        public override void Translate(Vector3 translation)
        {
            foreach (var node in Nodes)
                node.Move(node.Position + translation);
        }

        /// <inheritdoc/>
        protected override void CreateNodes(IItemContainer map, IList<Vector3> nodePositions)
        {
            base.CreateNodes(map, nodePositions);
            SetNodeRotations();
        }

        /// <summary>
        /// Sets the node rotations when creating a path item.
        /// Called by <see cref="CreateNodes">CreateNodes</see>.
        /// </summary>
        protected virtual void SetNodeRotations()
        {
            for (int i = 0; i < Nodes.Count; i++)
            {
                var p0 = Nodes[Math.Max(0, i - 1)].Position;
                var p1 = Nodes[i].Position;
                var p2 = Nodes[Math.Min(Nodes.Count - 1, i + 1)].Position;
                var p3 = Nodes[Math.Min(Nodes.Count - 1, i + 2)].Position;
                var vec = CatmullRomSpline.Derivative(p0, p1, p2, p3, 0);
                var angle = MathEx.GetNodeAngle(vec);
                Nodes[i].Rotation = Quaternion.CreateFromYawPitchRoll((float)angle, 0, 0); ;
            }
        }

        internal override INode GetMainNode() => Nodes[^1];
    }
}