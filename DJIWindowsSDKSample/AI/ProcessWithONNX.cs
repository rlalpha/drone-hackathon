using CustomVision;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DJIWindowsSDKSample.ViewModels;
using DJIWindowsSDKSample;

using Windows.AI.MachineLearning;
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
    class ProcessWithONNX
    {
        SolidColorBrush _fillBrush = new SolidColorBrush(Windows.UI.Colors.Transparent);
        SolidColorBrush _lineBrushRed = new SolidColorBrush(Windows.UI.Colors.Red);
        SolidColorBrush _lineBrushGreen = new SolidColorBrush(Windows.UI.Colors.Green);
        double _lineThickness = 2.0;
        StorageFile file = null;

        ObjectDetection objectDetection;



        public ProcessWithONNX()
        {
            /// <summary>
            ///     [1] ___________ Modify code here __________
            ///     Copy obj.name to labels
            /// 
            /// </summary>
            List<String> labels = new List<String> {
                "Box",
                "Circle",
                "Doublearrow",
                "Downarrow",
                "Nobox",
                "QR",
                "Square",
                "Star",
                "Triangle"
                };
            /// <summary>
            ///     [2] ___________ Modify code here __________
            ///     ObjectDetection Usage:
            ///     ObjectDetection( labels, maxDetection, probabilityThreshold, iouThreshold)
            ///         maxDetection: 
            ///            The maximum number of Objects to Detect.
            ///         probabilityThreshold:
            ///             Remove the detections with probability below the threshold.
            ///          iouThreshold:
            ///             Remove the detections with overlay area above the threshold.
            /// </summary>
            objectDetection = new ObjectDetection(labels, 20, 0.65F, 0.45F);

        }

        public async Task<IList<PredictionModel>> ProcessSoftwareBitmap(SoftwareBitmap bitmap, MainPage viewmodel)
        {
            if (!Windows.Foundation.Metadata.ApiInformation.IsMethodPresent("Windows.Media.VideoFrame", "CreateWithSoftwareBitmap"))
            {
                return null;
            }
            IList<PredictionModel> output = null;
            //Convert SoftwareBitmap  into VideoFrame
            using (VideoFrame frame = VideoFrame.CreateWithSoftwareBitmap(bitmap))
            {

                try
                {
                    if (file == null)
                    {
                        file = await Windows.Storage.StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///AI/model.onnx"));
                        await objectDetection.Init(file);
                    }

                    output = await objectDetection.PredictImageAsync(frame);

                    //Debug.WriteLine(output);
                    if (output != null)
                    {
                        UpdateResult(output, viewmodel);
                    }


                }
                catch (Exception e)
                {
                    string s = e.Message;
                    MainPage.ShowMessagePopup(e.Message);
                }

            }

            return output;
        }

        private async void UpdateResult(IList<PredictionModel> outputlist, MainPage viewmodel)
        {
            try
            {
                //viewmodel.GetCanvas().Children.Clear();
                await viewmodel.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    var overlayCanvas = viewmodel.GetCanvas();

                    var VideoActualWidth = (uint)viewmodel.GetImage().ActualWidth;
                    var VideoActualHeight = (uint)viewmodel.GetImage().ActualHeight;
                    
                    overlayCanvas.Children.Clear();
                                       
                    foreach (var output in outputlist)
                    {

                        var box = output.BoundingBox;

                        //strdebuglog += "x=" + box.X + "    y=" + box.Y + "   with=" + box.Width + "    height=" + box.Height + "\n";

                        // process output boxes
                        double x = (double)Math.Max(box.Left, 0);
                        double y = (double)Math.Max(box.Top, 0);
                        double w = (double)Math.Min(1 - x, box.Width);
                        double h = (double)Math.Min(1 - y, box.Height);

                        string boxTest = output.TagName;

                        x = VideoActualWidth * x;
                        y = VideoActualHeight * y;
                        w = VideoActualWidth * w;
                        h = VideoActualHeight * h;

                        var rectStroke = _lineBrushRed;

                        var r = new Windows.UI.Xaml.Shapes.Rectangle
                        {
                            Tag = box,
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
                            Text = $"{boxTest} ({Math.Round(output.Probability, 4)})",
                            FontWeight = FontWeights.Bold,
                            Width = 126,
                            Height = 21,
                            HorizontalTextAlignment = TextAlignment.Center
                        };

                        var textBack = new Windows.UI.Xaml.Shapes.Rectangle
                        {
                            Width = 134,
                            Height = 29,
                            Fill = rectStroke,
                            Margin = new Thickness(x, y, 0, 0)
                        };

                        overlayCanvas.Children.Add(textBack);
                        overlayCanvas.Children.Add(tb);
                        overlayCanvas.Children.Add(r);

                    }
                    // viewmodel.GetMainPage().GetDebugLog().Text = strdebuglog;
                });

            }
            catch (Exception ex)
            {
               MainPage.ShowMessagePopup(ex.Message);

            }

        }




    }
}