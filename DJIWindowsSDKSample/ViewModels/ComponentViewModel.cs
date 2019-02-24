using DJI.WindowsSDK;
using DJIDemo.AIModel;
using DJIUWPSample.Commands;
using DJIUWPSample.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.ApplicationModel.Core;
using Windows.System.Threading;
using Windows.UI.Core;
using Windows.UI.Popups;

namespace DJIWindowsSDKSample.ViewModels
{
    class ComponentViewModel : ViewModelBase
    {
        private float throttle = 0;
        private float roll = 0;
        private float pitch = 0;
        private float yaw = 0;

        public ICommand _startTakeoff;
        public ICommand StartTakeoff
        {
            get
            {
                if (_startTakeoff == null)
                {
                    _startTakeoff = new RelayCommand(async delegate ()
                    {
                        var res = await DJISDKManager.Instance.ComponentManager.GetFlightControllerHandler(0, 0).StartTakeoffAsync();
                        var messageDialog = new MessageDialog(String.Format("Start send Takeoff command: {0}", res.ToString()));
                        await messageDialog.ShowAsync();
                    }, delegate () { return true; });
                }
                return _startTakeoff;
            }
        }

        public ICommand _startLanding;
        public ICommand StartLanding
        {
            get
            {
                if (_startLanding == null)
                {
                    _startLanding = new RelayCommand(async delegate ()
                    {
                        throttle = 0;
                        roll = 0;
                        pitch = 0;
                        yaw = 0;
                        DJISDKManager.Instance.VirtualRemoteController.UpdateJoystickValue(throttle, yaw, pitch, roll);
                        var res = await DJISDKManager.Instance.ComponentManager.GetFlightControllerHandler(0, 0).StartAutoLandingAsync();
                        var messageDialog = new MessageDialog(String.Format("Start send landing command: {0}", res.ToString()));
                        await messageDialog.ShowAsync();
                    }, delegate () { return true; });
                }
                return _startLanding;
            }
        }

        public ICommand _goUp;
        public ICommand GoUp
        {
            get
            {
                if (_goUp == null)
                {
                    _goUp = new RelayCommand(async delegate ()
                    {
                       throttle = 0.1f;
                       DJISDKManager.Instance.VirtualRemoteController.UpdateJoystickValue(throttle, yaw, pitch, roll);
                    }, delegate () { return true; });
                }
                return _goUp;
            }
        }

        public ICommand _goDown;
        public ICommand GoDown
        {
            get
            {
                if (_goDown == null)
                {
                    _goDown = new RelayCommand(async delegate ()
                    {
                        throttle = -0.1f;
                        DJISDKManager.Instance.VirtualRemoteController.UpdateJoystickValue(throttle, yaw, pitch, roll);
                    }, delegate () { return true; });
                }
                return _goDown;
            }
        }

        public ICommand _getHeight;
        public ICommand GetHeight
        {
            get
            {
                if (_getHeight == null)
                {
                    _getHeight = new RelayCommand(async delegate ()
                    {
                        var res = await DJISDKManager.Instance.ComponentManager.GetFlightControllerHandler(0, 0).GetAltitudeAsync();
                        var messageDialog = new MessageDialog(String.Format("Height: {0}", res.value.Value.value.ToString()));
                        await messageDialog.ShowAsync();
                        var real_height = res.value.Value.value;
                    }, delegate () { return true; });
                }
                return _getHeight;
            }
        }

        public ICommand _brake;
        public ICommand Brake
        {
            get
            {
                if (_brake == null)
                {
                    _brake = new RelayCommand(async delegate ()
                    {
                        throttle = 0;
                        roll = 0;
                        pitch = 0;
                        yaw = 0;
                        DJISDKManager.Instance.VirtualRemoteController.UpdateJoystickValue(throttle, yaw, pitch, roll);
                    }, delegate () { return true; });
                }
                return _brake;
            }
        }

        public ICommand _autoFlight;
        public ICommand AutoFlight
        {
            get
            {
                if (_autoFlight == null)
                {
                    _autoFlight = new RelayCommand(async delegate ()
                    {
                        var res = await DJISDKManager.Instance.ComponentManager.GetFlightControllerHandler(0, 0).StartTakeoffAsync();
                        var res2 = await DJISDKManager.Instance.ComponentManager.GetFlightControllerHandler(0, 0).GetAltitudeAsync();
                        var height = res2.value.Value.value;
                        await Task.Delay(5000);
                        do
                        {
                            res2 = await DJISDKManager.Instance.ComponentManager.GetFlightControllerHandler(0, 0).GetAltitudeAsync();
                            height = res2.value.Value.value;
                            throttle = 0.1f;
                            DJISDKManager.Instance.VirtualRemoteController.UpdateJoystickValue(throttle, yaw, pitch, roll);
                        }
                        while (height < 1.3);

                        throttle = 0;
                        DJISDKManager.Instance.VirtualRemoteController.UpdateJoystickValue(throttle, yaw, pitch, roll);
                    }, delegate () { return true; });
                }
                return _autoFlight;
            }
        }

        public ICommand _godownTest;
        public ICommand GoDownTest
        {
            get
            {
                if (_godownTest == null)
                {
                    _godownTest = new RelayCommand(async delegate ()
                    {
                        var res = await DJISDKManager.Instance.ComponentManager.GetFlightControllerHandler(0, 0).StartTakeoffAsync();
                        var res2 = await DJISDKManager.Instance.ComponentManager.GetFlightControllerHandler(0, 0).GetAltitudeAsync();
                        var height = res2.value.Value.value;
                        await Task.Delay(2000);
                        do
                        {
                            res2 = await DJISDKManager.Instance.ComponentManager.GetFlightControllerHandler(0, 0).GetAltitudeAsync();
                            height = res2.value.Value.value;
                            throttle = -0.2f;
                            DJISDKManager.Instance.VirtualRemoteController.UpdateJoystickValue(throttle, yaw, pitch, roll);
                        }
                        while (height > 0.2);

                        throttle = 0;
                        DJISDKManager.Instance.VirtualRemoteController.UpdateJoystickValue(throttle, yaw, pitch, roll);
                    }, delegate () { return true; });
                }
                return _godownTest;
            }
        }

    }
}
