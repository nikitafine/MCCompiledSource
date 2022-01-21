﻿using mc_compiled.Commands;
using mc_compiled.Json;
using mc_compiled.MCC.Compiler;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mc_compiled.MCC
{
    /// <summary>
    /// A scoreboard value that can be written to.
    /// </summary>
    public abstract class ScoreboardValue
    {
        public const int MAX_NAME_LENGTH = 16;
        public string baseName;

        public ScoreboardValue(string baseName)
        {
            this.baseName = baseName;
        }

        /// <summary>
        /// Get the commands to define this value.
        /// </summary>
        /// <returns></returns>
        public abstract string[] CommandsDefine();
        /// <summary>
        /// Get the commands to initialize this value for all players.
        /// </summary>
        /// <returns></returns>
        public abstract string[] CommandsInit();
       /// <summary>
       /// Get the commands to set this value to any given literal.
       /// </summary>
       /// <param name="accessor">The variable name used to access this. Might be field of struct.</param>
       /// <param name="selector">The selector to set this for.</param>
       /// <param name="token">The literal that is being used as the value.</param>
       /// <returns></returns>
        public abstract string[] CommandsSetLiteral(string accessor, string selector, TokenLiteral token);

        /// <summary>
        /// Setup temporary variables before printing this variable as rawtext.
        /// </summary>
        /// <param name="accessor">The string used to access this value.</param>
        /// <param name="selector"></param>
        /// <param name="index">The index to use to identify scoreboard values.</param>
        /// <returns></returns>
        public abstract string[] CommandsRawTextSetup(string accessor, string selector, int index);
        /// <summary>
        /// Convert this scoreboard value to its expression as rawtext.
        /// Basically the Minecraft equivalent of ToString().
        /// Called immediately after ToRawTextSetup(string).
        /// </summary>
        /// <param name="accessor">The string used to access this value.</param>
        /// <param name="selector"></param>
        /// <param name="index">The index to use to identify scoreboard values.</param>
        /// <returns></returns>
        public abstract JSONRawTerm[] ToRawText(string accessor, string selector, int index);

        /// <summary>
        /// Get the maximum name length for this scoreboard value type.
        /// </summary>
        /// <returns></returns>
        public abstract int GetMaxNameLength();
        /// <summary>
        /// Get the accessibile names that can be used to reference this variable.
        /// </summary>
        /// <returns></returns>
        public abstract string[] GetAccessibleNames();
    }


    public class ScoreboardValueInteger : ScoreboardValue
    {
        public ScoreboardValueInteger(string baseName) : base(baseName) { }

        public override string[] CommandsDefine()
        {
            return new[] { Command.ScoreboardCreateObjective(baseName) };
        }
        public override string[] CommandsInit()
        {
            return new[] { Command.ScoreboardAdd("@a", baseName, 0) };
        }
        public override string[] CommandsSetLiteral(string accessor, string selector, TokenLiteral token)
        {
            if (token is TokenStringLiteral)
                return new string[] { };

            if (token is TokenNumberLiteral)
                return new string[] { Command.ScoreboardSet(selector, baseName,
                    (token as TokenNumberLiteral).GetNumberInt())};

            if (token is TokenBooleanLiteral)
                return new string[] { };

            return new string[] { };
        }

        public override string[] CommandsRawTextSetup(string accessor, string selector, int index)
        {
            return new string[0];
        }
        public override JSONRawTerm[] ToRawText(string accessor, string selector, int index)
        {
            return new[] { new JSONScore(selector, baseName) };
        }

        public override int GetMaxNameLength() =>
            MAX_NAME_LENGTH;
        public override string[] GetAccessibleNames() =>
            new[] { baseName };
    }
    public class ScoreboardValueTime : ScoreboardValueInteger
    {
        public const string SB_MINUTES = "_mcc_t_mins";
        public const string SB_SECONDS = "_mcc_t_secs";
        public const string SB_TEMP = "_mcc_t_temp";
        public const string SB_CONST = "_mcc_t_const";
        public ScoreboardValueTime(string baseName) : base(baseName) { }

        public override string[] CommandsRawTextSetup(string accessor, string selector, int index)
        {
            string minutes = SB_MINUTES + index;
            string seconds = SB_SECONDS + index;
            string temporary = SB_TEMP + index;
            string constant = SB_CONST + index;

            return new string[]
            {
                Command.ScoreboardCreateObjective(minutes),
                Command.ScoreboardCreateObjective(seconds),
                Command.ScoreboardCreateObjective(temporary),
                Command.ScoreboardCreateObjective(constant),

                Command.ScoreboardSet("@a", constant, 20),
                Command.ScoreboardOpSet(selector, temporary, baseName),
                Command.ScoreboardOpDiv(selector, temporary, constant),
                Command.ScoreboardOpSet(selector, seconds, temporary),
                Command.ScoreboardSet("@a", constant, 60),
                Command.ScoreboardOpDiv(selector, temporary, constant),
                Command.ScoreboardOpSet(selector, minutes, temporary),
                Command.ScoreboardOpMul(selector, temporary, constant),
                Command.ScoreboardOpSub(selector, seconds, temporary)
            };
        }
        public override JSONRawTerm[] ToRawText(string accessor, string selector, int index)
        {
            string minutes = SB_MINUTES + index;
            string seconds = SB_SECONDS + index;

            return new JSONRawTerm[]
            {
                new JSONScore(selector, minutes),
                new JSONText(":"),
                new JSONScore(selector, seconds)
            };
        }

        public override int GetMaxNameLength() =>
            MAX_NAME_LENGTH;
        public override string[] GetAccessibleNames() =>
            new[] { baseName };
    }
    public sealed class ScoreboardValueDecimal : ScoreboardValue
    {
        public const string WHOLE_SUFFIX = ":w";
        public const string DECIMAL_SUFFIX = ":d";
        public string WholeName { get => baseName + WHOLE_SUFFIX; }
        public string DecimalName { get => baseName + DECIMAL_SUFFIX; }

        public readonly int precision;

        public ScoreboardValueDecimal(string baseName, int precision) : base(baseName)
        {
            this.precision = precision;
        }

        public override string[] CommandsDefine()
        {
            return new[] {
                Command.ScoreboardCreateObjective(WholeName),
                Command.ScoreboardCreateObjective(DecimalName)
            };
        }
        public override string[] CommandsInit()
        {
            return new[] { Command.ScoreboardAdd("@a", baseName, 0) };
        }
        public override string[] CommandsSetLiteral(string accessor, string selector, TokenLiteral token)
        {
            if (token is TokenStringLiteral)
                return new string[] { };

            if (token is TokenIntegerLiteral)
            {
                int integer = (token as TokenIntegerLiteral).number;
                return new string[] {
                    Command.ScoreboardSet(selector, WholeName, integer),
                    Command.ScoreboardSet(selector, DecimalName, 0)
                };
            }
            if (token is TokenDecimalLiteral)
            {
                TokenDecimalLiteral literal = token as TokenDecimalLiteral;
                double number = literal.number.FixPoint(precision);
                int wholePart = (int)Math.Floor(number);
                int decimalPart = (number - wholePart).ToFixedInt(precision);
                return new string[] {
                    Command.ScoreboardSet(selector, WholeName, wholePart),
                    Command.ScoreboardSet(selector, DecimalName, decimalPart)
                };
            }

            if (token is TokenBooleanLiteral)
                return new string[] { };

            return new string[] { };
        }

        public override string[] CommandsRawTextSetup(string accessor, string selector, int index)
        {
            return new string[0];
        }
        public override JSONRawTerm[] ToRawText(string accessor, string selector, int index)
        {
            return new JSONRawTerm[]
            {
                new JSONScore(selector, WholeName),
                new JSONText("."),
                new JSONScore(selector, DecimalName)
            };
        }

        public override int GetMaxNameLength() =>
           MAX_NAME_LENGTH - 2;
        public override string[] GetAccessibleNames() =>
            new[] { baseName };
    }
    public sealed class ScoreboardValueBoolean : ScoreboardValue
    {
        public ScoreboardValueBoolean(string baseName) : base(baseName) { }

        public override string[] CommandsDefine()
        {
            return new[] { Command.ScoreboardCreateObjective(baseName) };
        }
        public override string[] CommandsInit()
        {
            return new[] { Command.ScoreboardAdd("@a", baseName, 0) }; // init to false
        }
        public override string[] CommandsSetLiteral(string accessor, string selector, TokenLiteral token)
        {
            if (token is TokenStringLiteral)
                return new string[] { };

            if (token is TokenNumberLiteral)
                return new string[] { Command.ScoreboardSet(selector, baseName,
                    (token as TokenNumberLiteral).GetNumberInt() % 2)};

            if (token is TokenBooleanLiteral)
                return new string[] { Command.ScoreboardSet(selector, baseName,
                    (token as TokenBooleanLiteral).boolean ? 1 : 0)};

            return new string[] { };
        }

        public override string[] CommandsRawTextSetup(string accessor, string selector, int index)
        {
            return new string[0];
        }
        public override JSONRawTerm[] ToRawText(string accessor, string selector, int index)
        {
            return new JSONRawTerm[]
            {
                new JSONScore(selector, baseName)
            };
        }

        public override int GetMaxNameLength() =>
            MAX_NAME_LENGTH;
        public override string[] GetAccessibleNames() =>
            new[] { baseName };
    }
    public sealed class ScoreboardValueStruct : ScoreboardValue
    {
        StructDefinition structure;

        public ScoreboardValueStruct(string baseName, StructDefinition structure) : base(baseName)
        {
            this.structure = structure;
        }
        private ScoreboardValue ParseAccessor(string accessor)
        {
            int colon = accessor.IndexOf(':');
            string field = accessor.Substring(colon + 1);
            return structure.GetField(field);
        }

        public override string[] CommandsDefine()
        {
            return structure.GetFullyQualifiedInternalNames(baseName);
        }
        public override string[] CommandsInit()
        {
            return structure.GetFullyQualifiedInternalNames(baseName)
                .Select(n => Command.ScoreboardAdd("@a", n, 0)).ToArray();
        }
        public override string[] CommandsSetLiteral(string accessor, string selector, TokenLiteral token)
        {
            ScoreboardValue value = ParseAccessor(accessor);

            string old = value.baseName;
            value.baseName = baseName + ':' + old;
            string[] ret = value.CommandsSetLiteral(null, selector, token);
            value.baseName = old;

            return ret;
        }

        public override string[] CommandsRawTextSetup(string accessor, string selector, int index)
        {
            ScoreboardValue value = ParseAccessor(accessor);
            return value.CommandsRawTextSetup(null, selector, index);
        }
        public override JSONRawTerm[] ToRawText(string accessor, string selector, int index)
        {
            ScoreboardValue value = ParseAccessor(accessor);
            return value.ToRawText(null, selector, index);
        }

        public override int GetMaxNameLength() =>
            MAX_NAME_LENGTH - 3;
        public override string[] GetAccessibleNames() =>
            structure.GetFullyQualifiedNames(baseName);
    }


    /// <summary>
    /// Manages the virtual scoreboard.
    /// </summary>
    public class ScoreboardManager
    {
        readonly Dictionary<string, ScoreboardValue> values;

        public ScoreboardManager()
        {
            values = new Dictionary<string, ScoreboardValue>();
        }

        public ScoreboardValue this[string name]
        {
            get
            {
                name = name.ToUpper();
                return values[name];
            }
        }
        public ScoreboardValue this[ScoreboardValue sb]
        {
            set
            {
                values[sb.baseName.ToUpper()] = sb;
            }
        }
    }
}