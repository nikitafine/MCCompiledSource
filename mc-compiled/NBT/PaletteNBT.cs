﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mc_compiled.NBT
{
    public struct PaletteNBT
    {
        public PaletteEntryNBT[] block_palette;

        // NBTCompound blockPositionData;    // Unimplemented. This will break more complex structures!

        public PaletteNBT(params PaletteEntryNBT[] entries)
        {
            block_palette = entries;
        }
        public NBTCompound ToNBT()
        {
            return new NBTCompound()
            {
                name = "palette",
                values = new NBTNode[]
                {
                    new NBTCompound()
                    {
                        name = "default",
                        values = new NBTNode[]
                        {
                            new NBTList()
                            {
                                name = "block_palette",
                                listType = TAG.Compound,
                                values = (from bp in block_palette select bp.ToNBT("")).ToArray()
                            },
                            new NBTCompound()
                            {
                                name = "block_position_data",
                                values = new NBTNode[] { new NBTEnd() }
                            },
                            new NBTEnd()
                        }
                    },
                    new NBTEnd()
                }
            };
        }
    }
}
