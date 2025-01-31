﻿using mc_compiled.Commands.Selectors;
using mc_compiled.Commands;
using mc_compiled.MCC.Compiler;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Ink;

namespace mc_compiled.MCC.Compiler
{
    /// <summary>
    /// Utilities for dealing with JSON/preprocessor stuff.
    /// </summary>
    public static class PreprocessorUtils
    {
        /// <summary>
        /// Returns if a JToken is a base type that can be converted into a non-json literal, outputting that token if true.
        /// </summary>
        /// <param name="token">The input token to check.</param>
        /// <param name="lineNumber">The line number to associate with the output literal, if created.</param>
        /// <param name="output">The output if this method returns true.</param>
        /// <returns></returns>
        public static bool TryGetLiteral(JToken token, int lineNumber, out TokenLiteral output)
        {
            switch (token.Type)
            {
                case JTokenType.Array:
                case JTokenType.Object:
                    output = new TokenJSONLiteral(token, lineNumber);
                    return true;

                case JTokenType.Integer:
                    output = new TokenIntegerLiteral(token.Value<int>(), IntMultiplier.none, lineNumber);
                    return true;
                case JTokenType.Float:
                    output = new TokenDecimalLiteral(token.Value<float>(), lineNumber);
                    return true;
                case JTokenType.String:
                    output = new TokenStringLiteral(token.Value<string>(), lineNumber);
                    return true;
                case JTokenType.Boolean:
                    output = new TokenBooleanLiteral(token.Value<bool>(), lineNumber);
                    return true;
                case JTokenType.Date:
                    output = new TokenStringLiteral(token.Value<DateTime>().ToString(), lineNumber);
                    return true;
                case JTokenType.Guid:
                    output = new TokenStringLiteral(token.Value<Guid>().ToString(), lineNumber);
                    return true;
                case JTokenType.Uri:
                    output = new TokenStringLiteral(token.Value<Uri>().OriginalString, lineNumber);
                    return true;
                case JTokenType.TimeSpan:
                    int time = (int)Math.Round(token.Value<TimeSpan>().TotalSeconds * 20d); // convert to ticks
                    output = new TokenIntegerLiteral(time, IntMultiplier.s, lineNumber);
                    return true;

                default:
                    output = null;
                    return false;
            }
        }
        /// <summary>
        /// Returns if a JToken can be unwrapped into a C# object. Outputs the object in a parameter if true.
        /// </summary>
        /// <param name="token">The input token to unwrap.</param>
        /// <param name="obj">The object to output, if returning true.</param>
        /// <returns></returns>
        public static bool TryUnwrapToken(JToken token, out object obj)
        {
            switch (token.Type)
            {
                case JTokenType.Array:
                case JTokenType.Object:
                    obj = token;
                    return true;

                case JTokenType.Integer:
                    obj = token.Value<int>();
                    return true;
                case JTokenType.Float:
                    obj = token.Value<float>();
                    return true;
                case JTokenType.String:
                    obj = token.Value<string>();
                    return true;
                case JTokenType.Boolean:
                    obj = token.Value<bool>();
                    return true;
                case JTokenType.Date:
                    obj = token.Value<DateTime>().ToString();
                    return true;
                case JTokenType.Guid:
                    obj = token.Value<Guid>().ToString();
                    return true;
                case JTokenType.Uri:
                    obj = token.Value<Uri>().OriginalString;
                    return true;
                case JTokenType.TimeSpan:
                    obj = (int)Math.Round(token.Value<TimeSpan>().TotalSeconds * 20d); // convert to ticks
                    return true;

                default:
                    obj = null;
                    return false;
            }
        }


        /// <summary>
        /// Wraps a dynamic value in its associated literal.
        /// </summary>
        /// <param name="value">The value to wrap.</param>
        /// <param name="line">The line number the token should originate on.</param>
        /// <returns>null if the dynamic couldn't be wrapped.</returns>
        public static TokenLiteral DynamicToLiteral(dynamic value, int line)
        {
            if (value is int)
                return new TokenIntegerLiteral(value, IntMultiplier.none, line);
            if (value is float)
                return new TokenDecimalLiteral(value, line);
            if (value is bool)
                return new TokenBooleanLiteral(value, line);
            if (value is string)
                return new TokenStringLiteral(value, line);
            if (value is Coord)
                return new TokenCoordinateLiteral(value, line);
            if (value is Selector)
                return new TokenSelectorLiteral(value, line);
            if (value is Range)
                return new TokenRangeLiteral(value, line);
            if (value is JToken)
                return new TokenJSONLiteral(value, line);

            return null;
        }


        /// <summary>
        /// Parse the "JSONAccessor" format documented for MCCompiled.
        /// </summary>
        /// <param name="tree"></param>
        /// <returns></returns>
        public static string[] ParseAccessor(string tree)
        {
            char[] SEPARATORS = { '.', '/' };
            return tree.Split(SEPARATORS);
        }
    }
}
