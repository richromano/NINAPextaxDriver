using Accord;
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
using NINA.Equipment.Equipment;
using NINA.Equipment.Interfaces;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Model;
using NINA.Equipment.SDK.CameraSDKs.ASTPANSDK;
using NINA.Equipment.Utility;
using NINA.Image.ImageData;
using NINA.Image.Interfaces;
using NINA.Profile;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.Mediator;
using Ricoh.CameraController;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Rtg.NINA.NinaPentaxDriver.NinaPentaxDriverDrivers {
    public class FocuserDriver : IFocuser, IDisposable {
        private bool disposedValue;
        internal IProfileService _profileService;
        internal bool _moving = false;
        internal bool _connected = false;
        private ICameraMediator _cameraMediator;
        private int _currentPosition = 10000;

        public FocuserDriver(IProfileService profileService, ICameraMediator cameraMediator) {
            _profileService = profileService;
            _cameraMediator = cameraMediator;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public bool IsMoving { get => _moving; }

        public int MaxIncrement {
            get {
                return 200;
            }
        }

        public int MaxStep {
            get {
                return 10000;
            }
        }

        public int Position {
            get {
                return _currentPosition;
            }

            set {
                // Need to limit because there is a bug in N.I.N.A.
                _moving = true;
                while (_currentPosition - value > 200) {
                    _cameraMediator.SendCommandBool($"SetPosition {200}");
                    _currentPosition = _currentPosition - 200;
                }

                while (_currentPosition - value < -200) {
                    _cameraMediator.SendCommandBool($"SetPosition {-200}");
                    _currentPosition = _currentPosition + 200;
                }

                _cameraMediator.SendCommandBool($"SetPosition {_currentPosition-value}");
                _currentPosition = value;
                _moving = false;
            }
        }

        public double StepSize { get => 1.0; }

        public bool TempCompAvailable { get => false; }

        public bool TempComp { get => false; set => throw new NotImplementedException(); }

        public double Temperature { get => double.NaN; }

        public bool HasSetupDialog => false;

        public string Id { get => "Pentax Lens"; }

        public string Name { get => "Pentax Lens"; }

        public string DisplayName { get => "Pentax Lens - Set Focus to Infinity Before Connecting"; }

        public string Category { get => "Pentax"; }

        public bool Connected {
            get {
                // TODO: Check that the camera is still open, if it is closed, then we are also closed
                if (!IsCameraConnected())
                    return false;

                return true;

            }
        }

        public string Description { get => "Pentax Lens"; }

        public string DriverInfo => "https://github.com/richromano/NINAPextaxDriver";

        public string DriverVersion => string.Empty;

        public IList<string> SupportedActions => new List<string>();

        public string Action(string actionName, string actionParameters) {
            throw new NotImplementedException();
        }

        private bool IsCameraConnected() {
            var type = _cameraMediator.GetType();
            var GetInfo = type.GetMethod("GetInfo");
            DeviceInfo info = (DeviceInfo)GetInfo.Invoke(_cameraMediator, null);

            if (!info.Connected) {
                return false;
            }

            return true;
        }

        public Task<bool> Connect(CancellationToken token) {
            return Task<bool>.Run(() => {
                if (!IsCameraConnected()) {
                    throw new NotConnectedException("Camera not connected.  Connect camera first.");
                    //return false;
                }

               _connected = true;
               _moving = true;
                //System.Windows.MessageBox.Show("Move focus to infinity before pressing OK");
                for (int i = 0; i < 2; i++) {

                    _currentPosition = 10000;
                    _connected = _cameraMediator.SendCommandBool($"SetPosition {-10000}");
                    Thread.Sleep(500);
                    if (!_connected)
                        throw new NotConnectedException("Camera not connected.  Connect camera first.");
                }
                _moving = false;
                return _connected;
            });
        }

        public void Disconnect() {
            _moving = false;
            _connected = false;
        }

        public void Halt() {
            //throw new NotImplementedException();
            return;
        }

        public Task Move(int position, CancellationToken ct, int waitInMs) {
            if(!_connected)
                throw new NotConnectedException("Focuser not connected.");

            if(!IsCameraConnected())
                throw new NotConnectedException("Camera not connected.  Connect camera first.");

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
