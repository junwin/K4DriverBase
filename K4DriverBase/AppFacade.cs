using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using K4ServiceInterface;
using System.ComponentModel;
using System.Reflection;
//using System.Windows.Forms;
using KaiTrade.Interfaces;
using log4net;

namespace DriverBase
{
    /// <summary>
    /// Provides access to the environment
    /// </summary>
    public class AppFacade : IFacade
    {
        private string _appPath = "";
        private IPriceHandler _priceHandler = null;

        /// <summary>
        /// Singleton OrderManager
        /// </summary>
        private static volatile AppFacade s_instance;

        /// <summary>
        /// used to lock the class during instantiation
        /// </summary>
        private static object s_Token = new object();

        /// <summary>
        /// Logger
        /// </summary>
        public ILog m_Log = null;

        private IProductManager productManager = null;

        

        private IOrderSvc orderService = null;

        
       

        public AppFacade(IProductManager productManager, IOrderSvc orderSvc)
        {
            this.productManager = productManager;
            this.orderService = orderSvc;
            //_appPath = Application.StartupPath;
        }

        public string AppPath
        {
            get { return _appPath; }
            set { _appPath = value; }
        }

        public IProductManager ProductManager
        {
            get { return productManager; }
            set { productManager = value; }
        }

        public IOrderSvc OrderService
        {
            get { return orderService; }
            set { orderService = value; }
        }


        

        public ITradeSignalManager TradeSignalManager
        { get; set; }

        public void AddProduct(string genericName, string tradeVenue)
        {
        }

        public void RequestProductDetails(KaiTrade.Interfaces.IProduct prod)
        {
        }

        public KaiTrade.Interfaces.IProduct AddProduct(string mnemonic, string Name, string mySecID, string myExchangeID, string ExDestination, string myCFICode, string myMMY, string myCurrency, double? strikePx, bool doEvent)
        {
            return null;
        }

        

        public IPriceHandler PriceHandler
        {
            get { return _priceHandler; }
            set { _priceHandler = value; }
        }



        public string GetUserProfileProperty(string section, string propertyName)
        {
            return "";
        }

        public void ProcessPositionUpdate(KaiTrade.Interfaces.IPosition position)
        {
        }

        public void ProcessITradeSignal(ITSSet mySet, ITradeSignal signal)
        {
            throw new Exception("not implimented");
        }


        public void UpdateOrderInformation(IOrder order, List<IFill> fills)
        {

        }

       
       
         

        public void ApplyUpdate(KaiTrade.Interfaces.IPXUpdate update)
        {
            throw new Exception("Not implimented");
        }

        public IPriceAgregator GetPriceAgregator(string name)
        {
            throw new Exception("Not implimented");
        }
          

       
    }


}
