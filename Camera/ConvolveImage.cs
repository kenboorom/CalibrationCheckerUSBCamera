using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Imaging;


namespace Camera
{
    class ConvolveImage
    {
        public int Factor = 1;
        public int Height, Width;
        public int Offset = 0;
        public int[,] Arr;
        public int Size;
    }

    class DoConvolve
    {
        Bitmap Conv3x3(Bitmap b, ConvolveImage m)
        {
            if (0 == m.Factor)
                return b;

            Bitmap bSrc = (Bitmap)b.Clone();
            BitmapData bmData = b.LockBits(new Rectangle(0, 0, b.Width, b.Height),
                                ImageLockMode.ReadWrite,
                                PixelFormat.Format24bppRgb);
            BitmapData bmSrc = bSrc.LockBits(new Rectangle(0, 0, bSrc.Width, bSrc.Height),
                               ImageLockMode.ReadWrite,
                               PixelFormat.Format24bppRgb);
            int stride = bmData.Stride;

            System.IntPtr Scan0 = bmData.Scan0;
            System.IntPtr SrcScan0 = bmSrc.Scan0;

            unsafe
            {
                byte* p = (byte*)(void*)Scan0;
                byte* pSrc = (byte*)(void*)SrcScan0;
                int nOffset = stride - b.Width * m.Width;
                int nWidth = b.Width - (m.Size - 1);
                int nHeight = b.Height - (m.Size - 2);

                int nPixel = 0;

                for (int y = 0; y < nHeight; y++)
                {
                    for (int x = 0; x < nWidth; x++)
                    {
                        for (int r = 0; r < m.Height; r++)
                        {
                            nPixel = 0;
                            for (int i = 0; i < m.Width; i++)
                                for (int j = 0; j < m.Height; j++)
                                {
                                    nPixel += (pSrc[(m.Width * (i + 1)) - 1 - r + stride * j] * m.Arr[j, i]);
                                }

                            nPixel /= m.Factor;
                            nPixel += m.Offset;

                            if (nPixel < 0) nPixel = 0;
                            if (nPixel > 255) nPixel = 255;
                            p[(m.Width * (m.Height / 2 + 1)) - 1 - r + stride * (m.Height / 2)] = (byte)nPixel;
                        }
                        p += m.Width;
                        pSrc += m.Width;
                    }
                    p += nOffset;
                    pSrc += nOffset;
                }
            }

            b.UnlockBits(bmData);
            bSrc.UnlockBits(bmSrc);
            return b;
        }
    }

}
