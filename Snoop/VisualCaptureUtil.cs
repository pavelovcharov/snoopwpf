// (c) Copyright Cory Plotts.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Snoop {
    internal class VisualCaptureUtil {
        const double BaseDpi = 96;

        public static void SaveVisual(Visual visual, int dpi, string filename) {
            // sometimes RenderTargetBitmap doesn't render the Visual or doesn't render the Visual properly
            // below i am using the trick that jamie rodriguez posted on his blog
            // where he wraps the Visual inside of a VisualBrush and then renders it.
            // http://blogs.msdn.com/b/jaimer/archive/2009/07/03/rendertargetbitmap-tips.aspx

            if (visual == null)
                return;

            Rect bounds;
            var uiElement = visual as UIElement;
            if (uiElement != null) {
                bounds = new Rect(new Size((int) uiElement.RenderSize.Width, (int) uiElement.RenderSize.Height));
            }
            else {
                bounds = VisualTreeHelper.GetDescendantBounds(visual);
            }

            var sizeFactor = dpi/BaseDpi;
            var rtb =
                new RenderTargetBitmap
                    (
                    (int) (bounds.Width*sizeFactor),
                    (int) (bounds.Height*sizeFactor),
                    dpi,
                    dpi,
                    PixelFormats.Pbgra32
                    );

            var dv = new DrawingVisual();
            using (var ctx = dv.RenderOpen()) {
                var vb = new VisualBrush(visual);
                ctx.DrawRectangle(vb, null, new Rect(new Point(), bounds.Size));
            }
            rtb.Render(dv);

            SaveRTBAsPNG(rtb, filename);
        }

        static void SaveRTBAsPNG(RenderTargetBitmap bitmap, string filename) {
            var pngBitmapEncoder = new PngBitmapEncoder();
            pngBitmapEncoder.Frames.Add(BitmapFrame.Create(bitmap));

            using (var fileStream = File.Create(filename))
                pngBitmapEncoder.Save(fileStream);
        }
    }
}