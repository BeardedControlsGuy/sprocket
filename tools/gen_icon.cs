// Procedurally draws the standalone Sprocket mark (flat chainring, "Forge" gradient) and
// emits sprocket_gear.png (header art) + sprocket.ico (multi-res app/taskbar icon).
//
// Build & run:
//   csc /nologo /target:exe /out:gen_icon.exe /r:System.Drawing.dll tools\gen_icon.cs
//   .\gen_icon.exe ..\assets
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

internal static class Program
{
    // "Forge" palette - standalone Sprocket identity (no cyan/brass)
    private static readonly Color Flame = ColorTranslator.FromHtml("#dc2626");
    private static readonly Color Ember = ColorTranslator.FromHtml("#f97316");
    private static readonly Color EmberLight = ColorTranslator.FromHtml("#fdba74");

    private const int Teeth = 10;

    /// <summary>A radial tooth in local space: straight sides from innerX to (outerX-halfW),
    /// capped with a rounded tip circle, laid out along the +X axis before rotation.</summary>
    private static GraphicsPath ToothPath(float innerX, float outerX, float halfW)
    {
        GraphicsPath p = new GraphicsPath(FillMode.Winding);
        float straightEnd = outerX - halfW;
        p.AddRectangle(new RectangleF(innerX, -halfW, straightEnd - innerX, halfW * 2));
        p.AddEllipse(straightEnd - halfW, -halfW, halfW * 2, halfW * 2);
        return p;
    }

    private static Bitmap Render(int size)
    {
        Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);

            float cx = size / 2f, cy = size / 2f;
            float outerR = size * 0.46f;
            float bandR = outerR * 0.74f;
            float toothHalfW = outerR * 0.13f;
            float toothInnerX = bandR * 0.95f;
            float holeR = outerR * 0.34f;
            float studR = outerR * 0.06f;

            // 1) Silhouette = band ring union radial capsule teeth (Winding merges the overlaps)
            using (GraphicsPath silhouette = new GraphicsPath(FillMode.Winding))
            {
                silhouette.AddEllipse(cx - bandR, cy - bandR, bandR * 2, bandR * 2);
                for (int i = 0; i < Teeth; i++)
                {
                    double angle = -Math.PI / 2 + i * (2 * Math.PI / Teeth);
                    using (GraphicsPath tooth = ToothPath(toothInnerX, outerR, toothHalfW))
                    {
                        Matrix m = new Matrix();
                        m.Translate(cx, cy);
                        m.Rotate((float)(angle * 180.0 / Math.PI));
                        tooth.Transform(m);
                        silhouette.AddPath(tooth, false);
                    }
                }

                RectangleF bounds = new RectangleF(cx - outerR, cy - outerR, outerR * 2, outerR * 2);
                using (LinearGradientBrush fill = new LinearGradientBrush(
                    bounds, EmberLight, Flame, LinearGradientMode.Vertical))
                {
                    fill.Blend = new Blend(3)
                    {
                        Factors = new float[] { 0f, 0.5f, 1f },
                        Positions = new float[] { 0f, 0.45f, 1f }
                    };
                    g.FillPath(fill, silhouette);
                }
            }

            // 2) Punch the center hole out to transparent
            g.CompositingMode = CompositingMode.SourceCopy;
            using (GraphicsPath hole = new GraphicsPath())
            {
                hole.AddEllipse(cx - holeR, cy - holeR, holeR * 2, holeR * 2);
                using (SolidBrush clear = new SolidBrush(Color.FromArgb(0, 0, 0, 0)))
                    g.FillPath(clear, hole);
            }
            g.CompositingMode = CompositingMode.SourceOver;

            // 3) Center axle stud for a little mechanical detail at larger sizes
            using (SolidBrush stud = new SolidBrush(Ember))
                g.FillEllipse(stud, cx - studR, cy - studR, studR * 2, studR * 2);
        }
        return bmp;
    }

    private static void SaveIco(string path, int[] sizes)
    {
        using (FileStream fs = new FileStream(path, FileMode.Create))
        using (BinaryWriter bw = new BinaryWriter(fs))
        {
            int count = sizes.Length;
            byte[][] pngData = new byte[count][];
            for (int i = 0; i < count; i++)
            {
                using (Bitmap bmp = Render(sizes[i]))
                using (MemoryStream ms = new MemoryStream())
                {
                    bmp.Save(ms, ImageFormat.Png);
                    pngData[i] = ms.ToArray();
                }
            }

            bw.Write((short)0);
            bw.Write((short)1);
            bw.Write((short)count);

            int offset = 6 + 16 * count;
            for (int i = 0; i < count; i++)
            {
                int s = sizes[i];
                bw.Write((byte)(s >= 256 ? 0 : s));
                bw.Write((byte)(s >= 256 ? 0 : s));
                bw.Write((byte)0);
                bw.Write((byte)0);
                bw.Write((short)1);
                bw.Write((short)32);
                bw.Write(pngData[i].Length);
                bw.Write(offset);
                offset += pngData[i].Length;
            }
            for (int i = 0; i < count; i++)
                bw.Write(pngData[i]);
        }
    }

    private static void Main(string[] args)
    {
        string outDir = args.Length > 0 ? args[0] : ".";
        Directory.CreateDirectory(outDir);

        using (Bitmap header = Render(512))
            header.Save(Path.Combine(outDir, "sprocket_gear.png"), ImageFormat.Png);

        SaveIco(Path.Combine(outDir, "sprocket.ico"), new int[] { 16, 32, 48, 256 });

        Console.WriteLine("Wrote sprocket_gear.png + sprocket.ico to " + outDir);
    }
}
