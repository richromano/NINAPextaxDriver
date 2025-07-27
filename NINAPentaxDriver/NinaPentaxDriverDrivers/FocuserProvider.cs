using NINA.Core.Utility;
using NINA.Equipment.Interfaces;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Interfaces.ViewModel;
using NINA.Image.Interfaces;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.Mediator;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Rtg.NINA.NinaPentaxDriver.NinaPentaxDriverDrivers {
    /// <summary>
    /// This Class shows the basic principle on how to add a new Device driver to N.I.N.A. via the plugin interface
    /// When the application scans for equipment the "GetEquipment" method of a device provider is called.
    /// This method should then return the specific List of Devices that you can connect to
    /// </summary>
    [Export(typeof(IEquipmentProvider))]
    public class FocuserProvider : IEquipmentProvider<IFocuser> {
        private IProfileService profileService;
        private ICameraMediator cameraMediator;

        [ImportingConstructor]
        public FocuserProvider(IProfileService profileService, ICameraMediator cameraMediator) {
            this.profileService = profileService;
            this.cameraMediator = cameraMediator;
        }

        public string Name => "NINA Pentax Driver";

        public IList<IFocuser> GetEquipment() {
            Logger.Info("Asked for a list of focusers");
            var devices = new List<IFocuser>();

            devices.Add(new FocuserDriver(profileService, cameraMediator));

            return devices;
        }
    }
}
