﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace TruckLib.ScsMap.Serialization
{
    class SoundSerializer : MapItemSerializer
    {
        public override MapItem Deserialize(BinaryReader r)
        {
            var sound = new Sound(false);
            ReadKdopItem(r, sound);

            sound.Name = r.ReadToken();
            sound.Reverb = r.ReadToken();
            sound.AreaWidth = r.ReadSingle();
            sound.AreaHeight = r.ReadSingle();
            sound.Node = new UnresolvedNode(r.ReadUInt64());

            return sound;
        }

        public override void Serialize(BinaryWriter w, MapItem item)
        {
            var sound = item as Sound;
            WriteKdopItem(w, sound);
            w.Write(sound.Name);
            w.Write(sound.Reverb);
            w.Write(sound.AreaWidth);
            w.Write(sound.AreaHeight);
            w.Write(sound.Node.Uid);
        }
    }
}
