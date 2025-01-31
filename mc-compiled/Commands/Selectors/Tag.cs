﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mc_compiled.Commands.Selectors
{
    /// <summary>
    /// Represents a selector option that limits from a tag.
    /// </summary>
    public struct Tag
    {
        public bool not;
        public string tagName;  // Can be null

        public Tag(string tagName, bool not)
        {
            this.not = not;
            this.tagName = tagName;
        }
        public Tag(string tagName)
        {
            not = tagName.StartsWith("!");
            if (not)
                this.tagName = tagName.Substring(1);
            else this.tagName = tagName;
        }

        /// <summary>
        /// Parse something like "!is_waiting"
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static Tag Parse(string str)
        {
            if (str == null)
                throw new ArgumentNullException();

            str = str.Trim();

            if(str.Length == 0)
                return new Tag("", false);

            if (str.StartsWith("!"))
                if (str.Length == 1)
                    return new Tag("", true);
                else
                    return new Tag(str.Substring(1), true);
            else
                return new Tag(str, false);
        }

        public string GetSection()
        {
            string s = tagName ?? "";
            if (not)
                return "tag=!" + s;
            else
                return "tag=" + s;
        }

        public override bool Equals(object obj)
        {
            return obj is Tag tag &&
                   not == tag.not &&
                   tagName == tag.tagName;
        }
        public override int GetHashCode()
        {
            int hashCode = 1337537810;
            hashCode = hashCode * -1521134295 + not.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(tagName);
            return hashCode;
        }
    }
}
