﻿using System;
using System.Collections.Generic;
using System.Text;

namespace TruckLib
{
    public struct Nibble
    {
        public const int MinValue = 0;
        public const int MaxValue = 15;
        private byte value;
        private byte Value
        {
            get => this.value;
            set
            {
                ValueInRange(value);
                this.value = value;
            }
        }

        public Nibble(byte value)
        {
            this.value = 0;
            Value = value;
        }

        private static void ValueInRange(byte value)
        {
            if (value < MinValue || value > MaxValue)
                throw new ArgumentOutOfRangeException();
        }

        private static Nibble Add(int a, int b)
        {
            var result = (byte)(a + b);
            result &= 0x0F; // simulate overflow
            return (Nibble)result;
        }

        public static Nibble operator +(Nibble a, int b) => Add(a.Value, b);
        public static Nibble operator +(int a, Nibble b) => Add(a, b.Value);
        public static Nibble operator +(Nibble a, Nibble b) => Add(a.Value, b.Value);
        public static Nibble operator ++(Nibble a) => Add(a.Value, 1);

        private static Nibble Subtract(int a, int b)
        {
            var result = (byte)(a - b);
            result &= 0x0F; // simulate overflow
            return (Nibble)result;
        }

        public static Nibble operator -(Nibble a, Nibble b) => Subtract(a.Value, b.Value);
        public static Nibble operator -(Nibble a, int b) => Subtract(a.Value, b);
        public static Nibble operator -(int a, Nibble b) => Subtract(a, b.Value);
        public static Nibble operator --(Nibble a) => Subtract(a.Value, 1);

        public static bool operator >(Nibble a, Nibble b) => a.Value > b.Value;
        public static bool operator >(Nibble a, int b) => a.Value > b;
        public static bool operator >(int a, Nibble b) => a > b.Value;

        public static bool operator <(Nibble a, Nibble b) => a.Value < b.Value;
        public static bool operator <(Nibble a, int b) => a.Value < b;
        public static bool operator <(int a, Nibble b) => a < b.Value;

        public static bool operator ==(Nibble a, Nibble b) => a.Value == b.Value;
        public static bool operator ==(Nibble a, int b) => a.Value == b;
        public static bool operator ==(int a, Nibble b) => a == b.Value;

        public static bool operator !=(Nibble a, Nibble b) => a.Value != b.Value;
        public static bool operator !=(Nibble a, int b) => a.Value != b;
        public static bool operator !=(int a, Nibble b) => a != b.Value;

        public static explicit operator Nibble(byte b)
        {
            return new Nibble(b);
        }

        public static explicit operator Nibble(int i)
        {
            return new Nibble((byte)i);
        }

        public static explicit operator byte(Nibble n)
        {
            return n.Value;
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return value.GetHashCode();
        }

        public override string ToString()
        {
            return value.ToString();
        }
    }
}
