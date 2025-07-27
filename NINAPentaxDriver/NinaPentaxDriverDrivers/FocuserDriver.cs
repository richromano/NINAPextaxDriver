using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Castle.Components.DictionaryAdapter.Xml;
using FTD2XX_NET;
using Google.Protobuf.WellKnownTypes;
using Newtonsoft.Json.Linq;
using NINA.Core.Enum;
using NINA.Core.Locale;
using NINA.Core.Model.Equipment;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.Equipment.Equipment.MyCamera;
using NINA.Equipment.Equipment.MyGuider.PHD2;
using NINA.Equipment.Interfaces;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Model;
using NINA.Equipment.SDK.CameraSDKs.ASTPANSDK;
using NINA.Equipment.Utility;
using NINA.Image.ImageData;
using NINA.Image.Interfaces;
using NINA.Profile;
using NINA.Profile.Interfaces;
using Ricoh.CameraController;

namespace Rtg.NINA.NinaPentaxDriver.NinaPentaxDriverDrivers {
    public class FocuserDriver : IFocuser, IDisposable {
        private bool disposedValue;
        internal IProfileService _profileService;
        internal bool _moving = false;
        internal bool _connected = false;
        private ICameraMediator _cameraMediator;
        private int _currentPosition;

        public FocuserDriver(IProfileService profileService, ICameraMediator cameraMediator) {
            _profileService = profileService;
            _cameraMediator = cameraMediator;
            _currentPosition = 10000;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public bool IsMoving { get => _moving; }

        public int MaxIncrement {
            get {
                return 10000;
            }
        }

        public int MaxStep { get => MaxIncrement; }

        public int Position {
            get {
                return _currentPosition;
            }

            set {
                _currentPosition = value;
                _cameraMediator.SendCommandString($"SetPosition {_currentPosition}");
            }
        }

        public double StepSize { get => 1.0; }

        public bool TempCompAvailable { get => false; }

        public bool TempComp { get => false; set => throw new NotImplementedException(); }

        public double Temperature { get => double.NaN; }

        public bool HasSetupDialog => false;

        public string Id { get => "One"; }

        public string Name { get => "Pentax Lens"; }

        public string DisplayName { get => "Pentax Lens"; }

        public string Category { get => "Pentax"; }

        public bool Connected {
            get {
                // Check that the camera is still open, if it is closed, then we are also closed
                return _connected;
            }
        }

        public string Description { get => "Pentax Lens"; }

        public string DriverInfo => "https://github.com/richromano/NINAPextaxDriver";

        public string DriverVersion => string.Empty;

        public IList<string> SupportedActions => new List<string>();

        public string Action(string actionName, string actionParameters) {
            throw new NotImplementedException();
        }

        public Task<bool> Connect(CancellationToken token) {
            _moving = false;

            return Task<bool>.Run(() => {
               _connected = true;
               _moving = true;
                Position = 10000;
               _moving = false;
                return _connected;
            });
        }

        public void Disconnect() {
            _moving = false;
            _connected = false;
        }

        public void Halt() {
            throw new NotImplementedException();
        }

        public Task Move(int position, CancellationToken ct, int waitInMs) {
            _moving = true;

            return Task.Run(() => {
                Position = position;
                _moving = false;
            });
        }

        public void SendCommandBlind(string command, bool raw) {
            throw new NotImplementedException();
        }

        public bool SendCommandBool(string command, bool raw) {
            throw new NotImplementedException();
        }

        public string SendCommandString(string command, bool raw) {
            throw new NotImplementedException();
        }

        public void SetupDialog() {
            throw new NotImplementedException();
        }

        protected virtual void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    // TODO: dispose managed state (managed objects)
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        public void Dispose() {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
