﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace TruckLib.ScsMap
{
    /// <summary>
    /// Reperesents a list of Far Item models.
    /// </summary>
    public class FarModelDataList : IList<FarModelData>
    {
        /// <summary>
        /// The Far Model item which parents these models.
        /// </summary>
        public FarModel Parent { get; init; }

        private readonly List<FarModelData> list = new();

        /// <summary>
        /// Instantiates an empty list.
        /// </summary>
        /// <param name="parent">The Far Model item which parents these models.</param>
        public FarModelDataList(FarModel parent)
        {
            Parent = parent;
        }

        /// <inheritdoc/>
        public FarModelData this[int index] 
        { 
            get => list[index]; 
            set => list[index] = value; 
        }

        /// <inheritdoc/>
        public int Count => list.Count;

        /// <inheritdoc/>
        public bool IsReadOnly => false;

        /// <inheritdoc/>
        public void Add(FarModelData item)
        {
            list.Add(item);
        }

        /// <summary>
        /// Creates a map node at the specified position and adds a FarModelData object with
        /// the given properties to the end of the list.
        /// </summary>
        /// <param name="position">The position of the node.</param>
        /// <param name="model">The unit name of the model.</param>
        /// <param name="scale">The scale of the model.</param>
        public void Add(Vector3 position, Token model, Vector3 scale)
        {
            Add(new FarModelData(CreateNode(position), model, scale));
        }

        /// <inheritdoc/>
        public void Clear()
        {
            foreach (var item in list)
                GetRidOfTheNode(item);
            list.Clear();
        }

        /// <inheritdoc/>
        public bool Contains(FarModelData item)
        {
            return list.Contains(item);
        }

        /// <inheritdoc/>
        public void CopyTo(FarModelData[] array, int arrayIndex)
        {
            list.CopyTo(array, arrayIndex);
        }

        /// <inheritdoc/>
        public IEnumerator<FarModelData> GetEnumerator()
        {
            return list.GetEnumerator();
        }

        /// <inheritdoc/>
        public int IndexOf(FarModelData item)
        {
            return list.IndexOf(item);
        }

        /// <inheritdoc/>
        public void Insert(int index, FarModelData item)
        {
            list.Insert(index, item);
        }

        /// <summary>
        /// Creates a map node at the specified position and inserts a FarModelData object with
        /// the given properties at the specified index.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <param name="position">The position of the node.</param>
        /// <param name="model">The unit name of the model.</param>
        /// <param name="scale">The scale of the model.</param>
        public void Insert(int index, Vector3 position, Token model, Vector3 scale)
        {
            Insert(index, new FarModelData(CreateNode(position), model, scale));
        }

        /// <summary>
        /// Removes the first occurrence of the specified object from the list
        /// and deletes its map node if it is not connected to anything else.
        /// </summary>
        /// <inheritdoc/>
        public bool Remove(FarModelData item)
        {
            var success = list.Remove(item);
            if (success)
                GetRidOfTheNode(item);
            return success;
        }

        /// <summary>
        /// Removes the element at the specified index from the list
        /// and deletes its map node if it is not connected to anything else.
        /// </summary>
        /// <inheritdoc/>
        public void RemoveAt(int index)
        {
            GetRidOfTheNode(list[index]);
            list.RemoveAt(index);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return list.GetEnumerator();
        }

        private Node CreateNode(Vector3 position)
        {
            return Parent.Node.Sectors[0].Map.AddNode(position, false, Parent);
        }

        private static void GetRidOfTheNode(FarModelData item)
        {
            item.Node.ForwardItem = null;
            if (item.Node.IsOrphaned())
                item.Node.Sectors[0].Map.Delete(item.Node);
        }
    }
}
