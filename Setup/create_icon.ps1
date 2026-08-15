# create_icon.ps1
# Compiles and executes a native C# utility to generate a standard compliant multi-resolution ICO file.
# Output: HotelAutomation.Worker\hotel.ico

param(
    [string]$SourcePngPath = ""
)

if (-not $SourcePngPath -or -not (Test-Path $SourcePngPath)) {
    Write-Host "Source PNG path is required." -ForegroundColor Red
    exit 1
}

$outputPath = Join-Path (Split-Path $PSScriptRoot -Parent) "HotelAutomation.Worker\hotel.ico"

$code = @"
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

public class IcoHelper
{
    public static void CreateIco(string pngPath, string icoPath)
    {
        using (var srcBmp = new Bitmap(pngPath))
        {
            int[] sizes = { 16, 32, 48, 64, 128, 256 };
            byte[][] blobs = new byte[sizes.Length][];

            for (int i = 0; i < sizes.Length; i++)
            {
                int sz = sizes[i];
                using (var destBmp = new Bitmap(sz, sz, PixelFormat.Format32bppArgb))
                {
                    using (var g = Graphics.FromImage(destBmp))
                    {
                        g.SmoothingMode = SmoothingMode.HighQuality;
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.Clear(Color.Transparent);
                        g.DrawImage(srcBmp, 0, 0, sz, sz);
                    }

                    if (sz == 256)
                    {
                        using (var ms = new MemoryStream())
                        {
                            destBmp.Save(ms, ImageFormat.Png);
                            blobs[i] = ms.ToArray();
                        }
                    }
                    else
                    {
                        using (var ms = new MemoryStream())
                        using (var bw = new BinaryWriter(ms))
                        {
                            // BITMAPINFOHEADER (40 bytes)
                            bw.Write((uint)40); // biSize
                            bw.Write((int)sz);   // biWidth
                            bw.Write((int)(sz * 2)); // biHeight (XOR + AND)
                            bw.Write((ushort)1); // biPlanes
                            bw.Write((ushort)32); // biBitCount
                            bw.Write((uint)0); // biCompression (BI_RGB)
                            bw.Write((uint)(sz * sz * 4)); // biSizeImage
                            bw.Write((int)0);
                            bw.Write((int)0);
                            bw.Write((uint)0);
                            bw.Write((uint)0);

                            // Write BGRA pixel data bottom-to-top
                            BitmapData data = destBmp.LockBits(
                                new Rectangle(0, 0, sz, sz),
                                ImageLockMode.ReadOnly,
                                PixelFormat.Format32bppArgb);

                            byte[] pixelBuffer = new byte[data.Stride * sz];
                            Marshal.Copy(data.Scan0, pixelBuffer, 0, pixelBuffer.Length);
                            destBmp.UnlockBits(data);

                            for (int y = sz - 1; y >= 0; y--)
                            {
                                int rowStart = y * data.Stride;
                                bw.Write(pixelBuffer, rowStart, sz * 4);
                            }

                            // AND mask (all zeros for 32bpp alpha)
                            int maskRowLen = ((sz + 31) / 32) * 4;
                            byte[] maskBytes = new byte[maskRowLen * sz];
                            bw.Write(maskBytes);

                            blobs[i] = ms.ToArray();
                        }
                    }
                }
            }

            using (var fs = new FileStream(icoPath, FileMode.Create))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write((ushort)0);
                bw.Write((ushort)1); // Icon type
                bw.Write((ushort)sizes.Length);

                int dataOffset = 6 + (sizes.Length * 16);

                for (int i = 0; i < sizes.Length; i++)
                {
                    int sz = sizes[i];
                    byte[] blob = blobs[i];

                    bw.Write((byte)(sz >= 256 ? 0 : sz));
                    bw.Write((byte)(sz >= 256 ? 0 : sz));
                    bw.Write((byte)0); // Color palette
                    bw.Write((byte)0); // Reserved
                    bw.Write((ushort)1); // Color planes
                    bw.Write((ushort)32); // Bits per pixel
                    bw.Write((uint)blob.Length);
                    bw.Write((uint)dataOffset);

                    dataOffset += blob.Length;
                }

                for (int i = 0; i < sizes.Length; i++)
                {
                    bw.Write(blobs[i]);
                }
            }
        }
    }
}
"@

try {
    Add-Type -TypeDefinition $code -ReferencedAssemblies System.Drawing -ErrorAction SilentlyContinue
    [IcoHelper]::CreateIco($SourcePngPath, $outputPath)
    Write-Host "Created valid multi-resolution ICO at $outputPath" -ForegroundColor Green
} catch {
    Write-Host "ERROR: $_" -ForegroundColor Red
    exit 1
}
