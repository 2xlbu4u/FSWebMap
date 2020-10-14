//using BeatlesBlog.SimConnect;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using Microsoft.FlightSimulator.SimConnect;

namespace WebMap {

    public class LatLon 
    {
        public double Latitude;
        public double Longitude;
    }


    public class OpenEventArgs : EventArgs {
        public string SimulatorName { get; private set; }
        public OpenEventArgs(string SimulatorName) {
            this.SimulatorName = SimulatorName;
        }
    }

    class SimConnectInstance {
        private const int WM_USER_SIMCONNECT = 0x0402;
        private IntPtr handle;
        private HwndSource handleSource;

        private MainViewModel sender;
        private SimConnect sc = null;
        private System.Timers.Timer refreshTimer = new System.Timers.Timer(1000);
        
        private const string appName = "Web Map";

        public EventHandler<OpenEventArgs> OpenEvent;
        public EventHandler DisconnectEvent;
        public LatLon userPos = new LatLon();
        enum DEFINITIONS
        {
            Struct1,
        }
        // this is how you declare a data structure so that
        // simconnect knows how to fill it/read it.
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        struct Struct1
        {
            public double latitude;
            public double longitude;
        };
        enum DATA_REQUESTS
        {
            REQUEST_1,
        };
        protected virtual void OnRaiseOpenEvent(OpenEventArgs e) {
            EventHandler<OpenEventArgs> handler = OpenEvent;
            if (handler != null) {
                handler(this, e);
            }
        }

        protected virtual void OnRaiseDisconnectEvent(EventArgs e) {
            EventHandler handler = DisconnectEvent;
            if (handler != null) {
                handler(this, e);
            }
        }

        public SimConnectInstance(MainViewModel sender)
        {

            this.sender = sender;

        }
        ~SimConnectInstance()
        {
            if (handleSource != null)
            {
                handleSource.RemoveHook(HandleSimConnectEvents);
            }
        }
        private IntPtr HandleSimConnectEvents(IntPtr hWnd, int message, IntPtr wParam, IntPtr lParam, ref bool isHandled)
        {
            isHandled = false;

            switch (message)
            {
                case WM_USER_SIMCONNECT:
                {
                    if (sc != null)
                    {
                        sc.ReceiveMessage();
                        isHandled = true;
                    }
                } 
                break;
            }
            return IntPtr.Zero;
        }
        public void Connect() {
            handle = new WindowInteropHelper(Application.Current.MainWindow).Handle;
            handleSource = HwndSource.FromHwnd(handle); // Get source of handle in order to add event handlers to it
            handleSource.AddHook(HandleSimConnectEvents);

            if (sc == null) {
                try {
                    sc = new SimConnect(appName, handle, WM_USER_SIMCONNECT, null, 0);
                    sc.OnRecvOpen += sc_OnRecvOpen;
                    sc.OnRecvException += sc_OnRecvException;
                    sc.OnRecvQuit += sc_OnRecvQuit;
                    
                    sc.AddToDataDefinition(DEFINITIONS.Struct1, "Plane Latitude", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    sc.AddToDataDefinition(DEFINITIONS.Struct1, "Plane Longitude", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    
                    sc.RegisterDataDefineStruct<Struct1>(DEFINITIONS.Struct1);
                    
                    sc.OnRecvSimobjectDataBytype += sc_OnRecvSimobjectData;
                }

                catch (Exception ex) {

                    Console.WriteLine("Unable to connect to Sim");
                }

                refreshTimer.Elapsed += (sender, e) =>
                {
                    sc.RequestDataOnSimObjectType(DATA_REQUESTS.REQUEST_1, DEFINITIONS.Struct1, 0, SIMCONNECT_SIMOBJECT_TYPE.USER);
                };
                refreshTimer.Start();
            }
        }

        public void Disconnect() {
            refreshTimer.Stop();
            sc.Dispose();
            handleSource.RemoveHook(HandleSimConnectEvents);
            sc = null;
            sender.TryDisableWebServer();
            OnRaiseDisconnectEvent(EventArgs.Empty);
        }

        private void sc_OnRecvOpen(SimConnect sender, SIMCONNECT_RECV_OPEN data) {
            OnRaiseOpenEvent(new OpenEventArgs(data.szApplicationName));

        }

        private void sc_OnRecvException(SimConnect sender, SIMCONNECT_RECV_EXCEPTION data) {
            Console.WriteLine(data.dwException);
        }

        private void sc_OnRecvQuit(SimConnect sender, SIMCONNECT_RECV data) {
            Disconnect();
        }

        private void sc_OnRecvSimobjectData(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA_BYTYPE data)
        {
            switch ((DATA_REQUESTS)data.dwRequestID)
            {
                case DATA_REQUESTS.REQUEST_1:
                    Struct1 s1 = (Struct1)data.dwData[0];
                    userPos.Latitude = s1.latitude;
                    userPos.Longitude = s1.longitude;
                 //   Console.WriteLine("lat: " + userPos.Latitude + " lon: " +userPos.Longitude);
                    break;

                default:
                    Console.WriteLine("Unknown request ID: " + data.dwRequestID);
                    break;
            }
        }
    }
}
