using System;
using Unturned.SystemEx;
using Random = System.Random;

namespace uTest;

internal static class ReservedIPv4AddressHelper
{
    internal static readonly uint[] Blocks;
    internal static readonly uint RangeSize;

    public const ushort MinDynamicPort = 49152;
    public const ushort MaxDynamicPort = ushort.MaxValue;

    static ReservedIPv4AddressHelper()
    {
        ReadOnlySpan<IPv4Filter> ranges =
        [
            // https://www.iana.org/assignments/ipv4-address-space/ipv4-address-space.xhtml
            Parse("0.0.0.0/8"),
            Parse("10.0.0.0/8"),
            Parse("100.64.0.0/10"),
            Parse("127.0.0.0/8"),
            Parse("169.254.0.0/16"),
            Parse("172.16.0.0/12"),
            Parse("192.0.0.0/24"),
            Parse("192.0.2.0/24"),
            Parse("192.31.196.0/24"),
            Parse("192.52.193.0/24"),
            Parse("192.88.99.0/24"),
            Parse("192.168.0.0/16"),
            Parse("192.175.48.0/24"),
            Parse("198.18.0.0/15"),
            Parse("198.51.100.0/24"),
            Parse("203.0.113.0/24"),
            Parse("224.0.0.0/3")
        ];

        Blocks = new uint[ranges.Length * 2];
        uint totalAvailableAddresses = uint.MaxValue;
        uint last = 0;
        for (int i = 0; i < ranges.Length; ++i)
        {
            IPv4Filter f = ranges[i];
            f.GetAddressRange(out IPv4Address min, out IPv4Address max);
            if (i != 0 && min.value <= last)
                throw new InvalidOperationException($"Block {min}-{max} ({f}) overlaps with {ranges[i - 1]}.");
            Blocks[i * 2] = min.value;
            Blocks[i * 2 + 1] = max.value;
            last = max.value;
            totalAvailableAddresses -= max.value - min.value + 1;
        }

        RangeSize = totalAvailableAddresses;

        return;

        static IPv4Filter Parse(string str)
        {
            if (!IPv4Filter.TryParse(str, out IPv4Filter filter))
                throw new FormatException($"Failed to parse address {str}.");

            uint mask = filter.subnetMask.value;
            if ((filter.address.value & ~mask) != 0)
                throw new FormatException($"Didn't use lowest possible address for {str}.");

            return filter;
        }
    }

    private static Random _randomOnMainThread;

    /// <summary>
    /// Checks whether or not an IP address is reserved.
    /// </summary>
    /// <param name="address"></param>
    /// <returns></returns>
    public static bool IsReserved(IPv4Address address)
    {
        uint v = address.value;
        for (int i = 0; i < Blocks.Length; i += 2)
        {
            if (v >= Blocks[i] && v <= Blocks[i + 1])
                return true;
        }

        return false;
    }

    /// <summary>
    /// Gets a random IPv4 address in the public address space.
    /// </summary>
    public static IPv4Address GetRandomPublicAddress()
    {
        uint value;
        if (GameThread.IsCurrent)
        {
            _randomOnMainThread ??= new Random();
            value = RandomUInt32(_randomOnMainThread);
        }
        else
        {
            Random r = new Random();
            value = RandomUInt32(r);
        }

        return GetMappedPublicAddress(value);
    }

    internal static IPv4Address GetMappedPublicAddress(uint value)
    {
        // assumes value is within [0, RangeSize).
        for (int i = 0; i < Blocks.Length; i += 2)
        {
            uint min = Blocks[i], max = Blocks[i + 1];

            if (value < min)
                break;

            value += max - min + 1;
        }

        return new IPv4Address(value);
    }
    
    private static uint RandomUInt32(Random r)
    {
        int lo = r.Next(0, RangeSize <= ushort.MaxValue ? unchecked ( (int)RangeSize ) : ushort.MaxValue + 1);
        int hi = RangeSize <= ushort.MaxValue ? 0 : r.Next(0, unchecked ( (int)(RangeSize >> 16) ));
        return (uint)lo | ((uint)hi << 16);
    }
}