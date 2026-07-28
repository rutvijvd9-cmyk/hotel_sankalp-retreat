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

public class IcoHelper
{
    public static void CreateIco(string pngPath, string icoPath)
    {
        using (var srcBmp = new Bitmap(pngPath))
        {
            int[] sizes = { 16, 32, 48, 256 };
            byte[][] pngBlobs = new byte[sizes.Length][];
            
            for (int i = 0; i < sizes.Length; i++)
            {
                int sz = sizes[i];
                using (var destBmp = new Bitmap(sz, sz))
                using (var g = Graphics.FromImage(destBmp))
                {
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.Clear(Color.Transparent);
                    
                    g.DrawImage(srcBmp, 0, 0, sz, sz);
                    
                    using (var ms = new MemoryStream())
                    {
                        destBmp.Save(ms, ImageFormat.Png);
                        pngBlobs[i] = ms.ToArray();
                    }
                }
            }
            
            using (var fs = new FileStream(icoPath, FileMode.Create))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write((ushort)0);
                bw.Write((ushort)1);
                bw.Write((ushort)sizes.Length);
                
                int dataOffset = 6 + (sizes.Length * 16);
                
                for (int i = 0; i < sizes.Length; i++)
                {
                    int sz = sizes[i];
                    byte[] blob = pngBlobs[i];
                    
                    bw.Write((byte)(sz == 256 ? 0 : sz));
                    bw.Write((byte)(sz == 256 ? 0 : sz));
                    bw.Write((byte)0);
                    bw.Write((byte)0);
                    bw.Write((ushort)1);
                    bw.Write((ushort)32);
                    bw.Write((uint)blob.Length);
                    bw.Write((uint)dataOffset);
                    
                    dataOffset += blob.Length;
                }
                
                for (int i = 0; i < sizes.Length; i++)
                {
                    bw.Write(pngBlobs[i]);
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
