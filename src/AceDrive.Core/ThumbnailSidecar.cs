namespace AceDrive.Core;

/// <summary>
/// Reads Kunos .texture sidecars that accompany .texturemips files: a tiny
/// protobuf header whose field 1 is width and field 2 height (varints).
/// Pure byte math so a future EVO Lua port transliterates it.
/// </summary>
public static class ThumbnailSidecar
{
    public static bool TryReadDims(ReadOnlySpan<byte> sidecar, out int width, out int height)
    {
        width = height = 0;
        int pos = 0;
        while (pos < sidecar.Length)
        {
            if (!TryReadVarint(sidecar, ref pos, out int key)) return false;
            int field = key >> 3;
            int wire = key & 7;
            if (wire == 2) // nested length-delimited (mip table): skip it
            {
                if (!TryReadVarint(sidecar, ref pos, out int len)) return false;
                if (len < 0 || pos + len > sidecar.Length) return false;
                pos += len;
                continue;
            }

            if (wire != 0) return false; // fixed-32/64/zigzag: unknown header shape
            if (!TryReadVarint(sidecar, ref pos, out int value)) return false;
            if (field == 1) width = value;
            else if (field == 2) height = value;
        }

        return width > 0 && height > 0;
    }

    private static bool TryReadVarint(ReadOnlySpan<byte> data, ref int pos, out int value)
    {
        value = 0;
        int shift = 0;
        while (pos < data.Length)
        {
            byte b = data[pos++];
            value |= (b & 0x7F) << shift;
            if ((b & 0x80) == 0) return true;
            shift += 7;
            if (shift > 28) return false; // dimensions never need more than int28
        }

        return false;
    }
}
