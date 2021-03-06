﻿/***************************************************************************
 *
 *      Copyright (c) 2009,2010,2011 KaiTrade LLC (registered in Delaware)
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

using System;
using System.IO;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using System.Diagnostics;
using System.Timers;
using System.Threading;

using System.Reflection;
using K4ServiceInterface;

namespace DriverBase
{
    public class DriverConnectionStatus : IDriverConnectionStatus
    {
        K4ServiceInterface.StatusConditon m_HistoricData = K4ServiceInterface.StatusConditon.none;
        StatusConditon m_OrderRouting = StatusConditon.none;
        StatusConditon m_Prices  = StatusConditon.none;

        public DriverConnectionStatus()
        {
        }

        public DriverConnectionStatus(StatusConditon orderRouting, StatusConditon prices)
        {
            m_OrderRouting = orderRouting;
            m_Prices = prices;
        }
        public StatusConditon HistoricData
        {
            get
            {
                return m_HistoricData;
            }
            set
            {
                m_HistoricData = value;
            }
        }

        public StatusConditon OrderRouting
        {
            get
            {
                return m_OrderRouting;
            }
            set
            {
                m_OrderRouting = value;
            }
        }

        public StatusConditon Prices
        {
            get
            {
                return m_Prices;
            }
            set
            {
                m_Prices = value;
            }
        }
    }
}
