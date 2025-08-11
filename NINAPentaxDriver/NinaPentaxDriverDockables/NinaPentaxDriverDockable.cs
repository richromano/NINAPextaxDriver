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

        public void UpdateDeviceInfo(CameraInfo deviceInfo) {
            // The IsVisible flag indicates if the dock window is active or hidden
            if (IsVisible) {
                CameraInfo = deviceInfo;
                if (CameraInfo.Connected) {
                } else {
                }
                RaisePropertyChanged(nameof(CameraInfo));
            }
        }
    }
}
