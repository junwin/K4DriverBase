using System;
using System.Collections.Generic;
using System.IO;
using log4net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using KaiTrade.Interfaces;
using K4ServiceInterface;
using Moq;


namespace DriverBaseTest
{
    [TestClass]
    public class ProductTest
    {
        IProductManager productManager;  

        [TestMethod]
        [DeploymentItem("Data/SimProduct.txt", "Data")]
        public void LoadFileTest()
        {
            ILog log =  Mock.Of<ILog>();

            productManager = new DriverBase.ProductManager(log);
            DriverBase.DriverBase driver = new DriverBase.DriverBase( log);
            driver.ProductManager = productManager;
            driver.ProductUpdate += new K4ServiceInterface.ProductUpdate(ProductUpdate);
            string currentDirectory = Directory.GetCurrentDirectory();
            driver.AddProductDirect(@"Data/SimProduct.txt");
            List<KaiTrade.Interfaces.IProduct> products = driver.ProductManager.GetProducts("KTACQG", "", "");
            Assert.AreEqual(products.Count,0);
            products = driver.ProductManager.GetProducts("KTASIM", "", "");
            Assert.AreEqual(products.Count, 3);
        }

        public void ProductUpdate(string updateType, KaiTrade.Interfaces.IProduct product)
        {
            productManager.AddProduct(product);
        }
    }
}
