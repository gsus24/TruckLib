﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TruckLib.ScsMap
{
    /// <summary>
    /// A single terrain quad.
    /// </summary>
    public class TerrainQuad : IBinarySerializable
    {
        /// <summary>
        /// Index of the main terrain material of this quad.
        /// </summary>
        public Nibble MainMaterial { get; set; }

        /// <summary>
        /// Additional material that will be drawn on top with the specified
        /// opacity value.
        /// </summary>
        public Nibble BlendMaterial { get; set; }

        /// <summary>
        /// Opacity for the blend material.
        /// </summary>
        public Nibble Opacity { get; set; }

        /// <summary>
        /// Texture color in the bottom left corner.
        /// </summary>
        public Nibble ColorBottomLeft { get; set; }

        /// <summary>
        /// Texture color in the bottom right corner.
        /// </summary>
        public Nibble ColorBottomRight { get; set; }

        /// <summary>
        /// Texture color in the top left corner.
        /// </summary>
        public Nibble ColorTopLeft { get; set; }

        /// <summary>
        /// Texture color in the top right corner.
        /// </summary>
        public Nibble ColorTopRight { get; set; }

        /// <summary>
        /// Vegetation setting for this quad.
        /// </summary>
        public QuadVegetation Vegetation;

        public void ReadFromStream(BinaryReader r)
        {
            const int n1Mask = 0x0F;
            const int n2Mask = 0xF0;

            // nibble 1: main material
            // nibble 2: 2nd material to blend on top of main material
            var byte1 = r.ReadByte();
            MainMaterial = (Nibble)(byte1 & n1Mask);
            BlendMaterial = (Nibble)((byte1 & n2Mask) >> 4);

            // nibble 1: material blend opacity
            // nibble 2: bottom left color
            var byte2 = r.ReadByte();
            Opacity = (Nibble)(byte2 & n1Mask);
            ColorBottomLeft = (Nibble)((byte2 & n2Mask) >> 4);

            // nibble 1: bottom right color
            // nibble 2: top left color
            var byte3 = r.ReadByte();
            ColorBottomRight = (Nibble)(byte3 & n1Mask);
            ColorTopLeft = (Nibble)((byte3 & n2Mask) >> 4);

            // nibble 1: top right color
            // nibble 2: normal veg. / no detail veg. / no veg.
            var byte4 = r.ReadByte();
            ColorTopRight = (Nibble)(byte4 & n1Mask);
            Vegetation = (QuadVegetation)((byte4 & n2Mask) >> 4);
        }

        public void WriteToStream(BinaryWriter w)
        {
            byte byte1 = 0;
            byte1 |= (byte)MainMaterial;
            byte1 |= (byte)((byte)BlendMaterial << 4);
            w.Write(byte1);

            byte byte2 = 0;
            byte2 |= (byte)Opacity;
            byte2 |= (byte)((byte)ColorBottomLeft << 4);
            w.Write(byte2);

            byte byte3 = 0;
            byte3 |= (byte)ColorBottomRight;
            byte3 |= (byte)((byte)ColorTopLeft << 4);
            w.Write(byte3);

            byte byte4 = 0;
            byte4 |= (byte)ColorTopRight;
            byte4 |= (byte)((byte)Vegetation << 4);
            w.Write(byte4);
        }
    }
}
