/***************************************************************************
 *
 *      Copyright (c) 2009,2010,2011,2012 KaiTrade LLC (registered in Delaware)
 *                     All Rights Reserved Worldwide
 *
 * STRICTLY PROPRIETARY and CONFIDENTIAL
 *
 * WARNING:  This file is the confidential property of KaiTrade LLC For
 * use only by those with the express written permission and license from
 * KaiTrade LLC.  Unauthorized reproduction, distribution, use or disclosure
 * of this file or any program (or document) is prohibited.
 *
 ***************************************************************************/

using K2DataObjects;
using K4ServiceInterface;
using log4net;
using KaiTrade.Interfaces;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Timers;
using K4Channel;


namespace DriverBase
{
    /// <summary>
    /// Base class for drivers, provides status message handling and
    /// session management
    /// </summary>
    public class DriverBase : IDriver
    {
        /// <summary>
        /// Create a logger to record driver specific info
        /// </summary>
        public ILog driverLog = null;

        public ILog oRLog = null;
        public ILog tSLog = null;
        /// <summary>
        /// Create a logger to record low level details - a wire log
        /// </summary>
        public ILog wireLog = null;

        /// <summary>
        /// Maps all active contexts using their ID
        /// </summary>
        protected Dictionary<string, OrderContext> _activeContextMap;

        /// <summary>
        /// Maps some external ID to an Order context
        /// </summary>
        protected Dictionary<string, OrderContext> _apiIDOrderMap;

        /// <summary>
        /// Maps the CLOrdID of incomming requests to an
        /// order context
        /// </summary>
        protected Dictionary<string, OrderContext> _clOrdIDOrderMap;

        /// <summary>
        /// Path to config files
        /// </summary>
        protected string _configPath;

        protected string[] _currencies = { "AUD", "USD", "GBP", "GBp", "EUR", "JPY", "CHF", "CAD", "NZD", "CZK", "DKK", "HUF", "PLN", "SEK", "NOK", "TRY", "ZAR" };
        /// <summary>
        /// Product defined in the driver, i.e. the drivers product object
        /// </summary>
        protected Dictionary<string, object> _externalProduct;

        /// <summary>
        /// Not to run on live market
        /// </summary>
        protected bool _liveMarket = false;

        /// <summary>
        /// Parent Driver manager
        /// </summary>
        protected IDriverManager _parent;

        protected Dictionary<string, List<IPriceAgregator>> _priceAgregators;
        /// <summary>
        /// This maps a long product name to a Generic name, for example F.US.EPU2 --> EP
        /// it is used to locate a generic product whose menmonic is some symbol of short name
        /// at this point in tim this is only done in DOM processing
        /// </summary>
        protected Dictionary<string, string> _productGenericNameRegister;

        /// <summary>
        /// Used to keep track of our list of subjects accessed by their key
        /// </summary>
        protected Dictionary<string, IPublisher> _publisherRegister;

        /// <summary>
        /// Dictionary of update contexts - used by some driver(CQG) to detect duplicate updates
        /// </summary>
        protected Dictionary<string, PXUpdateContext> _pXContexts;

        /// <summary>
        /// are we supposed to queue replace requests?
        /// </summary>
        protected bool _queueReplaceRequests = false;

        /// <summary>
        /// Keeps track of driver sessions using the a string key
        /// </summary>
        protected Dictionary<string, IDriverSession> _sessions;

        /// <summary>
        /// The state of the driver as loaded from the app config
        /// </summary>
        protected IDriverState _state;

        protected Status _status = Status.closed;
        protected bool _useAsyncPriceUpdates = false;
        protected string m_ID = "";
        /// <summary>
        /// You must define these in the main drievr class NOW IN BASE CLASS!!
        /// </summary>
        //private string Name = "";
        protected string m_Tag = "";

        private BarUpdate _barUpdate;
        private DriverConnectionStatus[] _connectionState;
        private ConnectionStatusUpdate _connectionStatusUpdate;
        /// <summary>
        /// unique idenetity for this instance of a driver
        /// </summary>
        private string _identity;

        /// <summary>
        /// Holds that last status message sent to clients - used for status requests
        /// </summary>
        private IMessage _lastStatus;

        private NewMessage _message = null;
        /// <summary>
        /// Module information based on the drivers assembly
        /// </summary>
        private IModuleDef _module;

        private PriceUpdate _priceUpdate;
        /// <summary>
        /// Register of products used by the driver
        /// </summary>
        private Dictionary<string, IProduct> _productRegister;

        /// <summary>
        /// Worker thread to handle Px messages
        /// </summary>
        private PxUpdateProcessor _pxUpdateProcessor;

        private Queue<IPXUpdate> _pxUpdateQueue;
        private Thread _pXUpdateThread;
        /// <summary>
        /// Worker thread to handle replace messages
        /// </summary>
        private OrderReplaceProcessor _replaceProcessor;

        /// <summary>
        /// Stores cancel and modify request data
        /// </summary>
        private Queue<RequestData> _replaceQueue;

        private Thread _replaceUpdateThread;
        private bool _runWD = true;
        /// <summary>
        /// state passed in with the start request - this should be XML used to construct m_State
        /// </summary>
        private string _startState = "";

        private NewStatusMessage _statusMessage = null;
        /// <summary>
        /// Timer for those algos that require some time based processing
        /// </summary>
        private System.Timers.Timer _timer;

        /// <summary>
        /// Timer interval used for the timer
        /// </summary>
        private long _timerInterval = 1000;

        private bool _useWatchDogStart = false;

        /// <summary>
        /// Watch dog thread - looks after starting BB API for lockups and
        /// failed api starts
        /// </summary>
        private Thread _wDThread;

        private string appPath = "";

        /// <summary>
        /// Clients of this driver
        /// </summary>
        //protected List<IClient> _clients;
        private DOMUpdate domUpdate;

        private BlockingCollection<IMessage> inboundMessages;

        /// <summary>
        /// Create a logger for use in this Driver
        /// </summary>
        private ILog log = null;

        private BlockingCollection<IMessage> outboundMessages;

        private IProductManager productManager = null;

        private ProductUpdate productUpdate;

        public  AccountUpdate AccountUpdate { get; set; }

        private BlockingCollection<IPXUpdate> pxUpdates;

        private BlockingCollection<RequestData> replaceRequests;

        private TradeSignalUpdate tradeSignalUpdate;

        private IDriverDef driverDef;

        

        public DriverBase()
        {
        }

        public DriverBase(ILog log)
        {
            /*
            // Set up logging - will participate in the standard toolkit log
            log = log4net.LogManager.GetLogger("KaiTrade");

            wireLog = log4net.LogManager.GetLogger("KaiTradeWireLog");
            driverLog = log4net.LogManager.GetLogger("KaiDriverLog");
             */
            this.log = log;

            oRLog = log;
            tSLog = log;

            /// <summary>
            /// Create a logger to record low level details - a wire log
            /// </summary>
            wireLog = log;

            /// <summary>
            /// Create a logger to record driver specific info
            /// </summary>
            driverLog = log;

            _state = new DriverState();

            _clOrdIDOrderMap = new Dictionary<string, OrderContext>();
            _apiIDOrderMap = new Dictionary<string, OrderContext>();
            _pXContexts = new Dictionary<string, PXUpdateContext>();

            //_clients = new List<IClient>();
            _publisherRegister = new Dictionary<string, IPublisher>();
            _productGenericNameRegister = new Dictionary<string, string>();
            _productRegister = new Dictionary<string, IProduct>();
            _externalProduct = new Dictionary<string, object>();
            _sessions = new Dictionary<string, IDriverSession>();
            _priceAgregators = new Dictionary<string, List<IPriceAgregator>>();
            _identity = System.Guid.NewGuid().ToString();

            // setup the components info - used on status messages
            SetComponentInfo(out _module, this, this.m_ID, "DriverManager", 0, "");

            // creat a last status object - used to report status when requested
            _lastStatus = new Message();

            _lastStatus.Label = "Status";
            _lastStatus.Data = "Loaded";
            _lastStatus.AppState = (int)Status.loaded;
            _activeContextMap = new Dictionary<string, OrderContext>();
            _connectionState = null;

            log.Info("MainMessageHandler Created");

            pxUpdates = new BlockingCollection<IPXUpdate>();
            _pxUpdateProcessor = new PxUpdateProcessor(this, pxUpdates);
            _pXUpdateThread = new Thread(_pxUpdateProcessor.ThreadRun);
            _pXUpdateThread.Start();

            replaceRequests = new BlockingCollection<RequestData>();
            _replaceProcessor = new OrderReplaceProcessor(this, replaceRequests, log);
            _replaceUpdateThread = new Thread(_replaceProcessor.ThreadRun);
            _replaceUpdateThread.Start();

            //private BlockingCollection<List<IDOMSlot>> slotUpdates;
            inboundMessages = new BlockingCollection<IMessage>();
            /*
            _inboundProcessor = new MessageProcessorThread(this, inboundMessages);
            _inboundProcessorThread = new Thread(_inboundProcessor.ThreadRun);
            _inboundProcessorThread.Start();
             */

            outboundMessages = new BlockingCollection<IMessage>();
            /*
            _outboundProcessor = new MessageProcessorThread(ref _clients, outboundMessages);
            _outboundProcessorThread = new Thread(_outboundProcessor.ThreadRun);
            _outboundProcessorThread.Start();
            */
        }

        public string AppPath
        {
            get { return appPath; }
            set { appPath = value; }
        }

        public DriverConnectionStatus[] ConnectionState
        {
            get { return _connectionState; }
            set { _connectionState = value; }
        }

        public string[] Currencies
        {
            get { return _currencies; }
            set { _currencies = value; }
        }

        public ILog Log
        {
            get { return log; }
            set { log = value; }
        }
        public IDriverDef DriverDef
        {
            get { return driverDef; }
            set { 
                driverDef = value;
                m_ID = value.Code;
            }
        }
        public NewMessage Message
        {
            get { return _message; }
            set { _message = value; }
        }

        public string Name { get; set; }
        /*
        /// <summary>
        /// Worker thread to handle Inbound messages
        /// </summary>
        private MessageProcessorThread  _inboundProcessor;
        private Thread _inboundProcessorThread;

        /// <summary>
        /// Worker thread to handle Outbound messages
        /// </summary>
        private MessageProcessorThread _outboundProcessor;
        private Thread _outboundProcessorThread;

        */
        public IProductManager ProductManager
        {
            get { return productManager; }
            set { productManager = value; }
        }
        public Dictionary<string, IProduct> ProductRegister
        {
            get { return _productRegister; }
            set { _productRegister = value; }
        }

        public NewStatusMessage StatusMessage
        {
            get { return _statusMessage; }
            set { _statusMessage = value; }
        }
        /// <summary>
        /// get/set timer interval - note this doen not change a running timer
        /// </summary>
        public long TimerInterval
        {
            get { return _timerInterval; }
            set { _timerInterval = value; }
        }

        public static void SetComponentInfo(out IModuleDef myModule, Object myObj, string myModuleName, string myPackageName, int nInstance, string myTag)
        {
            try
            {
                myModule = new ModuleDef();
                myModule.Name = myModuleName;
                myModule.PackageFullName = myPackageName;
                myModule.Instance = nInstance;
                myModule.HostName = myPackageName + ":" + nInstance.ToString();
                myModule.Server = System.Environment.MachineName;

                // Other component info

                Assembly myAssy = myObj.GetType().Assembly;

                string myAssyFullName = myAssy.FullName;

                myModule.PackageFullName = myAssyFullName;
                int myVersionStart = myAssyFullName.IndexOf("Version=");
                myVersionStart += 8;
                int myCultureStart = myAssyFullName.IndexOf("Culture=");

                string myAssyVersion = myAssyFullName.Substring(myVersionStart, myCultureStart - myVersionStart);
                myAssyVersion = myAssyVersion.Trim();

                myModule.PackageVersion = myAssyVersion;

                Process myCurrentProcess = Process.GetCurrentProcess();
                myModule.HostModule = myCurrentProcess.MainModule.ModuleName;
                myModule.HostFileName = myCurrentProcess.MainModule.FileName;
                myModule.HostVersionInfo = myCurrentProcess.MainModule.FileVersionInfo.ToString();

                myModule.Tag = myTag;
            }
            catch (Exception myE)
            {
                //m_Log.Error("SetComponentInfo", myE);
                myModule = null;
            }
        }

        /// <summary>
        /// Apply new cancel requests
        /// </summary>
        /// <param name="myMsg"></param>
        public void ApplyCancelRequest(IMessage myMsg)
        {
            try
            {
                if (1 == 1)
                {
                    string mnemonic = "";
                    ICancelOrderRequest cancelRequest;
                    cancelRequest = JsonConvert.DeserializeObject<CancelOrderRequest>(myMsg.Data);
                    CancelRequestData r = new CancelRequestData(crrType.cancel, cancelRequest);

                    // Get the context - we must have this to access the order
                    if (_clOrdIDOrderMap.ContainsKey(cancelRequest.OrigClOrdID))
                    {
                        r.OrderContext = _clOrdIDOrderMap[cancelRequest.OrigClOrdID];
                        _clOrdIDOrderMap.Add(r.ClOrdID, r.OrderContext);
                    }
                    else
                    {
                        //sendCancelReplaceRej(replaceData.LastQFMod, QuickFix.CxlRejReason.UNKNOWN_ORDER, "an order does not exist for the modifyOrder");
                        Exception myE = new Exception("an order does not exist for the cancelOrder");
                        throw myE;
                    }

                    try
                    {
                        if (oRLog.IsInfoEnabled)
                        {
                            oRLog.Info("cancelOrder:context" + r.OrderContext.ToString());
                            oRLog.Info("cancelOrder:order" + r.OrderContext.ExternalOrder.ToString());
                        }
                    }
                    catch
                    {
                    }

                    r.OrderContext.ClOrdID = r.ClOrdID;
                    r.OrderContext.OrigClOrdID = r.ClOrdID;

                    // use a blocking collection
                    replaceRequests.Add(r);
                }
                else
                {
                    //sync update
                    //DoApplyPriceUpdate(update);
                }
            }
            catch (Exception myE)
            {
                log.Error("ApplyReplaceRequest", myE);
            }
        }

        public void ApplyDOMUpdate(string mnemonic, IDOMSlot[] domSlots)
        {
            try
            {
                // uses a blocking collection
                //pxUpdates.Add(update);
            }
            catch (Exception myE)
            {
                log.Error("ApplyDOMUpdate", myE);
            }
        }

        public void ApplyPriceUpdate(IPXUpdate update)
        {
            try
            {
                // uses a blocking collection
                pxUpdates.Add(update);
            }
            catch (Exception myE)
            {
                log.Error("ApplyPriceUpdate", myE);
            }
        }

        /// <summary>
        /// Apply new replace requests
        /// </summary>
        /// <param name="myMsg"></param>
        public void ApplyReplaceRequest(IMessage myMsg)
        {
            try
            {
                if (1 == 1)
                {
                    string mnemonic = "";
                    IModifyOrderRequst modifyRequest;
                    modifyRequest = JsonConvert.DeserializeObject<ModifyOrderRequest>(myMsg.Data);
                    ModifyRequestData r = new ModifyRequestData(crrType.cancel, modifyRequest);

                    // Get the context - we must have this to access the order
                    if (_clOrdIDOrderMap.ContainsKey(r.OrigClOrdID))
                    {
                        r.OrderContext = _clOrdIDOrderMap[r.OrigClOrdID];
                        _clOrdIDOrderMap.Add(r.ClOrdID, r.OrderContext);
                    }
                    else
                    {
                        //sendCancelReplaceRej(replaceData.LastQFMod, QuickFix.CxlRejReason.UNKNOWN_ORDER, "an order does not exist for the modifyOrder");
                        Exception myE = new Exception("an order does not exist for the modifyOrder");
                        throw myE;
                    }

                    try
                    {
                        if (oRLog.IsInfoEnabled)
                        {
                            oRLog.Info("modifyOrder:context" + r.OrderContext.ToString());
                            oRLog.Info("modifyOrder:order" + r.OrderContext.ExternalOrder.ToString());
                        }
                    }
                    catch
                    {
                    }

                    // swap the clordid to the new ones  on our stored order

                    r.OrderContext.ClOrdID = r.ClOrdID;
                    r.OrderContext.OrigClOrdID = r.OrigClOrdID;

                    // Use blocking collection
                    replaceRequests.Add(r);
                }
                else
                {
                    //sync update
                    //DoApplyPriceUpdate(update);
                }
            }
            catch (Exception myE)
            {
                log.Error("ApplyReplaceRequest", myE);
            }
        }

        public void BarUpdateClients(string requestID, ITSItem[] bars)
        {
            if (BarUpdate != null)
            {
                BarUpdate(requestID, bars);
            }
        }

        public virtual OrderReplaceResult cancelOrder(CancelRequestData replaceData)
        {
            return OrderReplaceResult.error;
        }

        public void ConnectionStatusUpdateClients(string sessionID, IDriverConnectionStatus statusUpdate)
        {
            if (ConnectionStatusUpdate != null)
            {
                ConnectionStatusUpdate(this, sessionID, statusUpdate);
            }
        }

        public void DoApplyPriceUpdate(IPXUpdate update)
        {
            try
            {
            }
            catch (Exception myE)
            {
                log.Error("DoApplyPriceUpdate", myE);
            }
        }

        public bool IsCurrencyPair(string myPair)
        {
            bool bRet = false;
            try
            {
                string currA = "";
                string currB = "";
                if (myPair.Length == 6)
                {
                    currA = myPair.Substring(0, 3);
                    currB = myPair.Substring(3, 3);
                    if (IsCurrrenyCode(currA) && IsCurrrenyCode(currB))
                    {
                        return true;
                    }
                }
                else if (myPair.Length == 7)
                {
                    currA = myPair.Substring(0, 3);
                    currB = myPair.Substring(4, 3);
                    if (IsCurrrenyCode(currA) && IsCurrrenyCode(currB))
                    {
                        return true;
                    }
                }
            }
            catch (Exception myE)
            {
                log.Error("IsCurrencyPair", myE);
            }

            return bRet;
        }

        public bool IsCurrrenyCode(string myCurrency)
        {
            bool bRet = false;
            try
            {
                foreach (string myListCurr in _currencies)
                {
                    if (myListCurr == myCurrency)
                    {
                        return true;
                    }
                }
            }
            catch (Exception myE)
            {
            }
            return bRet;
        }

        public virtual OrderReplaceResult modifyOrder(ModifyRequestData replaceData)
        {
            return OrderReplaceResult.error;
        }

        public virtual void OpenPrices(IProduct product, int depthLevels, string requestID)
        {
            try
            {
                SubscribeMD(product, depthLevels, requestID);
            }
            catch (Exception myE)
            {
            }
        }

        /// <summary>
        /// Do an async update to any registered delegates
        /// </summary>
        /// <param name="update"></param>
        public void PriceUpdateClients(IPXUpdate update)
        {
            if (PriceUpdate != null)
            {
                PriceUpdate(update);
            }
        }

        /// <summary>
        /// Send a simple advisory message to all clients of the
        /// adapter
        /// </summary>
        /// <param name="myMessageText"></param>
        public void SendAdvisoryMessage(string myMessageText)
        {
            try
            {
                this.driverLog.Info(Name + " advisory:" + myMessageText);
                IDriverStatusMessage myDSM;
                setupAdvisory(out myDSM, myMessageText);
                IMessage myMessage = new Message();

                myMessage.Label = "DriverAdvisory";
                myMessage.Data = JsonConvert.SerializeObject(myDSM);
                SendStatusMessage(myMessage);
            }
            catch (Exception myE)
            {
                log.Error("SendAdvisoryMessage", myE);
            }
        }

        /// <summary>
        /// Resend the last status message
        /// </summary>
        public void SendLastStatusMessage()
        {
            try
            {
                SendStatusMessage(_lastStatus);
            }
            catch (Exception myE)
            {
            }
        }

        /// <summary>
        /// Send some message back to our clients
        /// </summary>
        /// <param name="myMessage"></param>
        public async void SendMessage(IMessage myMessage)
        {
            try
            {
                if (_message != null)
                {
                    _message(myMessage);
                }

                /*
                // do the update assync
                lock (((ICollection)_outboundProcessorQueue).SyncRoot)
                {
                    _outboundProcessorQueue.Enqueue(myMessage);
                    _outboundProcessorSyncEvents.NewItemEvent.Set();
                }
                 */
            }
            catch (Exception myE)
            {
                log.Error("SendMessage", myE);
            }
        }

        /// <summary>
        /// Send a FIX style response to our clients
        /// </summary>
        /// <param name="msgType"></param>
        /// <param name="myResponseMsg"></param>
        public void SendResponse(IMessageHeader inboundHeader, string msgType, string myResponseMsg)
        {
            try
            {
                SendResponse(inboundHeader.ClientID, inboundHeader.ClientSubID, inboundHeader.CorrelationID, msgType, myResponseMsg);
            }
            catch (Exception myE)
            {
                log.Error("sendResponse", myE);
            }
        }

        public void SendResponse(string targetID, string targetSubId, string correlationId, string msgType, string myResponseMsg)
        {
            try
            {
                // Create message envelope
                IMessage myMsg = new Message();

                // Set the raw FIX Message
                myMsg.Data = myResponseMsg;

                myMsg.Tag = m_ID;
                myMsg.AppSpecific = 0;
                myMsg.Label = msgType;
                myMsg.AppType = "FIX.4.3";
                myMsg.TargetID = targetID;
                myMsg.TargetSubID = targetSubId;
                myMsg.CorrelationID = correlationId;
                myMsg.CreationTime = System.DateTime.Now.ToString();

                // Send the exec report to all clients
                SendMessage(myMsg);
            }
            catch (Exception myE)
            {
                log.Error("sendResponse", myE);
            }
        }

        /// <summary>
        /// Send a status message to all of our clients
        /// </summary>
        /// <param name="myMessage"></param>
        public void SendStatusMessage(IMessage myMessage)
        {
            try
            {
                // only preserve status messages - do not save an advisory
                if (myMessage.Label == "DriverStatus")
                {
                    _lastStatus = myMessage;
                }
                if (StatusMessage != null)
                {
                    StatusMessage(myMessage);
                }
            }
            catch (Exception myE)
            {
                log.Error("SendStatusMessage", myE);
            }
        }

        /// <summary>
        /// Send a FIX style status message specifing all parameters
        /// </summary>
        /// <param name="myState"></param>
        /// <param name="myText"></param>
        /// <param name="myBegin"></param>
        /// <param name="mySID"></param>
        /// <param name="myTID"></param>
        /// <param name="myFixName"></param>
        public void SendStatusMessage(Status myState, string myText, string myBegin, string mySID, string myTID, string myFixName)
        {
            try
            {
                // Update a FIX session status
                UpdateStatus(myFixName, myBegin, myTID, mySID, myState, myText);

                IDriverStatusMessage myDSM;

                setupStatus(out myDSM, myState, myText);

                IDriverSession mySession = new DriverSession();
                mySession.SessionName = myFixName;
                mySession.BeginString = myBegin;
                mySession.SID = mySID;
                mySession.TID = myTID;
                mySession.UserName = myFixName;
                mySession.State = myState;
                mySession.Text = myText;
                myDSM.Sessions.Add(mySession);

                IMessage statusMsg = new Message();
                statusMsg.Format = "XML";
                statusMsg.Label = "DriverStatus";
                statusMsg.Data = JsonConvert.SerializeObject(myDSM);
                SendStatusMessage(statusMsg);
                _lastStatus = statusMsg;
            }
            catch (Exception myE)
            {
                log.Error("SendStatusMessage", myE);
            }
        }

        /// <summary>
        /// send a stuts message
        /// </summary>
        /// <param name="myState"></param>
        /// <param name="myText"></param>
        public void SendStatusMessage(Status myState, string myText)
        {
            try
            {
                this.driverLog.Info(Name + ":" + myState.ToString() + ":" + myText);
                // update the base session status
                UpdateStatus("DriverStatus", "", "", "", myState, myText);

                IDriverStatusMessage myDSM;

                setupStatus(out myDSM, myState, myText);

                IMessage statusMsg = new Message();
                statusMsg.Format = "XML";
                statusMsg.Label = "DriverStatus";
                statusMsg.Data = JsonConvert.SerializeObject(myDSM);
                SendStatusMessage(statusMsg);
                _lastStatus = statusMsg;
            }
            catch (Exception myE)
            {
                log.Error("SendStatusMessage", myE);
            }
        }

        /// <summary>
        /// Display or Hide any UI the driver has
        /// </summary>
        /// <param name="uiVisible">true => show UI, False => hide UI</param>
        public virtual void ShowUI(bool uiVisible)
        {
            // needs to be done by the subclass
        }

        /// <summary>
        /// Add a session with the name specified
        /// </summary>
        /// <param name="myName"></param>
        protected IDriverSession AddSession(string myName)
        {
            try
            {
                return AddSession(myName, "", "", "");
            }
            catch (Exception myE)
            {
                log.Error("AddSession", myE);
                return null;
            }
        }

        /// <summary>
        /// Add a session with the details specified
        /// </summary>
        /// <param name="myName"></param>
        /// <param name="myVersion"></param>
        /// <param name="myTID"></param>
        /// <param name="mySID"></param>
        protected IDriverSession AddSession(string myName, string myVersion, string myTID, string mySID)
        {
            try
            {
                IDriverSession mySession = new DriverSession(this, myName, myVersion, myTID, mySID);
                string myKey = mySession.Key;
                if (_sessions.ContainsKey(myKey))
                {
                    _sessions[myKey] = mySession;
                }
                else
                {
                    _sessions.Add(myKey, mySession);
                }
                return mySession;
            }
            catch (Exception myE)
            {
                log.Error("AddSession2", myE);
                return null;
            }
        }

        /// <summary>
        /// Get a set of TS data - the driver will implement this if supported
        /// </summary>
        /// <param name="myTSSet"></param>
        protected virtual void DoGetTSData(ref ITSSet myTSSet)
        {
            throw new Exception("TS1 Data requests are not supported.");
        }

        protected virtual void DoSend(IMessage myMsg)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        protected virtual void DoStart(string myState)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        protected virtual void DoStop()
        {
            throw new Exception("The method or operation is not implemented.");
        }

        protected virtual void DoUnRegister(IPublisher myPublisher)
        {
            try
            {
            }
            catch (Exception myE)
            {
            }
        }

        protected void StartTimer()
        {
            try
            {
                if (_timerInterval > 0)
                {
                    _timer = new System.Timers.Timer(_timerInterval);
                    _timer.Elapsed += new ElapsedEventHandler(OnTimer);
                    _timer.Interval = (double)_timerInterval;
                    _timer.Enabled = true;
                }
            }
            catch (Exception myE)
            {
            }
        }

        protected void StopTimer()
        {
            try
            {
                if (_timer != null)
                {
                    _timer.Enabled = false;
                }
                _timer = null;
            }
            catch (Exception myE)
            {
            }
        }

        protected virtual void SubscribeMD(IProduct product, int depthLevels, string requestID)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        /// <summary>
        /// called on each timer tick - implimented by the base class
        /// </summary>
        protected virtual void TimerTick()
        {
        }

        protected virtual void UnSubscribeMD(IPublisher myPub)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        /// <summary>
        /// Update a session status adding a session if needed
        /// </summary>
        /// <param name="myName"></param>
        /// <param name="myVersion"></param>
        /// <param name="myTID"></param>
        /// <param name="mySID"></param>
        /// <param name="myState"></param>
        /// <param name="myStatusText"></param>
        protected void UpdateStatus(string myName, string myVersion, string myTID, string mySID, Status myState, string myStatusText)
        {
            try
            {
                string mySessionKey = this.m_ID + ":" + myName;
                IDriverSession mySession = null;
                if (_sessions.ContainsKey(mySessionKey))
                {
                    mySession = _sessions[mySessionKey];
                }
                else
                {
                    mySession = this.AddSession(myName, myVersion, myTID, mySID);
                }
                mySession.State = myState;
                mySession.StatusText = myStatusText;
            }
            catch (Exception myE)
            {
                log.Error("UpdateStatus", myE);
            }
        }

        private void OnTimer(object source, ElapsedEventArgs e)
        {
            try
            {
                TimerTick();
            }
            catch (Exception myE)
            {
                log.Error("OnTimer", myE);
            }
        }
        /// <summary>
        /// Set up an advisory message - this give any clients general purpose advisory
        /// information - note that regular status messages should be used for any type of error
        /// </summary>
        /// <param name="myAdvisory"></param>
        /// <param name="myText"></param>
        private void setupAdvisory(out IDriverStatusMessage myDSM, string myText)
        {
            myDSM = new DriverStatusMessage();
            try
            {
                myDSM.Text = myText;
                myDSM.DriverCode = this.m_ID;
                myDSM.Module = _module.ToString();
            }
            catch (Exception myE)
            {
                log.Error("setupAdvisory", myE);
            }
        }
        /// <summary>
        /// Setup a general purpose status message
        /// </summary>
        /// <param name="myStatus"></param>
        /// <param name="myState"></param>
        /// <param name="myText"></param>
        private void setupStatus(out IDriverStatusMessage myDSM, Status myState, string myText)
        {
            myDSM = null;
            try
            {
                setupAdvisory(out myDSM, myText);
                myDSM.State = (int)myState;
            }
            catch (Exception myE)
            {
                log.Error("setupStatus", myE);
            }
        }
        #region Driver Members

        public BarUpdate BarUpdate
        {
            get { return _barUpdate; }
            set { _barUpdate = value; }
        }

        public ConnectionStatusUpdate ConnectionStatusUpdate
        {
            get { return _connectionStatusUpdate; }
            set { _connectionStatusUpdate = value; }
        }

        public DOMUpdate DOMUpdate
        {
            get { return domUpdate; }
            set { domUpdate = value; }
        }

        public string ID
        {
            get
            {
                return m_ID;
            }
            set
            {
                m_ID = value;
            }
        }

        /// <summary>
        /// If set to true then run on the live market
        /// </summary>
        public bool LiveMarket
        {
            get
            { return _liveMarket; }
            set
            {
                _liveMarket = value;
                if (_liveMarket)
                {
                    log.Info("LiveMarket:True");
                }
                else
                {
                    log.Info("LiveMarket:False");
                }
            }
        }

        public PriceUpdate PriceUpdate
        {
            get { return _priceUpdate; }
            set { _priceUpdate = value; }
        }

        public ProductUpdate ProductUpdate
        {
            get { return productUpdate; }
            set { productUpdate = value; }
        }

        public string Tag
        {
            get
            {
                return m_Tag;
            }
            set
            {
                m_Tag = value;
            }
        }

        public TradeSignalUpdate TradeSignalUpdate
        {
            get { return tradeSignalUpdate; }
            set { tradeSignalUpdate = value; }
        }

        protected bool UseWatchDogStart
        {
            get { return _useWatchDogStart; }
            set { _useWatchDogStart = value; }
        }

        /// <summary>
        /// Get the running status of some driver
        /// compliments the StatusRequest();
        /// </summary>
        public virtual IDriverConnectionStatus[] GetConnectionStatus(IDriverSession[] sessions)
        {
            // returns none on all states - unless overridden
            return null;
        }

        public IDriverState GetState()
        {
            throw new Exception("The method or operation is not implemented.");
        }
        public virtual List<IVenueTradeDestination> GetTradeDestinations(string cfiCode)
        {
            // returns an empty list  if not overrridden
            return new List<IVenueTradeDestination>();
        }

        public void OnMessage(IMessage myMessage)
        {
            try
            {
                DoSend(myMessage);
            }
            catch (Exception myE)
            {
                log.Error("OnMessage", myE);
            }
        }

        public void OnStatusMessage(IMessage myMessage)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Process a set of order - this is driver specific and used to provide
        /// some access to specific driver functions - it should be avoided as it will
        /// be depricated
        /// </summary>
        /// <param name="cmdName"></param>
        /// <param name="orderIds"></param>
        /// <returns></returns>
        public virtual int ProcessOrders(string cmdName, string[] orderIds)
        {
            return 0;
        }

        /// <summary>
        /// Request any conditions that the driver supports- note that this
        /// is asyncronous the driver will add any conditions using the Facade
        /// </summary>
        public virtual void RequestConditions()
        {
        }

        /// <summary>
        /// Request the product details, get the driver to access the product and fill in
        /// product details in the kaitrade product object.
        /// Note that not all drivers support this and that the call may take some
        /// time to set the values.
        /// </summary>
        /// <param name="myProduct"></param>
        public virtual void RequestProductDetails(IProduct myProduct)
        {
            try
            {
                log.Error("RequestProductDetails:product i/f - Driver Base class cannot process");
            }
            catch (Exception myE)
            {
                log.Error("RequestProductDetails", myE);
            }
        }

        /// <summary>
        /// Will request any trade systems that the driver supports - note that this
        /// is asyncronous the driver will add any trading systems using the Facade
        /// </summary>
        public virtual void RequestTradeSystems()
        {
        }
        public void Send(IMessage myMsg)
        {
            try
            {
                /*
                if (_message != null)
                {
                    _message(myMsg);
                }
                 */
                DoSend(myMsg);
                //inboundMessages.Add(myMsg);
            }
            catch (Exception myE)
            {
                log.Error("Driver.Send", myE);
            }
        }
        public void SetParent(IDriverManager myParent)
        {
            _parent = myParent;
        }

        public void Start(string myState)
        {
            try
            {
                _startState = myState;
                // set this as a publisher
                //Factory.Instance().GetPublisherManager().Add(this);

                //set the  default location of the config path

                _configPath = Directory.GetCurrentDirectory() + @"\config\";
                try
                {
                    // load the driver state
                    if (myState.Length > 0)
                    {
                        _state = JsonConvert.DeserializeObject<DriverState>(myState);
                        _configPath = _state.ConfigPath;
                        _useAsyncPriceUpdates = _state.AsyncPrices;
                        _queueReplaceRequests = _state.QueueReplaceRequests;
                    }
                }
                catch (Exception myE)
                {
                    log.Error("Driver.Start:cannot read state", myE);
                }
                if (this.UseWatchDogStart)
                {
                    /// the watch dog issues the start req
                    StartWD();
                }
                else
                {
                    DoStart(_startState);
                }
            }
            catch (Exception myE)
            {
                log.Error("Driver.Start", myE);
            }
        }
        public void StatusRequest()
        {
            try
            {
                SendLastStatusMessage();
            }
            catch (Exception myE)
            {
                log.Error("Driver.StatusRequest", myE);
            }
        }

        public void Stop()
        {
            try
            {
                _runWD = false;
                if (this.UseWatchDogStart)
                {
                    _wDThread.Abort();
                }

                try
                {
                    DoStop();
                }
                catch (Exception myE)
                {
                    this.driverLog.Error("Stop:DoStop in base", myE);
                }

                _publisherRegister.Clear();
                _sessions.Clear();
            }
            catch (Exception myE)
            {
                log.Error("Stop:", myE);
            }
        }

        public void UnRegister(IPublisher myPublisher)
        {
            try
            {
                DoUnRegister(myPublisher);
            }
            catch (Exception myE)
            {
                log.Error("Driver.UnRegister:publisher", myE);
            }
        }

        private void runWDThread()
        {
            try
            {
                while (_runWD)
                {
                    if ((_status != Status.open) && (_status != Status.opening))
                    {
                        DoStart(_startState);
                    }

                    Thread.Sleep(5000);
                }
            }
            catch (Exception myE)
            {
                this.SendStatusMessage(Status.error, "WD Thread terminated :" + myE.Message);
                log.Error("runWDThread", myE);
            }
        }

        /// <summary>
        /// Start the watchdog thread
        /// </summary>
        /// <param name="myState"></param>
        private void StartWD()
        {
            try
            {
                _runWD = true;
                _wDThread = new Thread(new ThreadStart(this.runWDThread));
                _wDThread.SetApartmentState(ApartmentState.STA);
                _wDThread.Start();
            }
            catch (Exception myE)
            {
                this.SendStatusMessage(Status.error, "Issue starting watchdog thread" + myE.Message);
                log.Error("StartWD", myE);
            }
        }
        #endregion Driver Members

        #region Driver Members

        public List<IDriverSession> Sessions
        {
            get
            {
                List<IDriverSession> mySessions = new List<IDriverSession>();
                foreach (IDriverSession mySession in _sessions.Values)
                {
                    mySessions.Add(mySession);
                }
                return mySessions;
            }
        }

        public Status Status
        {
            get { return _status; }
        }

        public void AddProductDirect(string filePath)
        {
            using (StreamReader sr = new StreamReader(filePath))
            {
                while (!sr.EndOfStream)
                {
                    Product product = JsonConvert.DeserializeObject<Product>(sr.ReadLine());
                    AddProductDirect(product);
                }
            }
        }

        /// <summary>
        /// Add product to the product manager - no events are raised
        /// </summary>
        /// <param name="productdata"></param>
        /// <returns></returns>
        public void AddProductDirect(Product productdata)
        {
           
            try
            {
                if(ProductUpdate != null)
                {
                    ProductUpdate(ProductUpdateType.NEW, productdata);
                }
                var productId = ProductManager.AddProduct(productdata);
            }
            catch (Exception myE)
            {
            }
            
        }

        public IProduct AddProductDirect(string myCFICode, string myExchangeID, string ExDestination, string mySymbol, string mySecID, string myMMY, string myStrikePx, bool doEvent)
        {
            IProduct product = null;
            try
            {
                double? strikePx = new double?();
                if (myStrikePx.Length > 0)
                {
                    strikePx = double.Parse(myStrikePx);
                }
                throw new Exception("not implimented");
                //product = AddProductDirect(mySecID, myCFICode, myExchangeID, ExDestination, mySymbol, mySecID, myMMY, myStrikePx, "", strikePx, doEvent);
            }
            catch (Exception myE)
            {
            }
            return product;
        }

        /// <summary>
        /// Get a set of time series data - if supported
        /// </summary>
        /// <param name="myTSSet"></param>
        public virtual void DisconnectTSData(ITSSet myTSSet)
        {
            //DoGetTSData(ref  myTSSet);
        }

        /// <summary>
        /// Get a set of time series data - if supported
        /// </summary>
        /// <param name="myTSSet"></param>
        public virtual void RequestTSData(ITSSet[] tsSet)
        {
            //DoGetTSData(myTSSet);
        }

        public void sendCancelRej(IMessageHeader inboundHeader, ICancelOrderRequest myReq, CxlRejReason myReason, string myReasonText)
        {
            try
            {
                string myFixMsg = RequestBuilder.DoRejectCxlReq(myReq, "UNKNOWN", myReason, myReasonText);
                // send our response message back to the clients of the adapter
                SendResponse(inboundHeader, KaiTrade.Interfaces.MessageType.REJECTCANCEL, myFixMsg);
            }
            catch (Exception myE)
            {
                log.Error("sendCancelRej", myE);
            }
        }

        public void sendCancelReplaceRej(IMessageHeader inboundHeader, IModifyOrderRequst myReq, CxlRejReason myReason, string myReasonText)
        {
            try
            {
                string myFixMsg = RequestBuilder.DoRejectModReq(myReq, "UNKNOWN", myReason, myReasonText);
                // send our response message back to the clients of the adapter
                SendResponse(inboundHeader, KaiTrade.Interfaces.MessageType.REJECTMODIFYORDER, myFixMsg);
            }
            catch (Exception myE)
            {
                log.Error("sendCancelReplaceRej", myE);
            }
        }

        public void sendExecReport(OrderContext context, string orderID, string status, string execType, decimal lastQty, int leavesQty, int cumQty, decimal lastPx, decimal avePx, string text, string ordRejReason)
        {
            Fill fill = new Fill();
            fill.OrderID = orderID;
            fill.ClOrdID = context.ClOrdID;
            fill.OrigClOrdID = context.OrigClOrdID;
            fill.OrderStatus = status;
            fill.ExecType = execType;
            fill.FillQty = lastQty;
            fill.LeavesQty = leavesQty;
            fill.CumQty = cumQty;
            fill.LastPx = lastPx;
            fill.AvgPx = avePx;
            fill.Text = text;
            fill.OrdRejReason = ordRejReason;
            sendExecReport(context as IMessageHeader, fill);
        }

        public void sendExecReport(string orderID, string status, string execType,decimal lastQty, int leavesQty, int cumQty, decimal lastPx, decimal avePx, string text, string ordRejReason)
        {
            try
            {
                Fill fill = new Fill();
                fill.OrderID = orderID;
                fill.OrderStatus = status;
                fill.ExecType = execType;
                fill.FillQty = lastQty;
                fill.LeavesQty = leavesQty;
                fill.CumQty = cumQty;
                fill.LastPx = lastPx;
                fill.AvgPx = avePx;
                fill.Text = text;
                fill.OrdRejReason = ordRejReason;
            }
            catch (Exception myE)
            {
                log.Error("sendExecReport", myE);
            }
        }

        public void sendExecReport(OrderContext myCntx, string orderID, string status, string execType, decimal  lastQty, int leavesQty, int cumQty, decimal lastPx, decimal avePx)
        {
            myCntx.OrdStatus = status;
            myCntx.ExecType = execType;
            myCntx.LastAveragePrice = avePx;
            Fill fill = new Fill();
            fill.OrderID = orderID;
            fill.OrderStatus = status;
            fill.ExecType = execType;
            fill.FillQty = lastQty;
            fill.LeavesQty = leavesQty;
            fill.CumQty = cumQty;
            fill.LastPx = lastPx;
            fill.AvgPx = avePx;

            sendExecReport(myCntx as IMessageHeader, fill);
        }

        public void sendExecReport(IMessageHeader inboundHeader, ISubmitRequest nos, string status, string execType, string text, string ordRejReason)
        {
            Fill fill = new Fill();
            if (nos.ClOrdID != null)
            {
                fill.ClOrdID = nos.ClOrdID;
            }
            fill.OrderID = "";
            fill.OrderStatus = status;
            fill.ExecType = execType;
            fill.FillQty = 0;
            fill.LeavesQty = 0;
            fill.CumQty = 0;
            fill.LastPx = 0M;
            fill.AvgPx = 0M;
            fill.Text = text;
            fill.OrdRejReason = ordRejReason;

            sendExecReport(inboundHeader, fill);
        }

        public void sendExecReport(IMessageHeader inboundHeader, IFill fill)
        {
            try
            {
                string execReportixMsg = JsonConvert.SerializeObject(fill);

                // send our response message back to the clients of the adapter
                SendResponse(inboundHeader, KaiTrade.Interfaces.MessageType.EXECUTION, execReportixMsg);
            }
            catch (Exception myE)
            {
                log.Error("sendExecReport", myE);
            }
        }

        /// <summary>
        /// set status and report change
        /// </summary>
        /// <param name="myStatus"></param>
        public void setStatus(Status myStatus)
        {
            try
            {
                _status = myStatus;
                this.SendStatusMessage(Status.open, Name + "Status changed to:" + _status.ToString());
                this.SendAdvisoryMessage(Name + "Status changed to:" + _status.ToString());
                log.Info(Name + "Status changed to:" + _status.ToString());
                wireLog.Info(Name + "Status changed to:" + _status.ToString());
            }
            catch (Exception myE)
            {
                log.Error("setStatus", myE);
            }
        }

        public IProduct XXAddProductDirect(string mnemonic, string myCFICode, string myExchangeID, string ExDestination, string mySymbol, string mySecID, string myMMY, string myStrikePx, string myCurrency, double? strikePx, bool doEvent)
        {
            IProduct product = null;
            try
            {
                throw new Exception("not implimented");
                //product = _facade.AddProduct(mnemonic, Name, mySecID, myExchangeID, ExDestination, myCFICode, myMMY, myCurrency, strikePx, doEvent);
            }
            catch (Exception myE)
            {
                log.Error("AddProductDirect", myE);
            }
            return product;
        }

        /// <summary>
        /// Add an account to the KaiTrade Account manager
        /// </summary>
        /// <param name="myAccountCode">Brokers account code (this will be used on orders</param>
        /// <param name="myLongName">Descriptive name</param>
        /// <param name="myFirmCode">Free format test - Code of the broker firm</param>
        protected void AddAccount(string myAccountCode, string myLongName, string myFirmCode)
        {
            Account account = new Account();
            account.LongName = myLongName;
            // use the descriptive name for the ID since we use that as the lookup on orders
            account.AccountCode = myAccountCode;
            account.VenueCode = m_ID;
            account.FirmCode = myFirmCode;

            if(AccountUpdate != null)
            {
                AccountUpdate(account);
            }

            
        }

        protected void AddContract(string myExchangeID, string myCode, string myName)
        {
        }

        /// <summary>
        /// Add an exchange supported by the venue concerned
        /// </summary>
        /// <param name="myCode"></param>
        /// <param name="myName"></param>
        protected void AddExchange(string myCode, string myName)
        {
        }

        protected OrderContext GetOrderContextApiID(string myApiID)
        {
            OrderContext myCntx = null;
            if (_apiIDOrderMap.ContainsKey(myApiID))
            {
                myCntx = _apiIDOrderMap[myApiID];
            }
            return myCntx;
        }

        protected OrderContext GetOrderContextClID(string myClOrdID)
        {
            OrderContext myCntx = null;
            if (_clOrdIDOrderMap.ContainsKey(myClOrdID))
            {
                myCntx = _clOrdIDOrderMap[myClOrdID];
            }
            return myCntx;
        }

        protected void identifyPendingContexts(long delay, ILog log)
        {
            try
            {
                foreach (OrderContext cntx in _activeContextMap.Values)
                {
                    if (cntx.isPending(delay))
                    {
                        log.Info("identifyPendingContexts:pending:" + cntx.ToString());
                    }
                }
            }
            catch (Exception myE)
            {
                log.Error("identifyPendingContexts", myE);
            }
        }

        /// <summary>
        /// record an order context
        /// </summary>
        /// <param name="myClOrdID"></param>
        /// <param name="myFixMsg"></param>
        /// <param name="myApiOrder"></param>
        protected OrderContext RecordOrderContext(string clientId, string clientSubId, string corrlationId, string myClOrdID, object myApiOrder, string myApiID)
        {
            OrderContext myCntx = null;
            try
            {
                myCntx = new OrderContext(clientId, clientSubId, corrlationId);
                myCntx.ClOrdID = myClOrdID;
                myCntx.ExternalOrder = myApiOrder;

                myCntx.APIOrderID = myApiID;
                if (_clOrdIDOrderMap.ContainsKey(myClOrdID))
                {
                    _clOrdIDOrderMap[myClOrdID] = myCntx;
                }
                else
                {
                    _clOrdIDOrderMap.Add(myClOrdID, myCntx);
                }
                if (myApiID.Length > 0)
                {
                    if (_apiIDOrderMap.ContainsKey(myApiID))
                    {
                        _apiIDOrderMap[myApiID] = myCntx;
                    }
                    else
                    {
                        _apiIDOrderMap.Add(myApiID, myCntx);
                    }
                }

                if (!_activeContextMap.ContainsKey(myCntx.Identity))
                {
                    _activeContextMap.Add(myCntx.Identity, myCntx);
                }
            }
            catch (Exception myE)
            {
                log.Error("RecordOrderContext", myE);
            }
            return myCntx;
        }

        /// <summary>
        /// Record a context against a particular cl order ID
        /// </summary>
        /// <param name="myClOrdID"></param>
        /// <param name="myCntx"></param>
        protected void RecordOrderContext(string myClOrdID, OrderContext myCntx)
        {
            try
            {
                if (_clOrdIDOrderMap.ContainsKey(myClOrdID))
                {
                    _clOrdIDOrderMap[myClOrdID] = myCntx;
                }
                else
                {
                    _clOrdIDOrderMap.Add(myClOrdID, myCntx);
                }
            }
            catch (Exception myE)
            {
            }
        }

        /// <summary>
        /// Record a context against a particular cl order ID
        /// </summary>
        /// <param name="myClOrdID"></param>
        /// <param name="myCntx"></param>
        protected void RecordOrderContextApiID(string myApiID, OrderContext myCntx)
        {
            try
            {
                if (_apiIDOrderMap.ContainsKey(myApiID))
                {
                    _apiIDOrderMap[myApiID] = myCntx;
                }
                else
                {
                    _apiIDOrderMap.Add(myApiID, myCntx);
                }
            }
            catch (Exception myE)
            {
            }
        }

        protected long removeInactiveContexts()
        {
            long numberActive = 0;
            try
            {
                List<string> removeIds = new List<string>();
                foreach (OrderContext cntx in _activeContextMap.Values)
                {
                    if (!cntx.isActive())
                    {
                        removeIds.Add(cntx.Identity);
                    }
                }
                foreach (string id in removeIds)
                {
                    _activeContextMap.Remove(id);
                }
                numberActive = _activeContextMap.Count;
            }
            catch (Exception myE)
            {
                log.Error("removeInactiveContexts", myE);
            }
            return numberActive;
        }

        protected void SetContextCommand(OrderContext myCntx, ORCommand expectedCmd, ORCommand newCmd)
        {
            try
            {
                if (myCntx.CurrentCommand != expectedCmd)
                {
                    this.driverLog.Error("SetContextCommand:unexpected existing cmd:clordid:" + myCntx.ClOrdID + " existing:" + myCntx.CurrentCommand.ToString() + " expectd:" + expectedCmd.ToString() + " new:" + newCmd.ToString());
                }
                myCntx.CurrentCommand = newCmd;
            }
            catch (Exception myE)
            {
                this.driverLog.Error("SetContextCommand", myE);
            }
        }

        /// <summary>
        /// Set the status of all subscriptions
        /// </summary>
        /// <param name="myStatus"></param>
        protected virtual void setSubscriptionsStatus(Status myStatus)
        {
            try
            {
                this.driverLog.Info("setSubscriptionsStatus:" + myStatus.ToString());
                foreach (IPublisher myPub in _publisherRegister.Values)
                {
                    myPub.Status = myStatus;
                }
            }
            catch (Exception myE)
            {
                log.Error("setSubscriptionsStatus", myE);
            }
        }
        /*
        protected virtual void StartBarsGenerate(ITSSet myTSSet)
        {
            try
            {
                // try get an agregator
                IPriceAgregator priceAgregator = _facade.GetPriceAgregator("K2PriceAggregatorBase");
                if (priceAgregator != null)
                {
                    priceAgregator.TSSet = myTSSet;
                    AddPriceAgregator(priceAgregator);
                }
            }
            catch (Exception myE)
            {
            }
        }

        protected virtual void AddPriceAgregator(IPriceAgregator priceAgregator)
        {
            try
            {
                if (_priceAgregators.ContainsKey(priceAgregator.TSSet.Mnemonic))
                {
                    if (_priceAgregators[priceAgregator.TSSet.Mnemonic].Contains(priceAgregator))
                    {
                        // do nothing already in the list
                    }
                    else
                    {
                        _priceAgregators[priceAgregator.TSSet.Mnemonic].Add(priceAgregator);
                    }
                }
                else
                {
                    List<IPriceAgregator> agList = new List<IPriceAgregator>();
                    agList.Add(priceAgregator);
                    _priceAgregators.Add(priceAgregator.TSSet.Mnemonic, agList);
                }
            }
            catch (Exception myE)
            {
            }
        }
         */
        #endregion Driver Members
    }
}