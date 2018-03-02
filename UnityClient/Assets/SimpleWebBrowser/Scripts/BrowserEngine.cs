using System;
using System.Diagnostics;
using System.Collections;
using System.Threading;
using MessageLibrary;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace SimpleWebBrowser
{
    public class BrowserEngine
    {
        private SharedTextureBuffer _mainTexArray;
        private MessageReader _inCommServer;
        private MessageWriter _outCommServer;
        private Process _pluginProcess;
        
        public Texture2D BrowserTexture = null;
        private bool StopRequestFlag = false;
        public bool Initialized = false;
        private bool _needToRunOnce = false;
        private string _runOnceJS = "";
        //Image buffer
        private bool _connected = false;
        private Thread _pollthread;


        #region Status events

        public delegate void PageLoaded(string url);

        public event PageLoaded OnPageLoaded;

        #endregion


        #region Settings

        public int kWidth = 512;
        public int kHeight = 512;

        private string _sharedFileName;

        //comm files
        private string _inCommFile;
        private string _outCommFile;

        private string _initialURL;
        private bool _enableWebRTC;
        private bool _enableGPU;

        #endregion

        #region Dialogs

        public delegate void JavaScriptDialog(string message, string prompt, DialogEventType type);

        public event JavaScriptDialog OnJavaScriptDialog;

        #endregion

        #region JSQuery

        public delegate void JavaScriptQuery(string message);

        public event JavaScriptQuery OnJavaScriptQuery;

        #endregion



        #region Init

        private void BackgroundPollThread() {
            while (!StopRequestFlag) {
                SendPing();
                Thread.Sleep(1000);
            }
        }

        public IEnumerator InitPlugin(int width, int height, string sharedfilename,string initialURL,bool enableWebRTC,bool enableGPU)
        {
            _pollthread=new Thread(BackgroundPollThread);
            _pollthread.Start();
            //Initialization (for now) requires a predefined path to PluginServer,
            //so change this section if you move the folder
            //Also change the path in deployment script.

#if UNITY_EDITOR_64
         string PluginServerPath = Application.dataPath + @"\SimpleWebBrowser\PluginServer\x64";
#else
#if UNITY_EDITOR_32
        string PluginServerPath = Application.dataPath + @"\SimpleWebBrowser\PluginServer\x86";
#else
        //HACK
        string AssemblyPath=System.Reflection.Assembly.GetExecutingAssembly().Location;
        //log this for error handling
        Debug.Log("Assembly path:"+AssemblyPath);

        AssemblyPath = Path.GetDirectoryName(AssemblyPath); //Managed
      
        AssemblyPath = Directory.GetParent(AssemblyPath).FullName; //<project>_Data
        AssemblyPath = Directory.GetParent(AssemblyPath).FullName;//required

        string PluginServerPath=AssemblyPath+@"\PluginServer";
#endif
#endif



            Debug.Log("Starting server from:" + PluginServerPath);

            kWidth = width;
            kHeight = height;



            _sharedFileName = sharedfilename;

            //randoms
            Guid inID = Guid.NewGuid();
            _outCommFile = inID.ToString();

            Guid outID = Guid.NewGuid();
            _inCommFile = outID.ToString();

            _initialURL = initialURL;
            _enableWebRTC = enableWebRTC;
            _enableGPU = enableGPU;

              if (BrowserTexture == null)
                BrowserTexture = new Texture2D(kWidth, kHeight, TextureFormat.BGRA32, false, true);
            string args = BuildParamsString();


           _connected = false;
            _inCommServer = null;
            _outCommServer = null;


            while (!_connected)
            {
                try
                {
                    _pluginProcess = new Process()
                    {
                        StartInfo = new ProcessStartInfo()
                        {
                            WorkingDirectory = PluginServerPath,
                            FileName = PluginServerPath + @"\SharedPluginServer.exe",
                            Arguments = args

                        }
                    };
                    _pluginProcess.Start();
                    Initialized = false;
                }
                catch (Exception ex)
                {
                    //log the file
                    Debug.Log("FAILED TO START SERVER FROM:" + PluginServerPath + @"\SharedPluginServer.exe");
                    throw;
                }

                bool isReady = false;
                while (!isReady) {
                    try {
                        isReady = _pluginProcess.WaitForInputIdle(0);
                    }
                    catch (Exception e) {
                        Debug.LogException(e);
                    }
                    yield return new WaitForSeconds(0.5f);
                }

                




                MessageReader inserv = null;
                MessageWriter outserv = null;
                try {
                    inserv = MessageReader.Open(_inCommFile);
                    outserv = MessageWriter.Open(_outCommFile);
                    _inCommServer = inserv;
                    _outCommServer = outserv;
                    _connected = true;
                }
                catch (Exception e) {
                    if (_inCommServer != null) _inCommServer.Dispose();
                    if (_outCommServer != null) _outCommServer.Dispose();
                    _pluginProcess.Dispose();
                }
                
            }
        }

        private string BuildParamsString()
        {
            string ret = kWidth.ToString() + " " + kHeight.ToString() + " ";
            ret = ret + _initialURL + " ";
            ret = ret + _sharedFileName + " ";
            ret = ret + _outCommFile + " ";
            ret = ret + _inCommFile + " ";

            if (_enableWebRTC)
                ret = ret + " 1"+" ";
            else
                ret = ret + " 0"+" ";

            if(_enableGPU)
                ret = ret + " 1" + " ";
            else
                ret = ret + " 0" + " ";

            return ret;
        }

        #endregion



        #region SendEvents

        public void SendNavigateEvent(string url, bool back, bool forward)
        {
            if (Initialized)
            {
                GenericEvent ge = new GenericEvent()
                {
                    Type = GenericEventType.Navigate,
                    GenericType = MessageLibrary.BrowserEventType.Generic,
                    NavigateUrl = url
                };

                if (back)
                    ge.Type = GenericEventType.GoBack;
                else if (forward)
                    ge.Type = GenericEventType.GoForward;

                EventPacket ep = new EventPacket()
                {
                    Event = ge,
                    Type = MessageLibrary.BrowserEventType.Generic
                };
                _outCommServer.TrySend(ep,100);
            }
        }

        public void SendShutdownEvent()
        {
            if (Initialized)
            {
                GenericEvent ge = new GenericEvent()
                {
                    Type = GenericEventType.Shutdown,
                    GenericType = MessageLibrary.BrowserEventType.Generic
                };

                EventPacket ep = new EventPacket()
                {
                    Event = ge,
                    Type = MessageLibrary.BrowserEventType.Generic
                };

                _outCommServer.TrySend(ep,100);
            }
        }

        

       public void SendDialogResponse(bool ok, string dinput)
        {
            if (Initialized)
            {
                DialogEvent de = new DialogEvent()
                {
                    GenericType = MessageLibrary.BrowserEventType.Dialog,
                    success = ok,
                    input = dinput
                };

                EventPacket ep = new EventPacket
                {
                    Event = de,
                    Type = MessageLibrary.BrowserEventType.Dialog
                };

                _outCommServer.TrySend(ep,100);
            }
        }

        public void SendQueryResponse(string response)
        {
            if (Initialized)
            {
                GenericEvent ge = new GenericEvent()
                {
                    Type = GenericEventType.JSQueryResponse,
                    GenericType = BrowserEventType.Generic,
                    JsQueryResponse = response
                };

                EventPacket ep = new EventPacket()
                {
                    Event = ge,
                    Type = BrowserEventType.Generic
                };

                _outCommServer.TrySend(ep,100);
            }
        }

        public void SendCharEvent(int character, KeyboardEventType type)
        {
            if (Initialized)
            {
                KeyboardEvent keyboardEvent = new KeyboardEvent()
                {
                    Type = type,
                    Key = character
                };
                EventPacket ep = new EventPacket()
                {
                    Event = keyboardEvent,
                    Type = MessageLibrary.BrowserEventType.Keyboard
                };

                _outCommServer.TrySend(ep, 100);
            }
        }

        public void SendMouseEvent(MouseMessage msg)
        {
            if (Initialized)
            {
                EventPacket ep = new EventPacket
                {
                    Event = msg,
                    Type = MessageLibrary.BrowserEventType.Mouse
                };

                if (!_outCommServer.TrySend(ep, 100)) {
                    Debug.LogWarningFormat("mouse message lost {0}", ep.Type);
                }
            }

        }

        public void SendExecuteJSEvent(string js)
        {
            if (Initialized)
            {
                GenericEvent ge = new GenericEvent()
                {
                    Type = GenericEventType.ExecuteJS,
                    GenericType = BrowserEventType.Generic,
                    JsCode = js
                };

                EventPacket ep = new EventPacket()
                {
                    Event = ge,
                    Type = BrowserEventType.Generic
                };

                _outCommServer.TrySend(ep, 100);
            }
        }

        public void SendPing()
       {
            if (Initialized){
                GenericEvent ge = new GenericEvent()
                {
                    Type = GenericEventType.Navigate, //could be any
                    GenericType = BrowserEventType.Ping,
                };
                EventPacket ep = new EventPacket()
                {
                    Event = ge,
                    Type = BrowserEventType.Ping
                };
                    _outCommServer.TrySend(ep, 100);
                }
        }


        #endregion


        #region Helpers

        /// <summary>
        /// Used to run JS on initialization, for example, to set CSS
        /// </summary>
        /// <param name="js">JS code</param>
       public void RunJSOnce(string js )
        {
            _needToRunOnce = true;
            _runOnceJS = js;
        }

        #endregion

        



     public void UpdateTexture()
        {

            if (Initialized)
            {


                UpdateInitialized();



                //execute run-once functions
                if (_needToRunOnce)
                {
                    SendExecuteJSEvent(_runOnceJS);
                    _needToRunOnce = false;
                }
            }
            else
            {
                if(_connected)
                { 
                        try
                        {
                            //init memory file
                            _mainTexArray = new SharedTextureBuffer(_sharedFileName);

                            Initialized = true;
                        }
                        catch (Exception ex)
                        {
                            Debug.Log("Exception on init:" + ex.Message + ".Waiting for plugin server");
                        }

                }
               

            }
        }

        //Receiver
        public void CheckMessage()
        {

            if (Initialized)
            {
                try
                {
                    // Ensure that no other threads try to use the stream at the same time.
                    EventPacket ep = _inCommServer.TryRecive(0);


                    if (ep != null)
                    {
                        //main handlers
                        if (ep.Type == BrowserEventType.Dialog)
                        {
                            DialogEvent dev = ep.Event as DialogEvent;
                            if (dev != null)
                            {
                                if (OnJavaScriptDialog != null)
                                    OnJavaScriptDialog(dev.Message, dev.DefaultPrompt, dev.Type);
                            }
                        }
                        if (ep.Type == BrowserEventType.Generic)
                        {
                            GenericEvent ge = ep.Event as GenericEvent;
                            if (ge != null)
                            {
                                if (ge.Type == GenericEventType.JSQuery)
                                {
                                    if (OnJavaScriptQuery != null)
                                        OnJavaScriptQuery(ge.JsQuery);
                                }
                            }

                            if (ge.Type == GenericEventType.PageLoaded)
                            {
                                if (OnPageLoaded != null)
                                    OnPageLoaded(ge.NavigateUrl);
                            }
                        }
                    }

                }
                catch (Exception e)
                {
                    Debug.Log("Error reading from socket,waiting for plugin server to start...");
                }
            }
        }


        public void Shutdown() {
            StopRequestFlag = true;
            SendShutdownEvent();
            Initialized = false;
            if (_pollthread!=null)
                _pollthread.Join();
        }

        //////////Added//////////
        public void UpdateInitialized()
        {
            if (Initialized)
            {
                SendPing();
                if (_mainTexArray.AcquireReadLock(1)) {
                    _mainTexArray.MarkProcessed();
                    if (_mainTexArray.Length > 0) {
                        BrowserTexture.LoadRawTextureData(_mainTexArray.UnsafeDataPointer(), _mainTexArray.Length);
                        BrowserTexture.Apply();
                    }
                    _mainTexArray.ReleaseReadLock();
                }
                else {
                    int i = 0;
                }
            }
        }
    }
   
}