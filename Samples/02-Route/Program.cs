﻿using System;
using System.IO;
using System.Numerics;
using TruckLib.ScsMap;

using Microsoft.VisualBasic.FileIO;
using System.Globalization;

namespace Route
{
    class Program
    {
        // dummys
        static Road r0;
        static Road r1;
        
        static void Main(string[] args)
        {            

            // load or create, edit offsets
            var map = new Map("pgi");
            //Map map = Map.Open(@"C:\Users\worker\Documents\Euro Truck Simulator 2\mod\user_map\map\pgi.mbd");
                   
            // read .csv
            // https://valhalla.openstreetmap.de/directions?profile=car&wps=8.736566305160524%2C47.9225903172469%2C8.736308813095095%2C47.92535111067535%2C8.732757568359377%2C47.92223082809373%2C8.736013770103456%2C47.92231351082017
            var path = @"C:\Users\worker\Documents\dev\ets2\PGI\data\bergstr_out.csv"; 
            using (TextFieldParser csvParser = new TextFieldParser(path))
            {
                // parser options
                csvParser.CommentTokens = new string[] { "#" };
                csvParser.SetDelimiters(new string[] { ";" });
                csvParser.HasFieldsEnclosedInQuotes = false;

                // last waypoint
                float x0 = 0;
                float y0 = 0;
                float z0 = 0;

                // offset
                float xoff = 0f;
                float yoff = 0f; //*-1
                float zoff = 0f;

                // helper
                bool firstLoop = true;
                bool firstOdd = true;
                bool odd = true;

                while (!csvParser.EndOfData)
                {
                    // Read current line fields, pointer moves to the next line.
                    string[] fields = csvParser.ReadFields();
                    fields[0] = fields[0].Replace(',', '.');
                    fields[1] = fields[1].Replace(',', '.');
                    fields[2] = fields[2].Replace(',', '.');
                    float x1 = float.Parse(fields[0], CultureInfo.InvariantCulture.NumberFormat) - xoff;
                    float y1 = float.Parse(fields[1], CultureInfo.InvariantCulture.NumberFormat) - yoff;
                    float z1 = float.Parse(fields[2], CultureInfo.InvariantCulture.NumberFormat) - zoff;

                    
                    
                    if(!firstLoop)
                    {
                        if(odd)
                        {
                            r0 = Road.Add(map,
                            new Vector3(x0, y0, z0), // position of backward (red) node
                            new Vector3(x1, z1, y1),    // position of forward (green) node
                            "ger1",  // unit name of the road model
                            10, 10   // terrain size on the left and right side
                            );
                            SetProperties(r0);

                            if(!firstOdd)
                            {
                                r1.ForwardNode.Merge(r0.Node);                                
                            }

                            firstOdd = false;

                        }

                        else
                        {   
                            r1 = Road.Add(map,
                                new Vector3(x0, y0, z0), // position of backward (red) node
                                new Vector3(x1, z1, y1),    // position of forward (green) node
                                "ger1",  // unit name of the road model
                                10, 10   // terrain size on the left and right side
                                );
                            SetProperties(r1);

                            r0.ForwardNode.Merge(r1.Node);
                                                        
                        } 

                        odd = !odd;

                    }
                                       
                    firstLoop = false;
                    
                    x0 = x1;
                    y0 = y1;
                    z0 = z1;

                }
            
            }

            // Save the map
            var documentsFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var userMapFolder = Path.Combine(documentsFolder, "Euro Truck Simulator 2/mod/user_map/map/");
            map.Save(userMapFolder, true);

            // Remember to recalculate (Map > Recompute map) after loading it in the editor for the first time.
        }

        static void SetProperties(Road r)
        {
            // Road properties:
            // For roads that don't consist of two seperate roadways,
            // various properties introduced with the template system
            // are set for the right side of the road but apply to both
            // sides, and the values on the left side are ignored.
            r.Right.Look = "ger_1";
            r.Right.Variant = "broken_de";
            r.Right.LeftEdge = "ger_sh_15";
            r.Right.RightEdge = "ger_sh_15";
            // Terrain properties:
            // Define what the terrain will look like.
            // We'll first set all of the following properties for the right side
            // and then clone them to the left side.
            r.Right.Terrain.QuadData.BrushMaterials[0] = new Material("34"); // "grass_ger_main"
            r.Right.Terrain.Profile = "profile12"; // "hills2"
            r.Right.Terrain.Noise = TerrainNoise.Percent0;
            r.Right.Terrain.Coefficient = 0.5f;

            // Vegetation properties:
            // Let's add some vegetation.
            r.Right.Vegetation[0].Name = "v2_1ger"; // "ger - mixed forest"
            r.Right.Vegetation[0].Density = 200;
            r.Right.Vegetation[0].From = 15;
            r.Right.Vegetation[0].To = 80;

            // Road models:
            // Let's add some bollards as well.
            r.Right.Models[0].Name = "219"; // "reflective post"
            r.Right.Models[0].Distance = 50;
            r.Right.Models[0].Offset = 6;

            // Copy all of these properties to the right side.
            r.Left = r.Right.Clone();
        }
    }
}
