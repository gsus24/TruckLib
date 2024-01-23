﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TruckLib.ScsMap
{
    public class RoadTerrain
    {     
        /// <summary>
        /// Widths of terrain rows in meters. 
        /// <para>After row 15, every row is 100 meters wide.</para>
        /// </summary>
        public static readonly int[] RowWidthSequence = new int[] { 1, 1, 1, 2, 6, 6, 10, 10, 10,
                20, 40, 50, 50, 50, 100 };

        private float size;
        /// <summary>
        /// Terrain size in meters. Must be between 0 and 6500.
        /// </summary>
        public float Size
        {
            get => size;
            set => size = Utils.SetIfInRange(value, 0, 6500);
        }

        /// <summary>
        /// Unit name of the terrain profile.
        /// </summary>
        public Token Profile { get; set; }

        /// <summary>
        /// Vertical scale coefficient of the terrain profile.
        /// </summary>
        public float Coefficient { get; set; }

        /// <summary>
        /// Gets or sets the strength of random noise applied to the vertices of the terrain.
        /// </summary>
        public TerrainNoise Noise { get; set; }

        /// <summary>
        /// Length of terrain transition to neighboring segment.
        /// </summary>
        public TerrainTransition Transition { get; set; }

        /// <summary>
        /// Properties of the terrain quads.
        /// </summary>
        public TerrainQuadData QuadData { get; set; } 

        /// <summary>
        /// Instantiates a RoadTerrain with its default values.
        /// </summary>
        public RoadTerrain()
        {
            Init();
        }

        internal RoadTerrain(bool initFields)
        {
            if (initFields) Init();
        }

        /// <summary>
        /// Sets the RoadTerrain's properties to its default values.
        /// </summary>
        protected void Init()
        {
            Profile = "profile0";
            Coefficient = 1f;
            Noise = TerrainNoise.Percent100;
            Transition = TerrainTransition._16;
            QuadData = new TerrainQuadData();
        }

        /// <summary>
        /// Returns the width of a terrain quad row at the given index.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <returns>The width of a terrain quad row at that index.</returns>
        public static int GetRowWidthAt(int index)
        {
            if (index < RowWidthSequence.Length)
            {
                return RowWidthSequence[index];
            }
            return RowWidthSequence[^1];
        }

        /// <summary>
        /// Updates the amount of quad columns and rows of this terrain.
        /// </summary>
        /// <param name="stepSize">The step size of the standalone terrain.</param>
        /// <param name="length">The length of the standalone terrain.</param>
        public void CalculateQuadGrid(StepSize stepSize, float length)
        {
            QuadData.Cols = (ushort)CalculateQuadCols(stepSize, length);
            QuadData.Rows = (ushort)CalculateQuadRows(Size);
            UpdateQuadAmount();
        }

        /// <summary>
        /// Calculates the amount of quad columns in this terrain.
        /// </summary>
        /// <param name="stepSize">The step size of the standalone terrain.</param>
        /// <param name="length">The length of the standalone terrain.</param>
        /// <returns>The amount of quad columns.</returns>
        private int CalculateQuadCols(StepSize stepSize, float length)
        {
            int terrainSteps;
            switch (stepSize)
            {
                case StepSize.Meters16:
                    terrainSteps = 16;
                    break;
                case StepSize.Meters12:
                    terrainSteps = 12;
                    break;
                case StepSize.Meters2:
                    terrainSteps = 2;
                    break;
                case StepSize.Meters4:
                default:
                    terrainSteps = 4;
                    break;
            }

            int cols = (int)(length / terrainSteps);
            return cols;
        }

        /// <summary>
        /// Updates the amount of quad columns and rows of this terrain.
        /// </summary>
        /// <param name="resolution">The resolution of the road.</param>
        /// <param name="length">The length of the road.</param>
        public void CalculateQuadGrid(RoadResolution resolution, float length)
        {
            QuadData.Cols = (ushort)CalculateQuadCols(resolution, length);
            QuadData.Rows = (ushort)CalculateQuadRows(Size);
            UpdateQuadAmount();
        }

        private void UpdateQuadAmount()
        {
            var amt = QuadData.Cols * QuadData.Rows;

            var quads = QuadData.Quads;
            if (amt == 0)
            {
                quads.Clear();
                return;
            }

            if (amt == quads.Count)
                return;

            if (quads.Count < amt)
            {
                var missing = amt - quads.Count;
                quads.Capacity += missing;
                for(int i= 0; i < missing; i++)
                {
                    quads.Add(new TerrainQuad());
                }
            }
            else
            {
                quads.RemoveRange(amt, quads.Count - amt);
            }
        }

        /// <summary>
        /// Calculates the amount of quad columns in this terrain.
        /// </summary>
        /// <param name="resolution">The resolution of the road.</param>
        /// <param name="length">The length of the road.</param>
        /// <returns>The amount of quad columns.</returns>
        private int CalculateQuadCols(RoadResolution resolution, float length)
        {
            int interval;
            int colsPerInterval;
            switch (resolution)
            {
                case RoadResolution.Superfine:
                    interval = 1;
                    colsPerInterval = 1;
                    break;
                case RoadResolution.HighPoly:
                    interval = 15;
                    colsPerInterval = 3;
                    break;
                case RoadResolution.Normal:
                default:
                    interval = 15;
                    colsPerInterval = 1;
                    break;
            }
            var cols = ((int)(length / interval) + 1) * colsPerInterval; 
            return cols;
        }

        /// <summary>
        /// Calculates the amount of quad rows in this terrain.
        /// </summary>
        /// <param name="terrainSize">The terrain size.</param>
        /// <returns>The amount of quad rows.</returns>
        private int CalculateQuadRows(float terrainSize)
        {
            // get the amt of rows by subtracting the sequence of widths
            // from the terrain size until it is 0 or negative.
            // TODO: Check if the game ever uses a width > 100.
            var rows = 0;
            var remainder = terrainSize;
            while (remainder > 0)
            {
                if (rows < RowWidthSequence.Length)
                {
                    remainder -= RowWidthSequence[rows];
                }
                else
                {
                    remainder -= RowWidthSequence.Last();
                }
                rows++;
            }

            return rows;
        }

        /// <summary>
        /// Makes a deep copy of this object.
        /// </summary>
        /// <returns>A deep copy of this object.</returns>
        public RoadTerrain Clone()
        {
            var rt = (RoadTerrain)MemberwiseClone();
            rt.QuadData = QuadData.Clone();
            return rt;
        }
    }
}
