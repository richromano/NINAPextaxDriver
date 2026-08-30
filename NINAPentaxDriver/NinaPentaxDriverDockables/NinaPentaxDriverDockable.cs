using Accord.Imaging.Filters;
using CommunityToolkit.Mvvm.Input;
using NINA.Astrometry;
using NINA.Astrometry.Interfaces;
using NINA.Core.Utility;
using NINA.Equipment.Equipment.MyCamera;
using NINA.Equipment.Interfaces;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Interfaces.ViewModel;
using NINA.Plugin.Interfaces;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.Mediator;
using NINA.WPF.Base.ViewModel;
using Rtg.NINA.NinaPentaxDriver.NinaPentaxDriverDrivers;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Reflection;
using System.Security.AccessControl;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Xceed.Wpf.Toolkit.Primitives;

namespace Rtg.NINA.NinaPentaxDriver.NinaPentaxDriverDockables {
    /// <summary>
    /// This Class shows the basic principle on how to add a new panel to N.I.N.A. Imaging tab via the plugin interface
    /// </summary>
    [Export(typeof(IDockableVM))]
    public class NinaPentaxDriverDockable : DockableVM, ICameraConsumer {
        private readonly ICameraMediator cameraMediator;
        private readonly IFocuserMediator focuserMediator;
        public static string SelectedItem="null";
        public static string SelectedZoomItem = "null"; 
        static int oldValues = 0;

        // Expose commands on the dockable so XAML can bind to them
        public ICommand StartCalibration { get; }
        public ICommand CancelCalibration { get; }

        [ImportingConstructor]
        public NinaPentaxDriverDockable(
            IProfileService profileService,
            ICameraMediator cameraMediator, IFocuserMediator focuserMediator) : base(profileService) {

            // This will reference the resource dictionary to import the SVG graphic and assign it as the icon for the header bar
            var dict = new ResourceDictionary();
            dict.Source = new Uri("Rtg.NINA.NinaPentaxDriver;component/NinaPentaxDriverDockables/NinaPentaxDriverDockableTemplates.xaml", UriKind.RelativeOrAbsolute);
            ImageGeometry = (System.Windows.Media.GeometryGroup)dict["Rtg.NINA.NinaPentaxDriver_AltitudeSVG"];
            ImageGeometry.Freeze();

            this.cameraMediator = cameraMediator;
            this.focuserMediator = focuserMediator;
            cameraMediator.RegisterConsumer(this);
            Title = "Camera Lens Aperture & LV Zoom";

            // Wire up commands on the dockable VM to invoke the focuser driver's commands via reflection
            StartCalibration = new CommunityToolkit.Mvvm.Input.RelayCommand(() => ExecuteStartCalibration());
            CancelCalibration = new CommunityToolkit.Mvvm.Input.RelayCommand(() => ExecuteCancelCalibration());
//            this.focuserMediator = focuserMediator;
        }

        public void Dispose() {
            // On shutdown cleanup
            cameraMediator.RemoveConsumer(this);
        }
        public CameraInfo CameraInfo { get; private set; }

        public bool Status1_4 { get; set; }
        public bool Status1_8 { get; set; }
        public bool Status2_0 { get; set; }
        public bool Status2_2 { get; set; }
        public bool Status2_8 { get; set; }
        public bool Status3_5 { get; set; }
        public bool Status4_0 { get; set; }
        public bool Status4_5 { get; set; }
        public bool Status5_6 { get; set; }
        public bool Status6_3 { get; set; }
        public bool Status8_0 { get; set; }

        public void UpdateDeviceInfo(CameraInfo deviceInfo) {

            // The IsVisible flag indicates if the dock window is active or hidden
            if (IsVisible) {
                CameraInfo = deviceInfo;
                if (CameraInfo.Connected) {
                    if (SelectedItem != "null") {
                        //                        MessageBox.Show($"Selected Item: {SelectedItem}");
                        bool response = cameraMediator.SendCommandBool(SelectedItem);
                        SelectedItem = "null";
                    }

                    if (SelectedZoomItem != "null") {
                        //                        MessageBox.Show($"Selected Item: {SelectedItem}");
                        bool response = cameraMediator.SendCommandBool(SelectedZoomItem);
                        SelectedZoomItem = "null";
                    }

                    string values =cameraMediator.SendCommandString("GetAperture");
                    //MessageBox.Show($"Values: {values}");
                    Match match = Regex.Match(values, @"-?\d+");
                    int intValues = 0;

                    if (match.Success) {
                        intValues = int.Parse(match.Value);
                    }

                    if (intValues != oldValues) {
                        //MessageBox.Show($"IntValues: {intValues.ToString()}");

                        if ((intValues & 0x1) != 0)
                            Status1_4 = true;
                        else
                            Status1_4 = false;

                        if ((intValues & 0x2) != 0)
                            Status1_8 = true;
                        else
                            Status1_8 = false;

                        if ((intValues & 0x4) != 0)
                            Status2_0 = true;
                        else
                            Status2_0 = false;

                        if ((intValues & 0x8) != 0)
                            Status2_2 = true;
                        else
                            Status2_2 = false;

                        if ((intValues & 0x10) != 0)
                            Status2_8 = true;
                        else
                            Status2_8 = false;

                        if ((intValues & 0x20) != 0)
                            Status3_5 = true;
                        else
                            Status3_5 = false;

                        if ((intValues & 0x40) != 0)
                            Status4_0 = true;
                        else
                            Status4_0 = false;

                        if ((intValues & 0x80) != 0)
                            Status4_5 = true;
                        else
                            Status4_5 = false;

                        if ((intValues & 0x100) != 0)
                            Status5_6 = true;
                        else
                            Status5_6 = false;

                        if ((intValues & 0x200) != 0)
                            Status6_3 = true;
                        else
                            Status6_3 = false;

                        if ((intValues & 0x400) != 0)
                            Status8_0 = true;
                        else
                            Status8_0 = false;

                        RaisePropertyChanged(nameof(Status1_4));
                        RaisePropertyChanged(nameof(Status1_8));
                        RaisePropertyChanged(nameof(Status2_0));
                        RaisePropertyChanged(nameof(Status2_2));
                        RaisePropertyChanged(nameof(Status2_8));
                        RaisePropertyChanged(nameof(Status3_5));
                        RaisePropertyChanged(nameof(Status4_0));
                        RaisePropertyChanged(nameof(Status4_5));
                        RaisePropertyChanged(nameof(Status5_6));
                        RaisePropertyChanged(nameof(Status6_3));
                        RaisePropertyChanged(nameof(Status8_0));

                        oldValues = intValues;
                    }
                } else {
                }
                RaisePropertyChanged(nameof(CameraInfo));
            }
        }

        private void ExecuteStartCalibration() {
            Logger.Info("here ExecuteStartCalibration");
            IDevice device = focuserMediator.GetDevice();
            //var canon = device as CanonFocuser;

            //var focuserInstance = GetFocuserInstance();
            var focuser = device as FocuserDriver;
            if (focuser == null) {
                Logger.Info("focuser null ExecuteStartCalibration");
                return;
            }

            // Try to invoke ICommand property named StartCalibration
            var prop = focuser.GetType().GetProperty("StartCalibration", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop != null) {
                if (typeof(ICommand).IsAssignableFrom(prop.PropertyType)) {
                    var cmd = prop.GetValue(focuser) as ICommand;
                    if (cmd != null && cmd.CanExecute(null)) {
                        Logger.Info("Calling Property StartCalibration");
                        cmd.Execute(null);
                        return;
                    }
                }
            }

            Logger.Info("Returning Calibrate");

        }

        private void ExecuteCancelCalibration() {
            Logger.Info("here ExecuteCancelCalibration");
            IDevice device = focuserMediator.GetDevice();
            //var canon = device as CanonFocuser;

            //var focuserInstance = GetFocuserInstance();
            var focuser = device as FocuserDriver;
            if (focuser == null) return;

            // Try to invoke ICommand property named CancelCalibration
            var prop = focuser.GetType().GetProperty("CancelCalibration", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop != null) {
                if (typeof(ICommand).IsAssignableFrom(prop.PropertyType)) {
                    var cmd = prop.GetValue(focuser) as ICommand;
                    if (cmd != null && cmd.CanExecute(null)) {
                        cmd.Execute(null);
                        return;
                    }
                }
            }

            // Try method named CancelCalibration()
            var method = focuser.GetType().GetMethod("CancelCalibration", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method != null) {
                try {
                    method.Invoke(focuser, null);
                    return;
                } catch { }
            }

            // Try to find and cancel a CancellationTokenSource field named _CalibrationToken
            var field = focuser.GetType().GetField("_CalibrationToken", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null) {
                var tokenSrc = field.GetValue(focuser) as CancellationTokenSource;
                tokenSrc?.Cancel();
            }
        }
    }
}
