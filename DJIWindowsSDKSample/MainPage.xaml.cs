using System;
using System.Threading.Tasks;
using System.Runtime.InteropServices.WindowsRuntime;
using DJI.WindowsSDK;
using Windows.UI.Popups;
using Windows.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using System.Collections.Generic;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Controls;
using DJIWindowsSDKSample.ViewModels;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using System.Diagnostics;
using DJIWindowsSDKSample.Controls;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using DJIDemo.AIModel;
using CustomVision;
using Windows.UI.Text;
using System.Linq;

// key: 98e7d89e54ea919a02881ef2

namespace DJIWindowsSDKSample
{
    public sealed partial class MainPage : Page
    {
        private DJIVideoParser.Parser videoParser;
        public WriteableBitmap VideoSource;

        static private bool firstwrite = true;
        static private HashSet<string> locationID = new HashSet<string>();

        //Worker task (thread) for reading barcode
        //As reading barcode is computationally expensive
        private Task readerWorker = null;
        private ISet<string> readed = new HashSet<string>();

        private object bufLock = new object();
        //these properties are guarded by bufLock
        private int width, height;
        private byte[] decodedDataBuf;

        private float throttle = 0;
        private float roll = 0;
        private float pitch = 0;
        private float yaw = 0;
        private float his_height = 1.2f;
        private float cur_height = 1.2f;
        public const float moveVelocity = 0.1f;
        public float diff_height = 0.41f;
        private float init_yaw = 0;
        private float cur_yaw = 0;
        private float hist_yaw = 0;
        public bool flag_yaw = true;
        public bool started_auto = false;
        public int down_arrow_count = 0;
        public int up_arrow_count = 0;


        private static string csv_datat = "";

        public MainPage()
        {
            this.InitializeComponent();
            DataContext = new ComponentViewModel();
            //Listen for registration success
            DJISDKManager.Instance.SDKRegistrationStateChanged += async (state, result) =>
            {
                if (state != SDKRegistrationState.Succeeded)
                {
                    var md = new MessageDialog(result.ToString());
                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () => await md.ShowAsync());
                    return;
                }
                //wait till initialization finish
                //use a large enough time and hope for the best
                await Task.Delay(1000);
                videoParser = new DJIVideoParser.Parser();
                videoParser.Initialize();
                videoParser.SetVideoDataCallack(0, 0, ReceiveDecodedData);
                DJISDKManager.Instance.VideoFeeder.GetPrimaryVideoFeed(0).VideoDataUpdated += OnVideoPush;

                //Disable Obstacle Avoidance
                await DJISDKManager.Instance.ComponentManager.GetFlightAssistantHandler(0, 0).SetObstacleAvoidanceEnabledAsync(new BoolMsg() { value = false });
                

                await Task.Delay(5000);
                //Reset Gimbal
                GimbalResetCommandMsg resetMsg = new GimbalResetCommandMsg() { value = GimbalResetCommand.UNKNOWN };
                await DJISDKManager.Instance.ComponentManager.GetGimbalHandler(0, 0).ResetGimbalAsync(resetMsg);

                //Camera Focus Mode [MANUAL, AF, AFC, UNKNOWN]
                CameraFocusModeMsg autofocus = new CameraFocusModeMsg() { value = CameraFocusMode.AFC};
                await DJISDKManager.Instance.ComponentManager.GetCameraHandler(0, 0).SetCameraFocusModeAsync(autofocus);

                //Gimbal Angle Change
                GimbalAngleRotation rotation = new GimbalAngleRotation()
                {
                    mode = GimbalAngleRotationMode.ABSOLUTE_ANGLE,
                    pitch = 60,
                    roll = 45,
                    yaw = 45,
                    pitchIgnored = false,
                    yawIgnored = false,
                    rollIgnored = false,
                    duration = 0.5
                };
                await DJISDKManager.Instance.ComponentManager.GetGimbalHandler(0, 0).RotateByAngleAsync(rotation);

            };
            DJISDKManager.Instance.RegisterApp("98e7d89e54ea919a02881ef2");
            changeTransitionState(true, 0);
            //init_yaw = cur_yaw;
            LoadMainPage(this);
        }

        public static void LoadMainPage(MainPage M)
        {
            Mainpage = M;
            processWithONNX = new ProcessWithONNX();
            processWithBarcode = new ProcessWithBarcode();
            BarcodePairList = new Dictionary<string, string>();
        }

        void OnVideoPush(VideoFeed sender, ref byte[] bytes)
        {
            videoParser.PushVideoData(0, 0, bytes, bytes.Length);
        }

        static MainPage Mainpage = null;
        static ProcessWithONNX processWithONNX = null;
        static private ProcessWithBarcode processWithBarcode = null;
        static public Dictionary<string, string> BarcodePairList = null;
        static StorageFile StorageFileObject = null;
        static private int state = 1;
        static private bool transition = false;
        static private int tran_state = 0;

        void BarcodeWorker()
        {
            readerWorker = new Task(async () =>
            {
                SoftwareBitmap bitmap;
                //ShowMessagePopup("ONNX Start");
                while (true)
                {
                    
                    try
                    {
                        lock (bufLock)
                        {
                            bitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, width, height);
                            bitmap.CopyFromBuffer(decodedDataBuf.AsBuffer());
                        }
                    }
                    catch
                    {
                        //the size maybe incorrect due to unknown reason
                        await Task.Delay(10);     
                        continue;
                    }

                    try
                    {
                        if (transition == true)
                        {
                            Debug.WriteLine("~~~~~~~~~~~~~~~~~~~~~~~~~~Transition is on");
                            handleTransitionAsync(this);
                        }
                        else
                        {
                            IList<PredictionModel> outputlist = null;
                            if (bitmap.PixelHeight != bitmap.PixelWidth)
                            {
                                try
                                {
                                    int destWidthAndHeight = 416;
                                    using (var resourceCreator = CanvasDevice.GetSharedDevice())
                                    using (var canvasBitmap = CanvasBitmap.CreateFromSoftwareBitmap(resourceCreator, bitmap))
                                    using (var canvasRenderTarget = new CanvasRenderTarget(resourceCreator, destWidthAndHeight, destWidthAndHeight, canvasBitmap.Dpi))
                                    using (var drawingSession = canvasRenderTarget.CreateDrawingSession())
                                    using (var scaleEffect = new ScaleEffect())
                                    {
                                        scaleEffect.Source = canvasBitmap;
                                        scaleEffect.Scale = new System.Numerics.Vector2((float)destWidthAndHeight / (float)bitmap.PixelWidth, (float)destWidthAndHeight / (float)bitmap.PixelHeight);
                                        drawingSession.DrawImage(scaleEffect);
                                        drawingSession.Flush();

                                        var sbp = SoftwareBitmap.CreateCopyFromBuffer(canvasRenderTarget.GetPixelBytes().AsBuffer(), BitmapPixelFormat.Bgra8, destWidthAndHeight, destWidthAndHeight, BitmapAlphaMode.Premultiplied);

                                        outputlist = await processWithONNX.ProcessSoftwareBitmap(sbp, Mainpage);
                                    }
                                }
                                catch
                                {
                                    outputlist = await processWithONNX.ProcessSoftwareBitmap(bitmap, Mainpage);
                                }
                            }
                            else
                            {
                                outputlist = await processWithONNX.ProcessSoftwareBitmap(bitmap, Mainpage);
                            }

                            if (state == 1)
                            {
                                roll = moveVelocity;
                                DJISDKManager.Instance.VirtualRemoteController.UpdateJoystickValue(throttle, yaw, pitch, roll);
                            }
                            else if (state == -1)
                            {
                                roll = -moveVelocity;
                                DJISDKManager.Instance.VirtualRemoteController.UpdateJoystickValue(throttle, yaw, pitch, roll);
                            }

                            if (outputlist != null)
                            {

                                bool bListChangeFlag = false;
                                foreach (var output in outputlist)
                                {
                                    double x = (double)Math.Max(output.BoundingBox.Left, 0);
                                    double y = (double)Math.Max(output.BoundingBox.Top, 0);
                                    double w = (double)Math.Min(1 - x, output.BoundingBox.Width);
                                    double h = (double)Math.Min(1 - y, output.BoundingBox.Height);

                                    var horiz_center = (x + w) - (w / 2);
                                    var verti_center = (y + h) - (h / 2);

                                    var bitmap4barcode = await GetCroppedBitmapAsync(bitmap, (uint)(x * bitmap.PixelWidth), (uint)(y * bitmap.PixelHeight), (uint)(w * bitmap.PixelWidth), (uint)(h * bitmap.PixelHeight));
                                    var barcodeoutput = await processWithBarcode.ProcessSoftwareBitmap(bitmap4barcode, Mainpage);

                                    bitmap4barcode.Dispose();

                                    //bitmap.Dispose();

                                    Debug.WriteLine("~~~~~~~~~~~~~~~~~~~~~~~~~~Transition is off");
                                    switch (output.TagName)
                                    {
                                        case "Nobox":
                                            if (barcodeoutput.Length == 1 && barcodeoutput.First().BarcodeFormat == Dynamsoft.Barcode.EnumBarcodeFormat.QR_CODE)
                                            {
                                                var result = barcodeoutput.First().BarcodeText;
                                                if (!BarcodePairList.ContainsKey(result))
                                                {
                                                    if (result.StartsWith("L") == true)
                                                    {
                                                        BarcodePairList.Add(result, "");
                                                        bListChangeFlag = true;
                                                    }
                                                }
                                            }
                                            break;
                                        case "Box":
                                            if (barcodeoutput.Length == 2)
                                            {
                                                var itemfirst = barcodeoutput.First();
                                                var itemlast = barcodeoutput.Last();
                                                string value = "";

                                                var val1 = itemfirst.BarcodeText.ToString();
                                                var val2 = itemlast.BarcodeText.ToString();

                                                if (val1.StartsWith("L") == true && val2.StartsWith("0"))
                                                {
                                                    BarcodePairList.Add(val1, val2);
                                                    bListChangeFlag = true;

                                                }
                                                else if (val2.StartsWith("L") == true && val1.StartsWith("0"))
                                                {
                                                    BarcodePairList.Add(val2, val1);
                                                    bListChangeFlag = true;
                                                }
                                            }
                                            break;

                                        case "QR":
                                            if (barcodeoutput.Length == 1 && barcodeoutput.First().BarcodeFormat == Dynamsoft.Barcode.EnumBarcodeFormat.QR_CODE)
                                            {
                                                var result = barcodeoutput.First().BarcodeText;
                                                if (result == "down")
                                                {
                                                    changeTransitionState(true, 1);
                                                    state *= -1;
                                                    down_arrow_count += 1;
                                                }
                                                else if (result == "turn" && down_arrow_count >= 2)
                                                {
                                                    roll = 0;
                                                    DJISDKManager.Instance.VirtualRemoteController.UpdateJoystickValue(throttle, yaw, pitch, roll);
                                                    hist_yaw = cur_yaw;
                                                    changeTransitionState(true, 2);
                                                }
                                                else if (result == "up")
                                                {
                                                    diff_height = 0.35f;
                                                    changeTransitionState(true, 3);
                                                    state *= -1;
                                                    up_arrow_count += 1;
                                                }
                                                else if (result == "land" && up_arrow_count >= 2)
                                                {
                                                    changeTransitionState(true, 4);
                                                    state *= -1;
                                                    //down_arrow_count += 1;
                                                }

                                            }
        
                                            break;
                                        /* case "Downarrow": //
                                            if (down_arrow_count < 2) {
                                                changeTransitionState(true, 1);
                                                state *= -1;
                                                down_arrow_count += 1;
                                            }
                                            break; */
                                        case "DoubleArrow":
                                            throttle = 0;
                                            roll = 0;
                                            DJISDKManager.Instance.VirtualRemoteController.UpdateJoystickValue(throttle, yaw, pitch, roll);
                                            break;
                                        default:
                                            continue;
                                    }

                                }

                                if (bListChangeFlag)
                                {
                                    await UpdateBarcodeDictionary();
                                }
                            }
                            else
                            {
                                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                                {
                                    GetCanvas().Children.Clear();
                                });
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        ShowMessagePopup(e.Message);
                    }
                    finally
                    {
                        bitmap.Dispose();
                    }

                   // check_state_qrcode();

                    Get_height();
                    Get_IMU();
                }
            });
          
        }

       /* private void check_state_qrcode() {
            var bitmap4barcode = await GetCroppedBitmapAsync(bitmap, (uint)(x * bitmap.PixelWidth), (uint)(y * bitmap.PixelHeight), (uint)(w * bitmap.PixelWidth), (uint)(h * bitmap.PixelHeight));
            var barcodeoutput = await processWithBarcode.ProcessSoftwareBitmap(bitmap4barcode, Mainpage);
        } */

        private static async Task handleTransitionAsync(MainPage obj) {
            switch (tran_state) {
                case 1:
                    obj.moveDown();
                    if ( (obj.his_height - obj.cur_height) >= obj.diff_height || obj.cur_height <= 0.4f) {
                        changeTransitionState(false, 0);
                        obj.his_height = obj.cur_height;
                        obj.stopMoveUpOrDown();
                        obj.diff_height += 0.1f;
                    }
                    
                    Debug.WriteLine("his: " + obj.his_height);
                    Debug.WriteLine("Cur " + obj.cur_height);
                    break;
                case 2:
                    obj.turn();
                    if (obj.init_yaw > -180 && obj.init_yaw < 0)
                    {
                        float diff = (obj.cur_yaw - obj.init_yaw);
                        Debug.WriteLine("Cur: " + obj.cur_yaw);
                        Debug.WriteLine("Init: " + obj.init_yaw);
                        Debug.WriteLine("Diff: " + diff);
                        if (diff >= 175)
                        {
                            changeTransitionState(false, 0);
                            obj.hist_yaw = obj.cur_yaw;
                            obj.stopTurn();
                        }
                    }
                    else
                    {
                        float diff = (obj.init_yaw - obj.cur_yaw);
                        Debug.WriteLine("Cur: " + obj.cur_yaw);
                        Debug.WriteLine("Init: " + obj.init_yaw);
                        Debug.WriteLine("Diff: " + diff);
                        if (diff <= 185 && obj.cur_yaw > -180 && obj.cur_yaw < 0)
                        {
                            changeTransitionState(false, 0);
                            obj.hist_yaw = obj.cur_yaw;
                            obj.stopTurn();
                        }
                    }

                    break;
                case 3:
                    obj.moveUp();
                    if ((obj.cur_height - obj.his_height) >= obj.diff_height || obj.cur_height >= 1.15f)
                    {
                        changeTransitionState(false, 0);
                        obj.his_height = obj.cur_height;
                        obj.stopMoveUpOrDown();
                        //obj.diff_height += 0.05f;
                    }

                    Debug.WriteLine("his: " + obj.his_height);
                    Debug.WriteLine("Cur " + obj.cur_height);
                    break;
                case 4:
                    obj.moveDown();
                    obj.throttle = 0;
                    obj.roll = 0;
                    obj.pitch = 0;
                    obj.yaw = 0;
                    DJISDKManager.Instance.VirtualRemoteController.UpdateJoystickValue(obj.throttle, obj.yaw, obj.pitch, obj.roll);
                    var res = await DJISDKManager.Instance.ComponentManager.GetFlightControllerHandler(0, 0).StartAutoLandingAsync();
                    var messageDialog = new MessageDialog(String.Format("Start send landing command: {0}", res.ToString()));
                    await messageDialog.ShowAsync();

                    break;
                default:
                    break;
            }
        }

        private void moveUp()
        {
            roll = 0;
            throttle = 0.1f;
            DJISDKManager.Instance.VirtualRemoteController.UpdateJoystickValue(throttle, yaw, pitch, roll);

        }

        private void moveDown()
        {
                roll = 0;
                throttle = -0.25f;
            DJISDKManager.Instance.VirtualRemoteController.UpdateJoystickValue(throttle, yaw, pitch, roll);
            
        }


        private void stopMoveUpOrDown()
        {

            roll = 0;
            throttle = 0.0f;
            DJISDKManager.Instance.VirtualRemoteController.UpdateJoystickValue(throttle, yaw, pitch, roll);

        }

        private void turn()
        {
            roll = 0.0f;
            throttle = 0.0f;
            yaw = 0.5f;
            DJISDKManager.Instance.VirtualRemoteController.UpdateJoystickValue(throttle, yaw, pitch, roll);

        }


        private void stopTurn()
        {
            roll = 0.0f;
            throttle = 0.0f;
            yaw = 0.0f;
            DJISDKManager.Instance.VirtualRemoteController.UpdateJoystickValue(throttle, yaw, pitch, roll);

        }


        private async void Get_height()
        {
            try {
                var res = await DJISDKManager.Instance.ComponentManager.GetFlightControllerHandler(0, 0).GetAltitudeAsync();
                var real_height = res.value.Value.value.ToString();
                cur_height = (float)res.value.Value.value;
                if (started_auto ==false && cur_height > 1.1f) {
                    started_auto = true;
                    changeTransitionState(false, 0);
                }
                //Debug.WriteLine("***********************New height: " + real_height.ToString());
                if (real_height != null)
                {
                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        readed.Add(real_height);
                        Textbox3.Text = real_height + " m";
                    });
                }
            }
            catch (InvalidCastException e)
            {
            }
        }
        
        private async void Get_IMU()
        {
            try
            {
                var res = await DJISDKManager.Instance.ComponentManager.GetFlightControllerHandler(0, 0).GetAttitudeAsync();
                var yaw_str = res.value.Value.yaw.ToString();
                var yaw_val = res.value.Value.yaw;
                //Debug.WriteLine("***********************New yaw: " + yaw.ToString());
                if (yaw_str != null)
                {
                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        cur_yaw = (float) yaw_val;
                        Textbox4.Text = yaw_str + " degree";
                        if (flag_yaw)
                        {
                            init_yaw = cur_yaw;
                            flag_yaw = false;
                        }
                    });
                }
            }
            catch (Exception e) {
            }
        }


        private async static Task UpdateBarcodeDictionary()
        {
            if (BarcodePairList == null)
            {
                return;
            }
            

            foreach (var item in BarcodePairList)
            {
                if (locationID.Contains(item.Key) == false)
                {
                    csv_datat += item.Key + "," + item.Value + "\n";
                    locationID.Add(item.Key);
                }
                
            }

            await Mainpage.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                var BarCanvas = Mainpage.GetBarCanvas();
               BarCanvas.Children.Clear();

                var tb = new TextBlock
                {
                    Margin = new Thickness(0, 0, 0, 0),
                    Text = csv_datat,
                    FontWeight = FontWeights.Bold,
                    FontSize = 30
                };
                BarCanvas.Children.Add(tb);
            }); 

            try
            {
                WriteFile(csv_datat);
            }
            catch
            {
            }
        }

        private async static void WriteFile(string data)
        {
            try
            {
                if (StorageFileObject == null)
                {
                    string filename = "hack2019-8.csv";

                    StorageFolder LocalStorageFolderObject = KnownFolders.SavedPictures;


                    StorageFileObject = await LocalStorageFolderObject.CreateFileAsync(filename, CreationCollisionOption.ReplaceExisting);
                }
                await FileIO.WriteTextAsync(StorageFileObject, data);

            }
            catch
            {
            }

        }

        public async static Task<SoftwareBitmap> GetCroppedBitmapAsync(SoftwareBitmap softwareBitmap, uint startPointX, uint startPointY, uint width, uint height)
        {
            using (InMemoryRandomAccessStream stream = new InMemoryRandomAccessStream())
            {
                BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.BmpEncoderId, stream);

                encoder.SetSoftwareBitmap(SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8));

                encoder.BitmapTransform.Bounds = new BitmapBounds()
                {
                    X = startPointX,
                    Y = startPointY,
                    Height = height,
                    Width = width
                };

                await encoder.FlushAsync();

                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);

                return await decoder.GetSoftwareBitmapAsync(softwareBitmap.BitmapPixelFormat, softwareBitmap.BitmapAlphaMode);
            }
        }

        public static async void ShowMessagePopup(string message)
        {
            await Mainpage.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                
                try
                {
                    NotifyPopup notifyPopup = new NotifyPopup(message);
                    notifyPopup.Show();
                }
                catch (Exception)
                {

                }
                
            });
        }

        //Decode data. Do nothing here. This function would return a bytes array with image data in RGBA format.
        async void ReceiveDecodedData(byte[] data, int width, int height)
        {
            //basically copied from the sample code
            lock (bufLock)
            {
                //lock when updating decoded buffer, as this is run in async
                //some operation in this function might overlap, so operations involving buffer, width and height must be locked
                if (decodedDataBuf == null)
                {
                    decodedDataBuf = data;
                }
                else
                {
                    if (data.Length != decodedDataBuf.Length)
                    {
                        Array.Resize(ref decodedDataBuf, data.Length);
                    }
                    data.CopyTo(decodedDataBuf.AsBuffer());
                    this.width = width;
                    this.height = height;
                }
            }
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                //dispatch to UI thread to do UI update (image)
                //WriteableBitmap is exclusive to UI thread
                if (VideoSource == null || VideoSource.PixelWidth != width || VideoSource.PixelHeight != height)
                {
                    VideoSource = new WriteableBitmap((int)width, (int)height);
                    fpvImage.Source = VideoSource;
                    //Start barcode reader worker after the first frame is received
                    if (readerWorker == null)
                    {
                        BarcodeWorker();
                        readerWorker.Start();
                        //Get_height();
                    }
                }
                lock (bufLock)
                {
                    //copy buffer to the bitmap and draw the region we will read on to notify the users
                    decodedDataBuf.AsBuffer().CopyTo(VideoSource.PixelBuffer);
                }
                //Invalidate cache and trigger redraw
                VideoSource.Invalidate();
            });
        }

        public Image GetImage()
        {
            return fpvImage;
        }

        public Canvas GetCanvas()
        {
            return MLResultCanvas;
        }

        public Canvas GetBarCanvas()
        {
            return BarResult;
        }

        public static void changeTransitionState(bool _transition, int _tran_state) {
            transition = _transition;
            tran_state = _tran_state;
        }
        
    }
}
