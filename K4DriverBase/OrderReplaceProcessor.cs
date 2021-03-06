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
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Collections;
using System.Linq;
using K4ServiceInterface;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using log4net;

namespace DriverBase
{
    /// <summary>
    /// Provide asyn processing of replace requests - note that this
    /// will collapse requests until they can be actioned by the broker API
    /// </summary>
    public class OrderReplaceProcessor
    {
        BlockingCollection<RequestData> replaceRequests;
        DriverBase _Handler;

        private Dictionary<string, RequestData> _replaceRequest;

        /// <summary>
        /// Create a logger to record driver specific info 
        /// </summary>
        public ILog  m_DriverLog = null;

        public OrderReplaceProcessor(DriverBase myHandler, BlockingCollection<RequestData> replaceRequestsCollection, ILog log)
        {
            replaceRequests = replaceRequestsCollection;
            _Handler = myHandler;
            _replaceRequest = new Dictionary<string, RequestData>();
            m_DriverLog = log;
           
        }
        // Consumer.ThreadRun
        public void ThreadRun()
        {
            int count = 0;
            try
            {
                foreach (var replace in replaceRequests.GetConsumingEnumerable())
                {

                    if (_replaceRequest.ContainsKey(replace.Mnemonic))
                    {
                        _replaceRequest[replace.Mnemonic] = (replace);
                    }
                    else
                    {
                        _replaceRequest.Add(replace.Mnemonic, replace);
                    }
                    OrderReplaceResult result = OrderReplaceResult.error;
                    if (_replaceRequest[replace.Mnemonic].CRRType == crrType.replace)
                    {
                        result = _Handler.modifyOrder(_replaceRequest[replace.Mnemonic] as ModifyRequestData);
                    }
                    else if (_replaceRequest[replace.Mnemonic].CRRType == crrType.cancel)
                    {
                        result = _Handler.cancelOrder(_replaceRequest[replace.Mnemonic] as CancelRequestData);
                    }
                    else
                    {
                        // error condition
                        m_DriverLog.Error("Invild CancelReplace type");
                    }

                    switch (result)
                    {
                        case OrderReplaceResult.success:
                            _replaceRequest.Remove(replace.Mnemonic);
                            break;
                        case OrderReplaceResult.error:
                            _replaceRequest.Remove(replace.Mnemonic);
                            break;
                        case OrderReplaceResult.replacePending:
                            _replaceRequest[replace.Mnemonic].RetryCount += 1;
                            break;
                        case OrderReplaceResult.cancelPending:
                            _replaceRequest[replace.Mnemonic].RetryCount += 1;
                            break;
                        default:
                            _replaceRequest[replace.Mnemonic].RetryCount += 1;
                            break;
                    }

                    // action any remaining requests
                    List<string> mnemonics = new List<string>();
                    foreach (string mnemonic in _replaceRequest.Keys)
                    {
                        mnemonics.Add(mnemonic);
                    }
                    foreach (string mnemonic in mnemonics)
                    {
                        result = _Handler.modifyOrder(_replaceRequest[mnemonic] as ModifyRequestData);
                        switch (result)
                        {
                            case OrderReplaceResult.success:
                                _replaceRequest.Remove(mnemonic);
                                break;
                            case OrderReplaceResult.error:
                                _replaceRequest.Remove(mnemonic);
                                break;
                            case OrderReplaceResult.replacePending:
                                break;
                            default:
                                break;
                        }

                    }
                }

                count++;
            }
            catch (Exception myE)
            {
                m_DriverLog.Error("Main Loop:", myE);
            }


        }


    } 
}
