﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mc_compiled.Commands.Selectors
{
    public struct BlockCheck
    {
        public static readonly BlockCheck DISABLED = new BlockCheck() { present = false };
        /// <summary>
        /// only false if this blockcheck is disabled
        /// </summary>
        public bool present;

        public Coord x, y, z;
        public string block;
        public int? data;

        public BlockCheck(string x, string y, string z, string block = "air", string data = null)
        {
            present = true;
            this.x = Coord.Parse(x).GetValueOrDefault();
            this.y = Coord.Parse(y).GetValueOrDefault();
            this.z = Coord.Parse(z).GetValueOrDefault();
            this.block = block;
            if (data == null || data.Equals("0") || string.IsNullOrEmpty(data))
                this.data = null;
            else
                this.data = int.Parse(data);
        }
        public BlockCheck(Coord x, Coord y, Coord z, string block = "air", int? data = null)
        {
            present = true;
            this.x = x;
            this.y = y;
            this.z = z;
            this.block = block;
            this.data = data;
        }

        public override string ToString()
        {
            List<string> parts = new List<string>();

            parts.Add(x.ToString());
            parts.Add(y.ToString());
            parts.Add(z.ToString());
            parts.Add(block);
            int tempData = data ?? 0;
            parts.Add(tempData.ToString());

            return "detect " + string.Join(" ", parts);
        }
        /// <summary>
        /// Get this BlockCheck as a testfor statement.
        /// </summary>
        /// <returns></returns>
        public string AsStoreIn(string selector, string objective)
        {
            return $"execute {selector} ~~~ detect {x} {y} {z} {block} {data ?? 0} scoreboard players set @s {objective} 1";
        }

        public override bool Equals(object obj)
        {
            return obj is BlockCheck check &&
                   present == check.present &&
                   EqualityComparer<Coord>.Default.Equals(x, check.x) &&
                   EqualityComparer<Coord>.Default.Equals(y, check.y) &&
                   EqualityComparer<Coord>.Default.Equals(z, check.z) &&
                   block == check.block &&
                   data == check.data;
        }
        public override int GetHashCode()
        {
            if (!present)
                return 851597659;

            int hashCode = 851597659;
            hashCode = hashCode * -1521134295 + present.GetHashCode();
            hashCode = hashCode * -1521134295 + x.GetHashCode();
            hashCode = hashCode * -1521134295 + y.GetHashCode();
            hashCode = hashCode * -1521134295 + z.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(block);
            hashCode = hashCode * -1521134295 + data.GetHashCode();
            return hashCode;
        }
    }
}