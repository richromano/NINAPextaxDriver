using Accord.Imaging.Filters;
using NINA.Astrometry;
using NINA.Astrometry.Interfaces;
using NINA.Equipment.Equipment.MyCamera;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Interfaces.ViewModel;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.ViewModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Rtg.NINA.NinaPentaxDriver.NinaPentaxDriverDockables {
    /// <summary>
    /// This Class shows the basic principle on how to add a new panel to N.I.N.A. Imaging tab via the plugin interface
    /// </summary>
    [Export(typeof(IDockableVM))]
    public class NinaPentaxDriverDockable : DockableVM, ICameraConsumer {
        private ICameraMediator cameraMediator;

        [ImportingConstructor]
        public NinaPentaxDriverDockable(
            IProfileService profileService,
            ICameraMediator cameraMediator) : base(profileService) {

            // This will reference the resource dictionary to import the SVG graphic and assign it as the icon for the header bar
            var dict = new ResourceDictionary();
            dict.Source = new Uri("Rtg.NINA.NinaPentaxDriver;component/NinaPentaxDriverDockables/NinaPentaxDriverDockableTemplates.xaml", UriKind.RelativeOrAbsolute);
            ImageGeometry = (System.Windows.Media.GeometryGroup)dict["Rtg.NINA.NinaPentaxDriver_AltitudeSVG"];
            ImageGeometry.Freeze();

            this.cameraMediator = cameraMediator;
            cameraMediator.RegisterConsumer(this);
            Title = "Camera Lens Aperture";
        }

        public void Dispose() {
            // On shutdown cleanup
            cameraMediator.RemoveConsumer(this);
        }
        public CameraInfo CameraInfo { get; private set; }

        /*
         *     public static readonly FNumber F1_4 = new FNumber("1.4");

    public static readonly FNumber F1_6 = new FNumber("1.6");

    public static readonly FNumber F1_7 = new FNumber("1.7");

    public static readonly FNumber F1_8 = new FNumber("1.8");

    public static readonly FNumber F1_9 = new FNumber("1.9");

    public static readonly FNumber F2_0 = new FNumber("2.0");

    public static readonly FNumber F2_2 = new FNumber("2.2");

    public static readonly FNumber F2_4 = new FNumber("2.4");

    public static readonly FNumber F2_5 = new FNumber("2.5");

    public static readonly FNumber F2_8 = new FNumber("2.8");

    public static readonly FNumber F3_2 = new FNumber("3.2");

    public static readonly FNumber F3_3 = new FNumber("3.3");

    public static readonly FNumber F3_5 = new FNumber("3.5");

    public static readonly FNumber F4_0 = new FNumber("4.0");

    public static readonly FNumber F4_5 = new FNumber("4.5");

    public static readonly FNumber F4_8 = new FNumber("4.8");

    public static readonly FNumber F5_0 = new FNumber("5.0");

    public static readonly FNumber F5_6 = new FNumber("5.6");

    public static readonly FNumber F5_8 = new FNumber("5.8");

    public static readonly FNumber F6_3 = new FNumber("6.3");

    public static readonly FNumber F6_7 = new FNumber("6.7");

    public static readonly FNumber F7_1 = new FNumber("7.1");

    public static readonly FNumber F8_0 = new FNumber("8.0");
*/

        public bool Status1_4 { get; set; }
        public bool Status2_0 { get; set; }
        public bool Status2_8 { get; set; }
        public bool Status4_0 { get; set; }
        public bool Status5_6 { get; set; }
        public bool Status8_0 { get; set; }

        public void UpdateDeviceInfo(CameraInfo deviceInfo) {
            // The IsVisible flag indicates if the dock window is active or hidden
            if (IsVisible) {
                CameraInfo = deviceInfo;
                if (CameraInfo.Connected) {
                    Status1_4 = true;
                    Status2_0 = true;
                    Status2_8 = true;
                    Status4_0 = true;
                    Status5_6 = true;
                    Status8_0 = true;
                } else {
                }
                RaisePropertyChanged(nameof(CameraInfo));
                RaisePropertyChanged(nameof(Status1_4));
                RaisePropertyChanged(nameof(Status2_0));
                RaisePropertyChanged(nameof(Status2_8));
                RaisePropertyChanged(nameof(Status4_0));
                RaisePropertyChanged(nameof(Status5_6));
                RaisePropertyChanged(nameof(Status8_0));
            }
        }
    }
}
