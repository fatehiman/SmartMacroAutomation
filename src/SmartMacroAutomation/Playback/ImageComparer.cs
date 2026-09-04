using System.Drawing;
using System.Drawing.Imaging;

namespace SmartMacroAutomation.Playback;

internal static class ImageComparer
{
    /// <summary>
    /// Simple per-pixel similarity: the percentage of pixels whose RGB color
    /// is within a small tolerance of the reference image's pixel at the same position.
    /// Images of different sizes are treated as 0% similar.
    /// </summary>
    public static double CalculateSimilarity(Bitmap reference, Bitmap current, int colorTolerance = 24)
    {
        if (reference.Width != current.Width || reference.Height != current.Height)
            return 0.0;

        int width = reference.Width;
        int height = reference.Height;

        var refData = reference.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var curData = current.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

        try
        {
            int total = width * height;
            int matched = 0;
            int strideR = refData.Stride;
            int strideC = curData.Stride;

            unsafe
            {
                byte* pRef = (byte*)refData.Scan0;
                byte* pCur = (byte*)curData.Scan0;

                for (int y = 0; y < height; y++)
                {
                    byte* rowRef = pRef + y * strideR;
                    byte* rowCur = pCur + y * strideC;
                    for (int x = 0; x < width; x++)
                    {
                        int i = x * 4;
                        int db = rowRef[i] - rowCur[i];
                        int dg = rowRef[i + 1] - rowCur[i + 1];
                        int dr = rowRef[i + 2] - rowCur[i + 2];

                        if (Math.Abs(db) <= colorTolerance && Math.Abs(dg) <= colorTolerance && Math.Abs(dr) <= colorTolerance)
                            matched++;
                    }
                }
            }

            return total == 0 ? 100.0 : matched * 100.0 / total;
        }
        finally
        {
            reference.UnlockBits(refData);
            current.UnlockBits(curData);
        }
    }
}
