﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mc_compiled.NBT
{
    public struct EntityListNBT
    {
        EntityNBT[] entities;

        public EntityListNBT(params EntityNBT[] entities)
        {
            this.entities = entities;
        }

        public NBTList ToNBT()
        {
            return new NBTList()
            {
                name = "entities",
                listType = TAG.Compound,
                values = (from entity in entities select entity.ToNBT("")).ToArray()
            };
        }
    }
}
