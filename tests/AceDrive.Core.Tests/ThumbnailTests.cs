using System.IO;
using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>Thumbnail pipeline ported from the shell test: sidecar + crop math.</summary>
public sealed class ThumbnailTests
{
    [Fact]
    public void Sidecar_RealHeader_ReadsDims()
    {
        // Captured byte-for-byte from a real SavedCars .texture sidecar.
        byte[] sidecar = [0x08, 0x80, 0x08, 0x10, 0x80, 0x08, 0x18, 0x01, 0x20, 0x03, 0x30, 0x01];
        Assert.True(ThumbnailSidecar.TryReadDims(sidecar, out int w, out int h));
        Assert.Equal((1024, 1024), (w, h));
    }

    [Fact]
    public void Sidecar_PkgHeader_WithNestedRecords_ReadsDims()
    {
        // Captured from a content.kspkg studio .texture: after the varint
        // fields there are nested (wire type 2) records that must be skipped.
        byte[] sidecar =
        [
            0x08, 0x80, 0x08, 0x10, 0x80, 0x08, 0x18, 0x01, 0x20, 0x22, 0x30, 0x01,
            0x52, 0x0C, 0x10, 0x80, 0x08, 0x18, 0x80, 0x08, 0x20, 0x01, 0x28, 0x01,
            0x30, 0x03,
        ];
        Assert.True(ThumbnailSidecar.TryReadDims(sidecar, out int w, out int h));
        Assert.Equal((1024, 1024), (w, h));
    }

    [Fact]
    public void Sidecar_NonVarintWireType_Rejected()
    {
        byte[] sidecar = [0x0D, 0x00, 0x00, 0x00]; // field 1, wire type 5
        Assert.False(ThumbnailSidecar.TryReadDims(sidecar, out _, out _));
    }

    [Fact]
    public void Sidecar_TruncatedVarint_Rejected()
    {
        byte[] sidecar = [0x08, 0x80]; // continuation bit set, then EOF
        Assert.False(ThumbnailSidecar.TryReadDims(sidecar, out _, out _));
    }

    [Fact]
    public void Sidecar_MissingHeight_Rejected()
    {
        byte[] sidecar = [0x08, 0x10]; // only width (16)
        Assert.False(ThumbnailSidecar.TryReadDims(sidecar, out _, out _));
    }

    private static byte[] SyntheticBgra(int w, int h, int carTop, int carBottom)
    {
        byte[] px = new byte[w * h * 4];
        for (int i = 0; i < px.Length; i += 4)
        {
            px[i] = 45; px[i + 1] = 47; px[i + 2] = 46; px[i + 3] = 255; // studio gray
        }

        for (int y = carTop; y < carBottom; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = (y * w + x) * 4;
                px[i] = 200; px[i + 1] = 30; px[i + 2] = 30; // red car band
            }
        }

        return px;
    }

    [Fact]
    public void Cropper_FindsCarBand_WithMargin()
    {
        byte[] px = SyntheticBgra(64, 64, carTop: 30, carBottom: 40);
        // Sampling runs over even rows: first match 30, last match 38;
        // headroom pads the roof, margin pads the wheels.
        Assert.Equal((30 - ThumbnailCropper.HeadroomPx, 38 + ThumbnailCropper.MarginPx),
            ThumbnailCropper.ContentRows(px, 64, 64));
    }

    [Fact]
    public void Cropper_RoofAtImageTop_ClampsHeadroom()
    {
        // Low-slung render: 20px of headroom would run off the image; the
        // kept band must start at row 0 instead of going negative.
        byte[] px = SyntheticBgra(64, 64, carTop: 2, carBottom: 40);
        Assert.Equal((0, 38 + ThumbnailCropper.MarginPx),
            ThumbnailCropper.ContentRows(px, 64, 64));
    }

    [Fact]
    public void Cropper_UniformBackdrop_ReturnsNull()
    {
        byte[] px = SyntheticBgra(64, 64, carTop: 0, carBottom: 0);
        Assert.Null(ThumbnailCropper.ContentRows(px, 64, 64));
    }

    [Fact]
    public void Cropper_ShortBuffer_ReturnsNull()
    {
        Assert.Null(ThumbnailCropper.ContentRows(new byte[100], 64, 64));
    }

    [Fact]
    public void Extractor_SyntheticPair_WritesCroppedPng()
    {
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            Directory.CreateDirectory(dir);
            // 64x64 -> varints fit in one byte each: field 1=0x40, field 2=0x40.
            File.WriteAllBytes(Path.Combine(dir, "car.texture"), [0x08, 0x40, 0x10, 0x40]);
            File.WriteAllBytes(Path.Combine(dir, "car.texturemips"),
                SyntheticBgra(64, 64, carTop: 20, carBottom: 40));

            var extractor = new CarThumbnailExtractor(new NullLogger());
            string? png = extractor.EnsurePng(Path.Combine(dir, "car.texturemips"), dir);
            Assert.NotNull(png);
            Assert.Equal(Path.Combine(dir, "car.png"), png);
            byte[] head = File.ReadAllBytes(png)[..8];
            Assert.Equal([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], head);

            // Cached: same path, no re-decode needed (delete input to prove).
            File.Delete(Path.Combine(dir, "car.texturemips"));
            Assert.Equal(png, extractor.EnsurePng(Path.Combine(dir, "car.texturemips"), dir));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Extractor_SidecarMismatch_ReturnsNull()
    {
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, "car.texture"), [0x08, 0x40, 0x10, 0x40]); // 64x64
            File.WriteAllBytes(Path.Combine(dir, "car.texturemips"), new byte[65 * 64 * 4]); // wrong size

            var extractor = new CarThumbnailExtractor(new NullLogger());
            Assert.Null(extractor.EnsurePng(Path.Combine(dir, "car.texturemips"), dir));
            Assert.False(File.Exists(Path.Combine(dir, "car.png")));
        }
        finally { Directory.Delete(dir, true); }
    }

    private static void WriteRenderPair(string dir, Guid guid)
    {
        File.WriteAllBytes(Path.Combine(dir, guid.ToString("D") + ".texture"),
            [0x08, 0x40, 0x10, 0x40]);
        File.WriteAllBytes(Path.Combine(dir, guid.ToString("D") + ".texturemips"),
            SyntheticBgra(64, 64, carTop: 20, carBottom: 40));
    }

    [Fact]
    public void PlayerCarThumb_ResolvesNewestOpenDataState()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            string renders = Path.Combine(root, "thumbs");
            string states = Path.Combine(root, "profiles", "prof1", "OpenData", "SavedCars");
            string backup = Path.Combine(root, "profiles", "prof1", "backup_9", "SavedCars");
            Directory.CreateDirectory(renders);
            Directory.CreateDirectory(states);
            Directory.CreateDirectory(backup);
            var old = Guid.NewGuid();
            var newe = Guid.NewGuid();
            WriteRenderPair(renders, old);
            WriteRenderPair(renders, newe);
            var stamp = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
            File.WriteAllText(Path.Combine(states, $"ks_test_car_{old:D}.carfinalstatewithconsumable"), "x");
            File.SetLastWriteTimeUtc(Path.Combine(states, $"ks_test_car_{old:D}.carfinalstatewithconsumable"), stamp);
            File.WriteAllText(Path.Combine(states, $"ks_test_car_{newe:D}.carfinalstatewithconsumable"), "x");
            File.SetLastWriteTimeUtc(Path.Combine(states, $"ks_test_car_{newe:D}.carfinalstatewithconsumable"), stamp.AddDays(1));
            // Newest overall but outside OpenData: must be ignored.
            File.WriteAllText(Path.Combine(backup, $"ks_test_car_{old:D}.carfinalstatewithconsumable"), "x");
            File.SetLastWriteTimeUtc(Path.Combine(backup, $"ks_test_car_{old:D}.carfinalstatewithconsumable"), stamp.AddDays(9));

            var extractor = new CarThumbnailExtractor(
                new NullLogger(), renders, Path.Combine(root, "profiles"));
            string? png = extractor.TryGetPlayerCarThumb(
                "ks_test_car", Path.Combine(root, "cache"));
            Assert.Equal(Path.Combine(root, "cache", $"{newe:D}.png"), png);
            Assert.True(File.Exists(png));

            Assert.Null(extractor.TryGetPlayerCarThumb("ks_other", Path.Combine(root, "cache")));
            Assert.Null(extractor.TryGetPlayerCarThumb("../evil", Path.Combine(root, "cache")));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void PlayerCarThumb_SkipsBlankNewestRender()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            string renders = Path.Combine(root, "thumbs");
            string states = Path.Combine(root, "profiles", "prof1", "OpenData", "SavedCars");
            Directory.CreateDirectory(renders);
            Directory.CreateDirectory(states);
            var withCar = Guid.NewGuid();
            var blank = Guid.NewGuid();
            WriteRenderPair(renders, withCar);
            // Newest state's render is a uniform backdrop: game saved no car image.
            File.WriteAllBytes(Path.Combine(renders, blank.ToString("D") + ".texture"),
                [0x08, 0x40, 0x10, 0x40]);
            File.WriteAllBytes(Path.Combine(renders, blank.ToString("D") + ".texturemips"),
                SyntheticBgra(64, 64, carTop: 0, carBottom: 0));
            var stamp = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
            File.WriteAllText(Path.Combine(states, $"ks_test_car_{withCar:D}.carfinalstatewithconsumable"), "x");
            File.SetLastWriteTimeUtc(Path.Combine(states, $"ks_test_car_{withCar:D}.carfinalstatewithconsumable"), stamp);
            File.WriteAllText(Path.Combine(states, $"ks_test_car_{blank:D}.carfinalstatewithconsumable"), "x");
            File.SetLastWriteTimeUtc(Path.Combine(states, $"ks_test_car_{blank:D}.carfinalstatewithconsumable"), stamp.AddDays(1));

            var extractor = new CarThumbnailExtractor(
                new NullLogger(), renders, Path.Combine(root, "profiles"));
            string? png = extractor.TryGetPlayerCarThumb(
                "ks_test_car", Path.Combine(root, "cache"));
            Assert.Equal(Path.Combine(root, "cache", $"{withCar:D}.png"), png);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void EnsurePng_ReplacesStaleCache_KeepsFreshCache()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            string renders = Path.Combine(root, "thumbs");
            string cache = Path.Combine(root, "cache");
            Directory.CreateDirectory(renders);
            var guid = Guid.NewGuid();
            WriteRenderPair(renders, guid);
            string src = Path.Combine(renders, guid.ToString("D") + ".texturemips");
            string png = Path.Combine(cache, guid.ToString("D") + ".png");
            var extractor = new CarThumbnailExtractor(new NullLogger());

            Assert.Equal(png, extractor.EnsurePng(src, cache));

            // Game re-rendered (repaint) after our cache: PNG gets regenerated.
            File.WriteAllText(png, "stale bytes");
            File.SetLastWriteTimeUtc(png, new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));
            File.SetLastWriteTimeUtc(src, new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc));
            Assert.Equal(png, extractor.EnsurePng(src, cache));
            Assert.Equal(0x89, File.ReadAllBytes(png)[0]); // real PNG again

            // Cache newer than the render: left untouched.
            File.WriteAllText(png, "sentinel");
            File.SetLastWriteTimeUtc(src, new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));
            Assert.Equal(png, extractor.EnsurePng(src, cache));
            Assert.Equal("sentinel", File.ReadAllText(png));
        }
        finally { Directory.Delete(root, true); }
    }
}
