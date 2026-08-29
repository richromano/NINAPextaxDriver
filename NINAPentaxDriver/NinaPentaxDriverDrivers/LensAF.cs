using NINA.Plugin.Interfaces;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Rtg.NINA.NinaPentaxDriver.NinaPentaxDriverDrivers {
    internal static class PentaxMediator {
        public static NinaPentaxDriver Plugin { get; private set; }
        public static IMessageBroker MessageBroker { get; private set; }

//        public static Dictionary<string, Database.NikonCameraSpec> CameraList { get; private set; }

        internal static void InitMediator(NinaPentaxDriver plugin, IMessageBroker messageBroker) {
            Plugin = plugin;
            MessageBroker = messageBroker;

//            CameraList = Database.NikonCameraSpec.ReadDatabase();
        }
    }

    public class LensAF {
        public class RegisterFocuser(string focuserName) : IMessage {
            public Guid SenderId => Guid.Parse(PentaxMediator.Plugin.Identifier);
            public string Sender => nameof(NinaPentaxDriver);
            public DateTimeOffset SentAt => DateTime.UtcNow;
            public Guid MessageId => Guid.NewGuid();
            public DateTimeOffset? Expiration => null;
            public Guid? CorrelationId => null;
            public int Version => 1;
            public IDictionary<string, object> CustomHeaders => new Dictionary<string, object>();
            public string Topic => "LensAF.RegisterFocuser";
            public object Content => focuserName;


            public async static void Send(string focuserName) {
                await PentaxMediator.MessageBroker.Publish(new RegisterFocuser(focuserName));
            }
        }


        public class GotoFocus() : IMessage {
            public Guid SenderId => Guid.Parse(PentaxMediator.Plugin.Identifier);
            public string Sender => nameof(NinaPentaxDriver);
            public DateTimeOffset SentAt => DateTime.UtcNow;
            public Guid MessageId => Guid.NewGuid();
            public DateTimeOffset? Expiration => null;
            public Guid? CorrelationId => null;
            public int Version => 1;
            public IDictionary<string, object> CustomHeaders => new Dictionary<string, object>();
            public string Topic => "LensAF.GotoFocus";
            public object Content => new();


            public async static void Send() {
                await PentaxMediator.MessageBroker.Publish(new GotoFocus());
            }
        }
    }
}
