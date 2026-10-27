using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DiscordPresence;

internal static class IconGen
{
    private static readonly int[] Sizes = { 16, 20, 24, 32, 40, 48, 64, 96, 128, 256 };

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            string root = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
            string assets = Path.Combine(root, "assets");
            Directory.CreateDirectory(assets);

            List<KeyValuePair<int, byte[]>> frames = new List<KeyValuePair<int, byte[]>>();
            foreach (int size in Sizes)
            {
                BitmapSource image = Render(size);
                // PNG compression for the large frames, classic DIBs for the small ones.
                byte[] data = size <= 64 ? EncodeDib(image) : EncodePng(image);
                frames.Add(new KeyValuePair<int, byte[]>(size, data));
            }

            WriteIco(Path.Combine(assets, "discord.ico"), frames);
            SavePng(Render(512), Path.Combine(assets, "discord-icon.png"));

            Console.WriteLine("Wrote assets/discord.ico and assets/discord-icon.png");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static BitmapSource Render(int size)
    {
        DrawingVisual visual = new DrawingVisual();
        using (DrawingContext dc = visual.RenderOpen())
        {
            double radius = size * 0.225;
            dc.DrawRoundedRectangle(
                new SolidColorBrush(Color.FromRgb(0x72, 0x89, 0xDA)),
                null,
                new Rect(0, 0, size, size),
                radius,
                radius);

            Geometry mark = Brand.MarkGeometry;
            Rect bounds = mark.Bounds;
            double scale = (size * 0.66) / bounds.Width;
            double x = (size - bounds.Width * scale) / 2.0 - bounds.X * scale;
            double y = (size - bounds.Height * scale) / 2.0 - bounds.Y * scale;

            dc.PushTransform(new MatrixTransform(scale, 0, 0, scale, x, y));
            dc.DrawGeometry(Brushes.White, null, mark);
            dc.Pop();
        }

        RenderTargetBitmap bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private static byte[] EncodePng(BitmapSource image)
    {
        PngBitmapEncoder encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using (MemoryStream stream = new MemoryStream())
        {
            encoder.Save(stream);
            return stream.ToArray();
        }
    }

    private static byte[] EncodeDib(BitmapSource image)
    {
        int width = image.PixelWidth;
        int height = image.PixelHeight;
        int stride = width * 4;

        BitmapSource converted = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
        byte[] pixels = new byte[stride * height];
        converted.CopyPixels(pixels, stride, 0);

        using (MemoryStream stream = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            writer.Write(40);                 // biSize
            writer.Write(width);              // biWidth
            writer.Write(height * 2);         // biHeight (XOR + AND)
            writer.Write((short)1);           // biPlanes
            writer.Write((short)32);          // biBitCount
            writer.Write(0);                  // biCompression
            writer.Write(stride * height);    // biSizeImage
            writer.Write(0);                  // biXPelsPerMeter
            writer.Write(0);                  // biYPelsPerMeter
            writer.Write(0);                  // biClrUsed
            writer.Write(0);                  // biClrImportant

            for (int y = height - 1; y >= 0; y--)
            {
                writer.Write(pixels, y * stride, stride);
            }

            int maskStride = ((width + 31) / 32) * 4;
            writer.Write(new byte[maskStride * height]);
            writer.Flush();
            return stream.ToArray();
        }
    }

    private static void SavePng(BitmapSource image, string path)
    {
        using (FileStream stream = File.Create(path))
        {
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            encoder.Save(stream);
        }
    }

    private static void WriteIco(string path, List<KeyValuePair<int, byte[]>> frames)
    {
        using (FileStream stream = File.Create(path))
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            writer.Write((ushort)0);               // reserved
            writer.Write((ushort)1);               // icon
            writer.Write((ushort)frames.Count);

            int offset = 6 + 16 * frames.Count;
            foreach (KeyValuePair<int, byte[]> frame in frames)
            {
                int size = frame.Key;
                writer.Write((byte)(size >= 256 ? 0 : size));
                writer.Write((byte)(size >= 256 ? 0 : size));
                writer.Write((byte)0);             // palette count
                writer.Write((byte)0);             // reserved
                writer.Write((ushort)1);           // planes
                writer.Write((ushort)32);          // bpp
                writer.Write((uint)frame.Value.Length);
                writer.Write((uint)offset);
                offset += frame.Value.Length;
            }

            foreach (KeyValuePair<int, byte[]> frame in frames)
            {
                writer.Write(frame.Value);
            }
        }
    }
}
