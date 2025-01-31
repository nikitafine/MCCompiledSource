﻿using mc_compiled.Commands;
using mc_compiled.Commands.Selectors;
using mc_compiled.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace mc_compiled.MCC.Compiler
{
    /// <summary>
    /// Represents a literal in code.
    /// </summary>
    public abstract class TokenLiteral : Token
    {
        public override string AsString() => "<? literal>";
        public abstract override string ToString();
        public TokenLiteral(int lineNumber) : base(lineNumber) { }

        /// <summary>
        /// Return a NEW token literal that is the result of adding these two literals.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public abstract TokenLiteral AddWithOther(TokenLiteral other);
        /// <summary>
        /// Return a NEW token literal that is the result of subtracting these two literals.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public abstract TokenLiteral SubWithOther(TokenLiteral other);
        /// <summary>
        /// Return a NEW token literal that is the result of multiplying these two literals.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public abstract TokenLiteral MulWithOther(TokenLiteral other);
        /// <summary>
        /// Return a NEW token literal that is the result of dividing these two literals.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public abstract TokenLiteral DivWithOther(TokenLiteral other);
        /// <summary>
        /// Return a NEW token literal that is the result of modulo'ing these two literals.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public abstract TokenLiteral ModWithOther(TokenLiteral other);
        /// <summary>
        /// Return a NEW token literal that is the result of modulo'ing these two literals.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public abstract bool CompareWithOther(TokenCompare.Type cType, TokenLiteral other);
    }

    /// <summary>
    /// Represents a generic number literal.
    /// </summary>
    public abstract class TokenNumberLiteral : TokenLiteral, IPreprocessor
    {
        public TokenNumberLiteral(int lineNumber) : base(lineNumber) { }

        public int GetNumberInt()
        {
            return (int)Math.Round(GetNumber());
        }

        /// <summary>
        /// Get the number stored in this literal.
        /// </summary>
        /// <returns></returns>
        public abstract float GetNumber();

        public abstract object GetValue();
    }
    public sealed class TokenStringLiteral : TokenLiteral, IPreprocessor, IImplicitToken, IIndexable
    {
        public readonly string text;

        public override string AsString() => '"' + text + '"';
        public TokenStringLiteral(string text, int lineNumber) : base(lineNumber)
        {
            this.text = text;
        }
        public override string ToString() => text;
        public object GetValue() =>  text;

        public static implicit operator string(TokenStringLiteral literal) => literal.text;

        public override TokenLiteral AddWithOther(TokenLiteral other)
        {
            if (!(other is IPreprocessor))
                throw new TokenException(this, "Invalid literal operation.");

            string append = (other as IPreprocessor).GetValue().ToString();
            return new TokenStringLiteral(text + append, lineNumber);
        }
        public override TokenLiteral SubWithOther(TokenLiteral other)
        {
            if(other is TokenStringLiteral)
            {
                string str = other as TokenStringLiteral;
                if (text.EndsWith(str))
                    str = text.Substring(0, text.Length - str.Length);
                return new TokenStringLiteral(str, lineNumber);
            } else if(other is TokenNumberLiteral)
            {
                int number = (other as TokenNumberLiteral).GetNumberInt();
                string str;

                if (number > text.Length)
                    str = "";
                else
                    str = text.Substring(0, text.Length - number);

                return new TokenStringLiteral(str, lineNumber);
            }

            throw new TokenException(this, "Invalid literal operation.");
        }
        public override TokenLiteral MulWithOther(TokenLiteral other)
        {
            if (other is TokenNumberLiteral)
            {
                float number = (other as TokenNumberLiteral).GetNumber();
                int length = (int)Math.Round(text.Length * number);

                int sample = 0;
                char[] characters = new char[length];
                for(int i = 0; i < length; i++)
                {
                    characters[i] = text[sample++];
                    if (sample >= text.Length)
                        sample = 0;
                }

                return new TokenStringLiteral(new string(characters), lineNumber);
            }

            throw new TokenException(this, "Invalid literal operation.");
        }
        public override TokenLiteral DivWithOther(TokenLiteral other)
        {
            if (other is TokenNumberLiteral)
            {
                float number = (other as TokenNumberLiteral).GetNumber();
                int length = (int)Math.Round(text.Length / number);

                return new TokenStringLiteral(text.Substring(0, length), lineNumber);
            }

            throw new TokenException(this, "Invalid literal operation.");
        }
        public override TokenLiteral ModWithOther(TokenLiteral other)
        {
            throw new TokenException(this, "Invalid literal operation.");
        }
        public override bool CompareWithOther(TokenCompare.Type cType, TokenLiteral other)
        {
            int a = text.Length;
            int b;

            if (other is TokenNumberLiteral)
                b = (other as TokenNumberLiteral).GetNumberInt();
            else if (other is TokenStringLiteral)
                b = (other as TokenStringLiteral).text.Length;
            else
                throw new NotImplementedException("TokenStringLiteral being compared to " + other.GetType().Name);

            switch (cType)
            {
                case TokenCompare.Type.EQUAL:
                    return a == b;
                case TokenCompare.Type.NOT_EQUAL:
                    return a != b;
                case TokenCompare.Type.LESS_THAN:
                    return a < b;
                case TokenCompare.Type.LESS_OR_EQUAL:
                    return a <= b;
                case TokenCompare.Type.GREATER_THAN:
                    return a > b;
                case TokenCompare.Type.GREATER_OR_EQUAL:
                    return a >= b;
                default:
                    throw new Exception("Unknown comparison type: " + cType);
            }
        }

        public Type[] GetImplicitTypes()
        {
            return new[] { typeof(TokenSelectorLiteral) };
        }
        public Token Convert(Executor executor, int index)
        {
            if(index == 0)
            {
                // try parsing selector from string
                int len = text.Length;

                if (len == 0)
                    return new TokenSelectorLiteral(Selector.Core.s, lineNumber);

                if(len > 1 && text[0] == '@')
                {
                    char _core = text[1];
                    Selector.Core core = Selector.ParseCore(_core);

                    if (len > 2)
                    {
                        Selector parsed = Selector.Parse(core, text.Substring(2));
                        return new TokenSelectorLiteral(parsed, lineNumber);
                    }

                    Selector single = new Selector() { core = core };
                    return new TokenSelectorLiteral(single, lineNumber);
                }

                string name = text;

                // try finding by managed entity name
                if (executor.entities.Search(name, out Selector find))
                    return new TokenSelectorLiteral(find, lineNumber);

                // use name:type format
                string type = null;
                int colon = name.IndexOf(':');
                if (colon != -1)
                {
                    string old = name;
                    name = old.Substring(0, colon);
                    if (old.Length >= colon)
                        type = old.Substring(colon + 1);
                }
                if (string.IsNullOrEmpty(name))
                    name = null;
                if (string.IsNullOrEmpty(type))
                    type = null;

                return new TokenSelectorLiteral(new Selector()
                {
                    core = Selector.Core.e,
                    entity = new Commands.Selectors.Entity(name, type, new List<string>())
                }, lineNumber);
            }

            return null;
        }

        public Token Index(TokenIndexer indexer, Statement forExceptions)
        {
            if (indexer is TokenIndexerInteger integer)
            {
                int value = integer.token.number;
                int length = text.Length;
                if (length >= value || value < 0)
                    throw integer.GetIndexOutOfBounds(0, length - 1, forExceptions);

                string newString = text[value].ToString();
                return new TokenStringLiteral(newString, lineNumber);
            }

            throw indexer.GetException(this, forExceptions);
        }
    }
    public sealed class TokenBooleanLiteral : TokenNumberLiteral, IPreprocessor
    {
        public readonly bool boolean;
        public override string AsString() => boolean.ToString();
        public TokenBooleanLiteral(bool boolean, int lineNumber) : base(lineNumber)
        {
            this.boolean = boolean;
        }
        public override string ToString() => boolean.ToString();
        public override object GetValue() => boolean;
        public override float GetNumber()
        {
            return boolean ? 1 : 0;
        }

        public static implicit operator bool(TokenBooleanLiteral literal) => literal.boolean;

        public override TokenLiteral AddWithOther(TokenLiteral other)
        {
            throw new TokenException(this, "Invalid literal operation.");
        }
        public override TokenLiteral SubWithOther(TokenLiteral other)
        {
            throw new TokenException(this, "Invalid literal operation.");
        }
        public override TokenLiteral MulWithOther(TokenLiteral other)
        {
            throw new TokenException(this, "Invalid literal operation.");
        }
        public override TokenLiteral DivWithOther(TokenLiteral other)
        {
            throw new TokenException(this, "Invalid literal operation.");
        }
        public override TokenLiteral ModWithOther(TokenLiteral other)
        {
            throw new TokenException(this, "Invalid literal operation.");
        }
        public override bool CompareWithOther(TokenCompare.Type cType, TokenLiteral other)
        {
            throw new NotImplementedException("Cannot compare boolean to another type.");
        }
    }

    public class TokenCoordinateLiteral : TokenNumberLiteral
    {
        public readonly Coord coordinate;
        public override string AsString() => coordinate.ToString();
        public override string ToString() => coordinate.ToString();
        public TokenCoordinateLiteral(Coord coordinate, int lineNumber) : base(lineNumber)
        {
            this.coordinate = coordinate;
        }
        public override float GetNumber()
        {
            if (coordinate.isFloat)
                return coordinate.valuef;
            else
                return coordinate.valuei;
        }
        public override object GetValue() => coordinate;

        public static implicit operator Coord(TokenCoordinateLiteral literal) => literal.coordinate;
        public static implicit operator int(TokenCoordinateLiteral literal) => literal.coordinate.valuei;
        public static implicit operator float(TokenCoordinateLiteral literal) => literal;

        public override TokenLiteral AddWithOther(TokenLiteral other)
        {
            if(other is TokenNumberLiteral)
            {
                float number = (other as TokenNumberLiteral).GetNumber();
                Coord coord = new Coord(coordinate);
                coord.valuef += number;
                coord.valuei = (int)Math.Round(coord.valuef);
                return new TokenCoordinateLiteral(coord, lineNumber);
            }

            throw new TokenException(this, "Invalid literal operation.");
        }
        public override TokenLiteral SubWithOther(TokenLiteral other)
        {
            if (other is TokenNumberLiteral)
            {
                float number = (other as TokenNumberLiteral).GetNumber();
                Coord coord = new Coord(coordinate);
                coord.valuef -= number;
                coord.valuei = (int)Math.Round(coord.valuef);
                return new TokenCoordinateLiteral(coord, lineNumber);
            }

            throw new TokenException(this, "Invalid literal operation.");
        }
        public override TokenLiteral MulWithOther(TokenLiteral other)
        {
            if (other is TokenNumberLiteral)
            {
                float number = (other as TokenNumberLiteral).GetNumber();
                Coord coord = new Coord(coordinate);
                coord.valuef *= number;
                coord.valuei = (int)Math.Round(coord.valuef);
                return new TokenCoordinateLiteral(coord, lineNumber);
            }

            throw new TokenException(this, "Invalid literal operation.");
        }
        public override TokenLiteral DivWithOther(TokenLiteral other)
        {
            if (other is TokenNumberLiteral)
            {
                float number = (other as TokenNumberLiteral).GetNumber();
                Coord coord = new Coord(coordinate);
                coord.valuef /= number;
                coord.valuei = (int)Math.Round(coord.valuef);
                return new TokenCoordinateLiteral(coord, lineNumber);
            }

            throw new TokenException(this, "Invalid literal operation.");
        }
        public override TokenLiteral ModWithOther(TokenLiteral other)
        {
            if (other is TokenNumberLiteral)
            {
                float number = (other as TokenNumberLiteral).GetNumber();
                Coord coord = new Coord(coordinate);
                coord.valuef %= number;
                coord.valuei = (int)Math.Round(coord.valuef);
                return new TokenCoordinateLiteral(coord, lineNumber);
            }

            throw new TokenException(this, "Invalid literal operation.");
        }
        public override bool CompareWithOther(TokenCompare.Type cType, TokenLiteral other)
        {
            float a = coordinate.valuef;
            float b;

            if (other is TokenNumberLiteral)
                b = (other as TokenNumberLiteral).GetNumber();
            else if (other is TokenStringLiteral)
                b = (other as TokenStringLiteral).text.Length;
            else
                throw new NotImplementedException("TokenCoordinateLiteral being compared to " + other.GetType().Name);

            switch (cType)
            {
                case TokenCompare.Type.EQUAL:
                    return a == b;
                case TokenCompare.Type.NOT_EQUAL:
                    return a != b;
                case TokenCompare.Type.LESS_THAN:
                    return a < b;
                case TokenCompare.Type.LESS_OR_EQUAL:
                    return a <= b;
                case TokenCompare.Type.GREATER_THAN:
                    return a > b;
                case TokenCompare.Type.GREATER_OR_EQUAL:
                    return a >= b;
                default:
                    throw new Exception("Unknown comparison type: " + cType);
            }
        }
    }
    public enum IntMultiplier : int
    {
        none = 1,
        s = 20,
        m = 1200,
        h = 72000
    }
    public class TokenIntegerLiteral : TokenCoordinateLiteral
    {
        public static IntMultiplier[] ALL_MULTIPLIERS = (IntMultiplier[])Enum.GetValues(typeof(IntMultiplier));

        /// <summary>
        /// The number that has already been multiplied.
        /// </summary>
        public readonly int number;
        /// <summary>
        /// The multiplier that was applied to this integer.
        /// </summary>
        public readonly IntMultiplier multiplier;
        
        /// <summary>
        /// Get the value of this number in a certain scale.
        /// If this number has no multiplier given <see cref="IntMultiplier.none"/>, then this method always returns the plain number.
        /// </summary>
        /// <param name="scale"></param>
        /// <returns></returns>
        public int Scaled(IntMultiplier scale)
        {
            // no multiplier given, they probably read the documentation
            if (multiplier == IntMultiplier.none)
                return number;

            // scale matches multiplier
            if (scale == multiplier)
                return number;

            return number / (int)scale;
        }

        public override string AsString() => number.ToString();
        public TokenIntegerLiteral(int number, IntMultiplier multiplier, int lineNumber) :
            base(new Coord(number, false, false, false), lineNumber)
        {
            this.number = number;
            this.multiplier = multiplier;
        }
        public override string ToString() => number.ToString();
        public override object GetValue() => number;
        public override float GetNumber()
        {
            return number;
        }

        public static implicit operator int(TokenIntegerLiteral literal) => literal.number;

        public override TokenLiteral AddWithOther(TokenLiteral other)
        {
            if(other is TokenIntegerLiteral)
            {
                int i = (other as TokenIntegerLiteral).number;
                return new TokenIntegerLiteral(number + i, IntMultiplier.none, lineNumber);
            } else if(other is TokenDecimalLiteral)
            {
                float value = (other as TokenDecimalLiteral).number;
                value += number;
                return new TokenDecimalLiteral(value, lineNumber);
            }

            throw new TokenException(this, "Invalid literal operation.");
        }
        public override TokenLiteral SubWithOther(TokenLiteral other)
        {
            if (other is TokenIntegerLiteral)
            {
                int i = (other as TokenIntegerLiteral).number;
                return new TokenIntegerLiteral(number - i, IntMultiplier.none, lineNumber);
            }
            else if (other is TokenDecimalLiteral)
            {
                float value = (other as TokenDecimalLiteral).number;
                value -= number;
                return new TokenDecimalLiteral(value, lineNumber);
            }

            throw new TokenException(this, "Invalid literal operation.");
        }
        public override TokenLiteral MulWithOther(TokenLiteral other)
        {
            if (other is TokenIntegerLiteral)
            {
                int i = (other as TokenIntegerLiteral).number;
                return new TokenIntegerLiteral(number * i, IntMultiplier.none, lineNumber);
            }
            else if (other is TokenDecimalLiteral)
            {
                float value = (other as TokenDecimalLiteral).number;
                value *= number;
                return new TokenDecimalLiteral(value, lineNumber);
            }

            throw new TokenException(this, "Invalid literal operation.");
        }
        public override TokenLiteral DivWithOther(TokenLiteral other)
        {
            if (other is TokenIntegerLiteral)
            {
                int i = (other as TokenIntegerLiteral).number;
                return new TokenIntegerLiteral(number / i, IntMultiplier.none, lineNumber);
            }
            else if (other is TokenDecimalLiteral)
            {
                float value = (other as TokenDecimalLiteral).number;
                value /= number;
                return new TokenDecimalLiteral(value, lineNumber);
            }

            throw new TokenException(this, "Invalid literal operation.");
        }
        public override TokenLiteral ModWithOther(TokenLiteral other)
        {
            if (other is TokenIntegerLiteral)
            {
                int i = (other as TokenIntegerLiteral).number;
                return new TokenIntegerLiteral(number % i, IntMultiplier.none, lineNumber);
            }
            else if (other is TokenDecimalLiteral)
            {
                float value = (other as TokenDecimalLiteral).number;
                value %= number;
                return new TokenDecimalLiteral(value, lineNumber);
            }

            throw new TokenException(this, "Invalid literal operation.");
        }
    }
    public sealed class TokenRangeLiteral : TokenLiteral, IPreprocessor, IIndexable
    {
        public Range range;
        public override string AsString() => range.ToString();
        public override string ToString() => range.ToString();
        public TokenRangeLiteral(Range range, int lineNumber) : base(lineNumber)
        {
            this.range = range;
        }

        public override TokenLiteral AddWithOther(TokenLiteral other)
        {
            if(other is TokenRangeLiteral)
            {
                Range r = (other as TokenRangeLiteral).range;
                return new TokenRangeLiteral(range + r, lineNumber);
            }
            else if (other is TokenIntegerLiteral)
            {
                int i = (other as TokenIntegerLiteral).number;
                return new TokenRangeLiteral(range + i, lineNumber);
            }
            else if (other is TokenDecimalLiteral)
            {
                float value = (other as TokenDecimalLiteral).number;
                return new TokenRangeLiteral(range + value, lineNumber);
            }

            throw new TokenException(this, "Invalid literal operation.");
        }
        public override TokenLiteral SubWithOther(TokenLiteral other)
        {
            if (other is TokenRangeLiteral)
            {
                Range r = (other as TokenRangeLiteral).range;
                return new TokenRangeLiteral(range - r, lineNumber);
            }
            else if (other is TokenIntegerLiteral)
            {
                int i = (other as TokenIntegerLiteral).number;
                return new TokenRangeLiteral(range - i, lineNumber);
            }
            else if (other is TokenDecimalLiteral)
            {
                float value = (other as TokenDecimalLiteral).number;
                return new TokenRangeLiteral(range - value, lineNumber);
            }

            throw new TokenException(this, "Invalid literal operation.");
        }
        public override TokenLiteral MulWithOther(TokenLiteral other)
        {
            if (other is TokenRangeLiteral)
            {
                Range r = (other as TokenRangeLiteral).range;
                return new TokenRangeLiteral(range * r, lineNumber);
            }
            else if (other is TokenIntegerLiteral)
            {
                int i = (other as TokenIntegerLiteral).number;
                return new TokenRangeLiteral(range * i, lineNumber);
            }
            else if (other is TokenDecimalLiteral)
            {
                float value = (other as TokenDecimalLiteral).number;
                return new TokenRangeLiteral(range * value, lineNumber);
            }

            throw new TokenException(this, "Invalid literal operation.");
        }
        public override TokenLiteral DivWithOther(TokenLiteral other)
        {
            if (other is TokenRangeLiteral)
            {
                Range r = (other as TokenRangeLiteral).range;
                return new TokenRangeLiteral(range / r, lineNumber);
            }
            else if (other is TokenIntegerLiteral)
            {
                int i = (other as TokenIntegerLiteral).number;
                return new TokenRangeLiteral(range / i, lineNumber);
            }
            else if (other is TokenDecimalLiteral)
            {
                float value = (other as TokenDecimalLiteral).number;
                return new TokenRangeLiteral(range / value, lineNumber);
            }

            throw new TokenException(this, "Invalid literal operation.");
        }
        public override TokenLiteral ModWithOther(TokenLiteral other)
        {
            if (other is TokenRangeLiteral)
            {
                Range r = (other as TokenRangeLiteral).range;
                return new TokenRangeLiteral(range % r, lineNumber);
            }
            else if (other is TokenIntegerLiteral)
            {
                int i = (other as TokenIntegerLiteral).number;
                return new TokenRangeLiteral(range % i, lineNumber);
            }
            else if (other is TokenDecimalLiteral)
            {
                float value = (other as TokenDecimalLiteral).number;
                return new TokenRangeLiteral(range % value, lineNumber);
            }

            throw new TokenException(this, "Invalid literal operation.");
        }
        public override bool CompareWithOther(TokenCompare.Type cType, TokenLiteral other)
        {
            int thisMin = range.min.HasValue ? range.min.Value : int.MinValue;
            int thisMax = range.max.HasValue ? range.max.Value : int.MaxValue;

            int b;

            if (other is TokenNumberLiteral)
                b = (other as TokenNumberLiteral).GetNumberInt();
            else if (other is TokenStringLiteral)
                b = (other as TokenStringLiteral).text.Length;
            else
                throw new NotImplementedException("TokenRangeLiteral being compared to " + other.GetType().Name);

            switch (cType)
            {
                case TokenCompare.Type.EQUAL:
                    return b <= thisMax && b >= thisMin;
                case TokenCompare.Type.NOT_EQUAL:
                    return b < thisMin || b > thisMax;
                case TokenCompare.Type.LESS_THAN:
                    return b < thisMin;
                case TokenCompare.Type.LESS_OR_EQUAL:
                    return b <= thisMin;
                case TokenCompare.Type.GREATER_THAN:
                    return b > thisMax;
                case TokenCompare.Type.GREATER_OR_EQUAL:
                    return b >= thisMax;
                default:
                    throw new Exception("Unknown comparison type: " + cType);
            }
        }

        public object GetValue()
        {
            // dereference to an integer
            if (range.single && !range.invert)
                return range.min.Value;

            return range;
        }
        public Token Index(TokenIndexer indexer, Statement forExceptions)
        {
            if (indexer is TokenIndexerInteger integer)
            {
                int value = integer.token.number;
                if (value > 1 || value < 0)
                    throw integer.GetIndexOutOfBounds(0, 2, forExceptions);

                // if its a single number both should return the same value
                if(range.single)
                    return new TokenIntegerLiteral(range.min.Value, IntMultiplier.none, lineNumber);

                // fetch min/max for 0/1
                if (value == 0)
                    return new TokenIntegerLiteral(range.min ?? 0, IntMultiplier.none, lineNumber);
                else
                    return new TokenIntegerLiteral(range.max ?? 0, IntMultiplier.none, lineNumber);
            }
            if(indexer is TokenIndexerString indexerString)
            {
                string input = indexerString.token.text.ToUpper();
                switch (input)
                {
                    case "MIN":
                    case "MINIMUM":
                    case "X":
                    case "A":
                        return new TokenIntegerLiteral(range.min ?? 0, IntMultiplier.none, lineNumber);
                    case "MAX":
                    case "MAXIMUM":
                    case "Y":
                    case "B":
                        return new TokenIntegerLiteral(range.max ?? 0, IntMultiplier.none, lineNumber);
                }

                throw new Exception($"Invalid indexer for range: '{input}'");
            }

            throw indexer.GetException(this, forExceptions);
        }
    }
    public sealed class TokenDecimalLiteral : TokenCoordinateLiteral
    {
        public readonly float number;
        public override string AsString() => number.ToString();
        public TokenDecimalLiteral(float number, int lineNumber) :
            base(new Coord(number, true, false, false), lineNumber)
        {
            this.number = number;
        }
        public override string ToString() => number.ToString();
        public override object GetValue() => number;
        public override float GetNumber()
        {
            return number;
        }

        public static implicit operator float(TokenDecimalLiteral literal) => literal.number;

        public override TokenLiteral AddWithOther(TokenLiteral other)
        {
            if(other is TokenNumberLiteral)
            {
                float f = (other as TokenNumberLiteral).GetNumber();
                return new TokenDecimalLiteral(number + f, lineNumber);
            }

            throw new TokenException(this, "Invalid literal operation.");
        }
        public override TokenLiteral SubWithOther(TokenLiteral other)
        {
            if (other is TokenNumberLiteral)
            {
                float f = (other as TokenNumberLiteral).GetNumber();
                return new TokenDecimalLiteral(number - f, lineNumber);
            }

            throw new TokenException(this, "Invalid literal operation.");
        }
        public override TokenLiteral MulWithOther(TokenLiteral other)
        {
            if (other is TokenNumberLiteral)
            {
                float f = (other as TokenNumberLiteral).GetNumber();
                return new TokenDecimalLiteral(number * f, lineNumber);
            }

            throw new TokenException(this, "Invalid literal operation.");
        }
        public override TokenLiteral DivWithOther(TokenLiteral other)
        {
            if (other is TokenNumberLiteral)
            {
                float f = (other as TokenNumberLiteral).GetNumber();
                return new TokenDecimalLiteral(number / f, lineNumber);
            }

            throw new TokenException(this, "Invalid literal operation.");
        }
        public override TokenLiteral ModWithOther(TokenLiteral other)
        {
            if (other is TokenNumberLiteral)
            {
                float f = (other as TokenNumberLiteral).GetNumber();
                return new TokenDecimalLiteral(number % f, lineNumber);
            }

            throw new TokenException(this, "Invalid literal operation.");
        }
    }

    /// <summary>
    /// A selector.
    /// </summary>
    public class TokenSelectorLiteral : TokenLiteral, IPreprocessor
    {
        public readonly bool simple;
        public readonly Selector selector;

        public override string AsString() => selector.ToString();
        public TokenSelectorLiteral(Selector selector, int lineNumber) : base(lineNumber)
        {
            simple = false;
            this.selector = selector;
        }
        public TokenSelectorLiteral(Selector.Core core, int lineNumber) : base(lineNumber)
        {
            simple = true;
            this.selector = new Selector()
            {
                core = core
            };
        }
        public override string ToString() => selector.ToString();

        public static implicit operator Selector(TokenSelectorLiteral t) => t.selector;
        public static implicit operator Selector.Core(TokenSelectorLiteral t) => t.selector.core;

        public override TokenLiteral AddWithOther(TokenLiteral other)
        {
            throw new NotImplementedException();
        }
        public override TokenLiteral SubWithOther(TokenLiteral other)
        {
            throw new NotImplementedException();
        }
        public override TokenLiteral MulWithOther(TokenLiteral other)
        {
            throw new NotImplementedException();
        }
        public override TokenLiteral DivWithOther(TokenLiteral other)
        {
            throw new NotImplementedException();
        }
        public override TokenLiteral ModWithOther(TokenLiteral other)
        {
            throw new NotImplementedException();
        }
        public override bool CompareWithOther(TokenCompare.Type cType, TokenLiteral other)
        {
            throw new NotImplementedException("Cannot compare selector to another type.");
        }

        public object GetValue() => selector;
    }
    /// <summary>
    /// A literal holding a JSON value. Mostly used for indexing purposes.
    /// </summary>
    public class TokenJSONLiteral : TokenLiteral, IPreprocessor, IIndexable
    {
        public readonly JToken token;

        public bool IsObject
        {
            get => token.Type == JTokenType.Object;
        }
        public bool IsArray
        {
            get => token.Type == JTokenType.Array;
        }

        public override string AsString() => token.ToString();
        public override string ToString() => token.ToString();
        public TokenJSONLiteral(JToken token, int lineNumber) : base(lineNumber)
        {
            this.token = token;
        }
        public static implicit operator JToken(TokenJSONLiteral t) => t.token;

        public object GetValue() => token;
        public override TokenLiteral AddWithOther(TokenLiteral other)
        {
            throw new NotImplementedException();
        }
        public override TokenLiteral SubWithOther(TokenLiteral other)
        {
            throw new NotImplementedException();
        }
        public override TokenLiteral MulWithOther(TokenLiteral other)
        {
            throw new NotImplementedException();
        }
        public override TokenLiteral DivWithOther(TokenLiteral other)
        {
            throw new NotImplementedException();
        }
        public override TokenLiteral ModWithOther(TokenLiteral other)
        {
            throw new NotImplementedException();
        }
        public override bool CompareWithOther(TokenCompare.Type cType, TokenLiteral other)
        {
            throw new NotImplementedException();
        }

        public Token Index(TokenIndexer indexer, Statement forExceptions)
        {
            if(indexer is TokenIndexerInteger integer)
            {
                if (!(token is JArray))
                    throw new StatementException(forExceptions, "JSON type cannot be indexed using a number: " + token.Type);

                JArray array = (JArray)token;
                int value = integer.token.number;
                int len = array.Count;

                if (value >= len || value < 0)
                    throw integer.GetIndexOutOfBounds(0, len - 1, forExceptions);

                JToken indexedItem = array[value];
                if(PreprocessorUtils.TryGetLiteral(indexedItem, lineNumber, out TokenLiteral output))
                    return output;
                else
                    throw new StatementException(forExceptions, "Couldn't load JSON value: " + indexedItem.ToString());
            }
            if(indexer is TokenIndexerString @string)
            {
                if(!(token is JObject))
                    throw new StatementException(forExceptions, "JSON type cannot be indexed using a string: " + token.Type);

                JObject json = (JObject)token;
                string word = @string.token.text;

                JToken gottenToken = json[word];

                if (gottenToken == null)
                    throw new StatementException(forExceptions, $"No JSON property found with the name '{word}'");

                if (PreprocessorUtils.TryGetLiteral(gottenToken, lineNumber, out TokenLiteral output))
                    return output;
                else
                    throw new StatementException(forExceptions, "Couldn't load JSON value: " + gottenToken.ToString());
            }

            throw indexer.GetException(this, forExceptions);
        }
    }
}