using CustomVision;
using Dynamsoft.Barcode;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DJIWindowsSDKSample;
using Windows.Graphics.Imaging;
using Windows.Media;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace DJIDemo.AIModel
{
    class ProcessWithBarcode
    {
        SolidColorBrush _fillBrush = new SolidColorBrush(Windows.UI.Colors.Transparent);
        SolidColorBrush _lineBrushRed = new SolidColorBrush(Windows.UI.Colors.Red);
        SolidColorBrush _lineBrushGreen = new SolidColorBrush(Windows.UI.Colors.Green);
        double _lineThickness = 2.0;
        StorageFile file = null;
        BarcodeReader barcodeReader = null;

        public ProcessWithBarcode()
        {

            barcodeReader = new BarcodeReader();
            barcodeReader.LicenseKeys = "t0068NQAAAFRBLfIhhhvIBS4b+dGvzH2fGnlZKIqxiRG2BwIFRQy9JjeODfbvKB7VE2b0IMl7D2sTcGPhLM+6ha6WDHGdiHY=";
        }


        public async Task<TextResult[]> ProcessSoftwareBitmap(SoftwareBitmap bitmap, MainPage viewmodel)
        {



            try
            {

                //Decode barcode here
                IRandomAccessStream RAstream = new InMemoryRandomAccessStream();

                // Create an encoder with the desired format
                BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, RAstream);

                // Set the software bitmap
                encoder.SetSoftwareBitmap(bitmap);
                await encoder.FlushAsync();
                var stream = RAstream.AsStream();



                //conver stream into bytebuffer
                byte[] bytebuffer = new byte[stream.Length];
                MemoryStream ms = new MemoryStream();
                int read;
                while ((read = stream.Read(bytebuffer, 0, bytebuffer.Length)) > 0)
                {
                    ms.Write(bytebuffer, 0, read);

                }

                TextResult[] result = barcodeReader.DecodeFileInMemory(ms.ToArray(), "");

                return result;

            }
            catch (BarcodeReaderException be)
            {

               MainPage.ShowMessagePopup(be.Message);
            }
            catch (InvalidOperationException ie)
            {
                ClearResult(viewmodel);
                MainPage.ShowMessagePopup(ie.Message);
            }
            catch (Exception e)
            {

               MainPage.ShowMessagePopup(e.Message);

            }
            finally
            {
                bitmap.Dispose();
            }


            return null;
        }



        private async void ClearResult(MainPage viewmodel)
        {
            await viewmodel.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                viewmodel.GetBarCanvas().Children.Clear();
            });
        }

        private async void updateresult(TextResult[] result, MainPage viewmodel, uint pixelwidth, uint pixelheight)
        {
            try
            {


                await viewmodel.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    var overlaycanvas = viewmodel.GetBarCanvas();

                    var videoactualwidth = viewmodel.GetImage().ActualWidth;
                    var videoactualheight = viewmodel.GetImage().ActualHeight;


                    overlaycanvas.Children.Clear();

                    int i = 0;
                    foreach (var res in result)
                    {



                        var TLPoint = res.LocalizationResult.ResultPoints[0];
                        var TRPoint = res.LocalizationResult.ResultPoints[1];
                        var BRPoint = res.LocalizationResult.ResultPoints[2];
                        var BLPoint = res.LocalizationResult.ResultPoints[3];


                        string type = result[i].BarcodeFormat.ToString();

                        var barcodeText = "\n" + " Text : " + res.BarcodeText + "\n";

                        double x = TLPoint.X / 1280.0;
                        double y = TLPoint.Y / 960.0;
                        double w = Math.Abs((TRPoint.X - TLPoint.X)) / 1280.0;
                        double h = Math.Abs((BLPoint.Y - TLPoint.Y)) / 960.0;

                        string boxTest = type;



                        x = videoactualwidth * x;
                        y = videoactualheight * y;
                        w = videoactualwidth * w;
                        h = videoactualheight * h;


                        var rectStroke = boxTest == "person" ? _lineBrushGreen : _lineBrushRed;

                        var r = new Windows.UI.Xaml.Shapes.Rectangle
                        {
                            //Tag = box,
                            Width = w,
                            Height = h,
                            Fill = _fillBrush,
                            Stroke = rectStroke,
                            StrokeThickness = _lineThickness,
                            Margin = new Thickness(x, y, 0, 0)
                        };



                        var tb = new TextBlock
                        {
                            Margin = new Thickness(x + 4, y + 4, 0, 0),
                            Text = $"{boxTest}  {barcodeText}",

                            FontWeight = FontWeights.Bold,

                            HorizontalTextAlignment = TextAlignment.Center
                        };

                        overlaycanvas.Children.Add(tb);
                        overlaycanvas.Children.Add(r);

                    }

                    i++;

                });
            }
            catch (Exception ex)
            {
                MainPage.ShowMessagePopup(ex.Message);

            }

        }






    }
}
