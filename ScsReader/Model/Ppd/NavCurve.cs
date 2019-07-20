﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace ScsReader.Model.Ppd
{
    /// <summary>
    /// Represents a navigation curve, used to define AI traffic paths and GPS navigation.
    /// </summary>
    public class NavCurve : IBinarySerializable
    {
        public Token Name;

        public (byte EndNode, byte EndLane, byte StartNode, byte StartLane) LeadsToNodes;

        public Vector3 StartPosition;

        public Vector3 EndPosition;

        public Quaternion StartRotation;

        public Quaternion EndRotation;

        public float Length;

        public int[] NextLines = new int[4];

        public int[] PreviousLines = new int[4];

        public uint CountNext;

        public uint CountPrevious;

        public int SemaphoreId;

        public Token TrafficRule;

        public uint NewData1Id;

        protected BitArray Flags = new BitArray(32);

        // TODO: Check if these flags are correct

        public byte PriorityModifier
        {
            get => (byte)Flags.GetBitString(16, 4);
            set => Flags.SetBitString(value, 16, 4);
        }

        public bool ForceNoBlinker
        {
            get => Flags[2];
            set => Flags[2] = value;
        }

        public bool RightBlinker
        {
            get => Flags[3];
            set => Flags[3] = value;
        }

        public bool LeftBlinker
        {
            get => Flags[4];
            set => Flags[4] = value;
        }

        /// <summary>
        /// Determines if small AI vehicles can use this curve.
        /// <para>AI vehicles will try to go into most suitable curve, 
        /// but if there will be none, they can also use any other 
        /// even if they are not allowed to.</para>
        /// </summary>
        public bool AllowSmallVehicles
        {
            get => Flags[5];
            set => Flags[5] = value;
        }

        /// <summary>
        /// Determines if large AI vehicles can use this curve.
        /// <para>AI vehicles will try to go into most suitable curve, 
        /// but if there will be none, they can also use any other 
        /// even if they are not allowed to.</para>
        /// </summary>
        public bool AllowLargeVehicles
        {
            get => Flags[6];
            set => Flags[6] = value;
        }

        /// <summary>
        /// Determines if the probability of AI vehicles entering this (prefab? nav. path?) is lowered.
        /// </summary>
        public bool LowProbability
        {
            get => Flags[14];
            set => Flags[14] = value;
        }

        /// <summary>
        /// Property defining extra limited displacement for AI vehicles.
        /// </summary>
        public bool LimitDisplacement
        {
            get => Flags[15];
            set => Flags[15] = value;
        }

        /// <summary>
        /// Determines if the PriorityModifier value will be added 
        /// to already existing priority for this lane.
        /// </summary>
        public bool AdditivePriority
        {
            get => Flags[16];
            set => Flags[16] = value;
        }

        public void ReadFromStream(BinaryReader r)
        {
            Name = r.ReadToken();
            Flags = new BitArray(r.ReadBytes(4));

            LeadsToNodes.EndNode = r.ReadByte();
            LeadsToNodes.EndLane = r.ReadByte();
            LeadsToNodes.StartNode = r.ReadByte();
            LeadsToNodes.StartLane = r.ReadByte();

            StartPosition = r.ReadVector3();
            EndPosition = r.ReadVector3();

            StartRotation = r.ReadQuaternion();
            EndRotation = r.ReadQuaternion();

            Length = r.ReadSingle();

            for (int i = 0; i < NextLines.Length; i++)
            {
                NextLines[i] = r.ReadInt32();
            }

            for (int i = 0; i < PreviousLines.Length; i++)
            {
                PreviousLines[i] = r.ReadInt32();
            }

            CountNext = r.ReadUInt32();
            CountPrevious = r.ReadUInt32();

            SemaphoreId = r.ReadInt32();

            TrafficRule = r.ReadToken();

            NewData1Id = r.ReadUInt32();
        }

        public void WriteToStream(BinaryWriter w)
        {
            throw new NotImplementedException();
        }
    }
}

