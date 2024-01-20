﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using TruckLib.Model;

namespace TruckLib.ScsMap
{
    /// <summary>
    /// A compound item, which groups multiple aux items into one, with an additional parent node
    /// to which they are tethered.
    /// </summary>
    /// <remarks>
    /// <para>Both the items and the nodes of compounded items are contained within
    /// its compound parent rather than belonging to the sector itself.</para>
    /// <para>The editor does not allow signs to be added to compounds, but signs without
    /// traffic rules can be added externally and the game will load them without issues.
    /// Likewise, the editor requires a compound to consist of at least two items,
    /// but a compound of only one item is supported (albeit somewhat pointless) if
    /// created externally.</para>
    /// </remarks>
    public class Compound : SingleNodeItem, IItemContainer
    {
        /// <inheritdoc/>
        public override ItemType ItemType => ItemType.Compound;

        /// <inheritdoc/>
        public override ItemFile DefaultItemFile => ItemFile.Aux;

        /// <inheritdoc/>
        protected override ushort DefaultViewDistance => KdopItem.ViewDistanceClose;

        /// <summary>
        /// Gets or sets the view distance of the item in meters.
        /// </summary>
        public new ushort ViewDistance
        {
            get => base.ViewDistance;
            set => base.ViewDistance = value;
        }

        /// <summary>
        /// Contains all map items owned by this compound.
        /// </summary>
        public List<MapItem> Items { get; set; } 


        /// <summary>
        /// Contains all nodes owned by this compound.
        /// </summary>
        public List<INode> Nodes { get; set; }

        /// <summary>
        /// Gets or sets if the items are reflected on water surfaces.
        /// </summary>
        public bool WaterReflection
        {
            get => Kdop.Flags[0];
            set => Kdop.Flags[0] = value;
        }

        /// <summary>
        /// Gets or sets if the compounded items will render behind a cut plane.
        /// </summary>
        public bool IgnoreCutPlanes
        {
            get => Kdop.Flags[1];
            set => Kdop.Flags[1] = value;
        }

        public bool Collision
        {
            get => !Kdop.Flags[4];
            set => Kdop.Flags[4] = !value;
        }

        public bool Shadows
        {
            get => !Kdop.Flags[5];
            set => Kdop.Flags[5] = !value;
        }

        public bool MirrorReflection
        {
            get => !Kdop.Flags[6];
            set => Kdop.Flags[6] = !value;
        }

        public Compound() : base() { }

        internal Compound(bool initFields) : base(initFields)
        {
            if (initFields) Init();
        }

        /// <inheritdoc/>
        protected override void Init()
        {
            base.Init();
            Items = new List<MapItem>();
            Nodes = new List<INode>();
        }

        /// <summary>
        /// Adds a new, empty compound container to the map.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="position">The position of the parent node.</param>
        /// <returns>The newly created compound.</returns>
        public static Compound Add(IItemContainer map, Vector3 position)
        {
            return Add<Compound>(map, position);
        }

        /// <summary>
        /// Creates a new node and adds it to the compound.
        /// </summary>
        /// <param name="position">The position of the new node.</param>
        /// <param name="isRed">Whether the node is a red node.</param>
        /// <returns>The newly created node.</returns>
        public Node AddNode(Vector3 position, bool isRed)
        {
            var node = new Node
            {
                Sectors = null,
                Position = position,
                IsRed = isRed
            };
            Nodes.Add(node);
            return node;
        }

        /// <summary>
        /// Creates a new node and adds it to the compound.
        /// </summary>
        /// <param name="position">The position of the new node.</param>
        /// <returns>The newly created node.</returns>
        public Node AddNode(Vector3 position)
        {
            return AddNode(position, false);
        }

        /// <summary>
        /// Adds an item to the compound. This is the final step in the Add() method of an item
        /// and should not be called on its own.
        /// </summary>
        /// <param name="item">The item.</param>
        void IItemContainer.AddItem(MapItem item)
        {
            if (item.ItemFile != ItemFile.Aux)
            {
                // The game will crash without logging an error message if you try to do that.
                throw new InvalidOperationException("A compound can only contain .aux items.");
            }
            Items.Add(item);
        }

        /// <summary>
        /// Returns a dictionary containing all map items in this compound.
        /// </summary>
        /// <returns>All map items in this compound.</returns>
        public Dictionary<ulong, MapItem> GetAllItems()
        {
            return Items.ToDictionary(k => k.Uid, v => v);
        }

        /// <summary>
        /// Returns an <see cref="IEnumerable{T}"/> containing all items of type T in this compound.
        /// </summary>
        /// <typeparam name="T">The item type.</typeparam>
        /// <returns>All items of type T in this compound.</returns>
        public IEnumerable<T> GetAllItems<T>() where T : MapItem
        {
            return Items.Where(x => x is T).Cast<T>();
        }

        /// <summary>
        /// Returns a dictionary containing all items of type T in this compound.
        /// </summary>
        /// <typeparam name="T">The item type.</typeparam>
        /// <returns>All items of type T in this compound.</returns>
        Dictionary<ulong, T> IItemContainer.GetAllItems<T>()
        {
            return GetAllItems<T>().ToDictionary(k => k.Uid, v => v);
        }

        /// <summary>
        /// Returns a dictionary containing all nodes in this compound.
        /// </summary>
        /// <returns>All nodes in this compound.</returns>
        public Dictionary<ulong, INode> GetAllNodes()
        {
            return Nodes.ToDictionary(k => k.Uid, v => v);
        }

        /// <summary>
        /// Deletes an item. Nodes that are only used by this item 
        /// will also be deleted.
        /// </summary>
        /// <param name="item">The item to delete.</param>
        public void Delete(MapItem item)
        {
            // delete item from compound
            if (Items.Contains(item))
            {
                Items.Remove(item);
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
        }

        /// <summary>
        /// Deletes a node and the items attached to it.
        /// </summary>
        /// <param name="node">The node to delete.</param>
        public void Delete(INode node)
        {
            if (Nodes.Contains(node))
            {
                Nodes.Remove(node);
            }

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

        internal void UpdateInternalReferences()
        {
            var itemsDict = GetAllItems();
            var nodesDict = GetAllNodes();

            // first of all, find map items referenced in nodes
            foreach (var node in Nodes)
            {
                node.UpdateItemReferences(itemsDict);
            }

            // then find nodes referenced in map items
            // and map items referenced in map items
            foreach (var item in Items)
            {
                item.UpdateNodeReferences(nodesDict);
                if (item is IItemReferences hasItemRef)
                {
                    hasItemRef.UpdateItemReferences(itemsDict);
                }
            }
        }
    }  
}
