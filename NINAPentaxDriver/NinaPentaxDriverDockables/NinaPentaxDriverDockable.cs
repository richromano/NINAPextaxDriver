using NINA.Astrometry;
using NINA.Astrometry.Interfaces;
using NINA.Equipment.Equipment.MyTelescope;
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
    /// In this example an altitude chart is added to the imaging tab that shows the altitude chart based on the position of the telescope    
    /// </summary>
    [Export(typeof(IDockableVM))]
    public class NinaPentaxDriverDockable : DockableVM, ITelescopeConsumer {
        private ITelescopeMediator telescopeMediator;

        [ImportingConstructor]
        public NinaPentaxDriverDockable(
            IProfileService profileService,
            ITelescopeMediator telescopeMediator) : base(profileService) {

            // This will reference the resource dictionary to import the SVG graphic and assign it as the icon for the header bar
            var dict = new ResourceDictionary();
            dict.Source = new Uri("Rtg.NINA.NinaPentaxDriver;component/NinaPentaxDriverDockables/NinaPentaxDriverDockableTemplates.xaml", UriKind.RelativeOrAbsolute);
            ImageGeometry = (System.Windows.Media.GeometryGroup)dict["Rtg.NINA.NinaPentaxDriver_AltitudeSVG"];
            ImageGeometry.Freeze();

            this.telescopeMediator = telescopeMediator;
            telescopeMediator.RegisterConsumer(this);
            Title = "Altitude Chart";
        }

        public void Dispose() {
            // On shutdown cleanup
            telescopeMediator.RemoveConsumer(this);
        }
        public TelescopeInfo TelescopeInfo { get; private set; }

        public void UpdateDeviceInfo(TelescopeInfo deviceInfo) {
            // The IsVisible flag indicates if the dock window is active or hidden
            if (IsVisible) {
                TelescopeInfo = deviceInfo;
                if (TelescopeInfo.Connected) {
                } else {
                }
                RaisePropertyChanged(nameof(TelescopeInfo));
            }
        }
    }
}
