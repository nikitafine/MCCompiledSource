﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mc_compiled.MCC
{
    /// <summary>
    /// Provides ways of doing estimated fixed point math.
    /// </summary>
    public static class FixedPoint
    {
        /// <summary>
        /// Convert this float to a fixed point number with a set precision.
        /// </summary>
        /// <param name="number"></param>
        /// <param name="precision"></param>
        /// <returns></returns>
        public static int ToFixedPoint(this float number, int precision)
        {
            return (int)Math.Floor(number * (float)Math.Pow(10, precision));
        }
        /// <summary>
        /// Raises this integer to a power of 10 that represents this fixed point number.
        /// </summary>
        /// <param name="integer"></param>
        /// <param name="precision"></param>
        /// <returns></returns>
        public static int ToFixedPoint(this int integer, int precision)
        {
            return integer * (int)Math.Pow(10, precision);
        }
        /// <summary>
        /// Get the level of precision needed to represent this float.
        /// </summary>
        /// <param name="number"></param>
        /// <returns></returns>
        public static int GetPrecision(this float number)
        {
            string str = number.ToString();
            return str.Length - str.IndexOf('.') - 1;
        }
    }
}
