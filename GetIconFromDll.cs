using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace WilliamForero_ApiRevit_II
{
    internal class GetIconFromDll
    {
        internal static BitmapSource GetEmbeddedImage(string name)
        {
            try
            {
                System.Reflection.Assembly a = System.Reflection.Assembly.GetExecutingAssembly();
                using (Stream s = a.GetManifestResourceStream(name))
                {
                    if (s == null) return null;

                    // Se carga el flujo de la imagen
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = s;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();

                    // se reescala la imagen
                    int pixelWidth = bitmap.PixelWidth;
                    int pixelHeight = bitmap.PixelHeight;

                    System.Windows.Media.PixelFormat format = System.Windows.Media.PixelFormats.Bgra32;
                    int stride = pixelWidth * ((format.BitsPerPixel + 7) / 8);
                    byte[] pixels = new byte[pixelHeight * stride];
                    bitmap.CopyPixels(pixels, stride, 0);

                    BitmapSource resultadoCorregido = BitmapSource.Create(
                        pixelWidth, pixelHeight, 96, 96, format, null, pixels, stride);

                    resultadoCorregido.Freeze();
                    return resultadoCorregido;
                }
            }
            catch
            {
                return null;
            }
        }
    }
}