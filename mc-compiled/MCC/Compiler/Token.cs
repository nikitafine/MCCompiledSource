﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace mc_compiled.MCC.Compiler
{
    /// <summary>
    /// The most basic processed unit in the file.
    /// Gives basic parsed information and generally assists in the assembly of a Statement
    /// </summary>
    public abstract class Token
    {
        /// <summary>
        /// Get this token represented as it might look in the source file.
        /// </summary>
        /// <returns></returns>
        public abstract string AsString();

        public int lineNumber;
        public Token(int lineNumber)
        {
            this.lineNumber = lineNumber;
        }

        public override string ToString() => AsString();

        /// <summary>
        /// Get a string that provides more information about the token given.
        /// </summary>
        /// <returns></returns>
        public string DebugString()
        {
            if(this is TokenNewline)
                return "[TokenNewline]\n";

            string typeName = GetType().Name.Substring(5);
            return '[' + typeName + ' ' + AsString() + ']';
        }
    }
    public struct TokenDefinition
    {
        public readonly Type type;
        public readonly string keyword;

        public TokenDefinition(Type type, string keyword)
        {
            this.type = type;
            this.keyword = keyword;
        }
    }
}