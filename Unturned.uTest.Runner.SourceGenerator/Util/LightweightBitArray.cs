using System.Collections;

namespace uTest.Util;

internal struct LightweightBitArray
{
    public BitArray? Bits;
    public long BitValue;

    public int Capacity => Bits?.Length ?? 64;

    public LightweightBitArray(int ct, bool defaultValue = false)
    {
        if (ct > 64)
        {
            Bits = new BitArray(ct);
        }

        if (defaultValue)
            SetAll(true);
    }

    public bool this[int index]
    {
        get
        {
            if (Bits != null)
                return Bits[index];

            return ((BitValue >>> index) & 1) == 1;
        }
        set
        {
            if (Bits != null)
            {
                Bits[index] = value;
                return;
            }

            long mask = (long)(1ul << index);
            if (value)
                BitValue |= mask;
            else
                BitValue &= ~mask;
        }
    }

    public void SetAll(bool value)
    {
        if (Bits == null)
        {
            BitValue = -1L;
        }
        else
        {
            Bits?.SetAll(value);
        }
    }
}