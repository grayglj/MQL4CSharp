﻿/*
Copyright 2016 Jason Separovic

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

using log4net;
using System;
using System.Collections.Generic;
using mqlsharp.Util;
using MQL4CSharp.Base.Common;
using MQL4CSharp.Base.Enums;
using NodaTime;

namespace MQL4CSharp.Base
{
    public abstract class BaseStrategy : MQLBase
    {
        public static readonly ILog LOG = LogManager.GetLogger(typeof(BaseStrategy));

        private bool evalOncePerCandle = true;
        private bool closeOnOpposingSignal = true;
        private Dictionary<string, StrategyMetaData> strategyMetaDataMap;
        private double yesterdaysHigh, yesterdaysLow, todaysHigh, todaysLow;
        private Dictionary<long, SignalResult> orderToSignalMap;
        public static DateTimeZone DATE_TZ = DateTimeZone.ForOffset(Offset.Zero);
        private String strategyName;
        private List<String> symbolList;
        private TIMEFRAME timeframe;

        private Dictionary<String, String> logHashMap;

        public BaseStrategy(Int64 ix) : base(ix)
        {
            this.symbolList = new List<string>();
            this.symbolList.Add(Symbol());
            this.timeframe = TIMEFRAME.PERIOD_CURRENT;
            strategyMetaDataMap = new Dictionary<string, StrategyMetaData>();
            logHashMap = new Dictionary<String, String>();
        }

        public BaseStrategy(int ix,
                            TIMEFRAME timeframe,
                            List<String> symbolList,
                            bool evalOncePerCandle = true,
                            bool closeOnOpposingSignal = true) : base(ix)
        {
            this.timeframe = timeframe;
            this.symbolList = symbolList;
            this.evalOncePerCandle = evalOncePerCandle;
            this.closeOnOpposingSignal = closeOnOpposingSignal;
            if (symbolList.Count == 0)
            {
                throw new Exception("SymbolList should not be empty in Strategy constructor");
            }
        }

        public BaseStrategy(int ix,
                            TIMEFRAME timeframe,
                            String symbol,
                            bool evalOncePerCandle = true,
                            bool closeOnOpposingSignal = true) : base(ix)
        {
            this.symbolList = new List<string>();
            this.symbolList.Add(symbol);
            this.timeframe = timeframe;
            this.evalOncePerCandle = evalOncePerCandle;
            this.closeOnOpposingSignal = closeOnOpposingSignal;
        }


        public override void OnTick()
        {
            foreach (String symbol in symbolList)
            {
                try
                {
                    int total = this.OrdersTotal();
                    for (int i = 0; i < total; i++)
                    {
                        if (OrderSelect(i, (int)SELECTION_TYPE.SELECT_BY_POS, (int)SELECTION_POOL.MODE_TRADES) && OrderMagicNumber() == getMagicNumber(symbol))
                        {
                            this.manageOpenTrades(symbol, OrderTicket());
                        }
                    }


                    if (checkCandle(symbol, timeframe))
                    {
                        if (this.isAsleep(symbol))
                        {
                            return;
                        }

                        if (!this.filter(symbol))
                        {
                            return;
                        }

                        // Check for a signal
                        SignalResult signal = this.evaluate(symbol);
                        if (signal.getSignal() != SignalResult.NEUTRAL)
                        {
                            //LOG.Info("Executing...");
                            this.executeTrade(symbol, signal);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LOG.Error(ex);
                }
            }

        }

        public StrategyMetaData getStrategyMetaDataMap(String symbol, TIMEFRAME timeframe)
        {
            int tf = (int)timeframe;
            if (tf == 0)
            {
                tf = (int)Period();
            }
            string key = symbol + "_" + tf;
            if (!strategyMetaDataMap.ContainsKey(key))
            {
                strategyMetaDataMap[key] = new StrategyMetaData();
            }
            return strategyMetaDataMap[key];
        }

        public DateTime getMarketDateTime(String symbol)
        {
            return MarketTime(symbol);
        }

        public LocalDate getMarketLocalDate(String symbol)
        {
            DateTime date = getMarketDateTime(symbol);
            return new LocalDate(date.Year, date.Month, date.Day);
        }

        public double iCandleBodyHigh(String symbol, TIMEFRAME timeframe, int shift)
        {
            double open = iOpen(symbol, (int)timeframe, shift);
            double close = iClose(symbol, (int)timeframe, shift);
            return open >= close? open : close;
        }

        public double iCandleBodyLow(String symbol, TIMEFRAME timeframe, int shift)
        {
            double open = iOpen(symbol, (int)timeframe, shift);
            double close = iClose(symbol, (int)timeframe, shift);
            return open <= close? open : close;
        }

        public void logInfoOnce(ILog logger, String symbol, DateTime date, Direction direction, String action, String message)
        {
            String key = String.Format("{0} {1} {2} {3} {4}", logger, symbol, date, direction, action);

            if (logHashMap.ContainsKey(key))
            {
                return;
            }

            logHashMap[key] = null;

            logger.Info(String.Format("[{0}] [{1}] [{2}] - {3}", symbol, date, direction, message));
        }

        public KeyValuePair<Double, Double> getHighLowPairInRange(String symbol, TIMEFRAME timeframe, DateTime from, DateTime to)
        {
            double low = Double.MaxValue;
            double high = 0;
            double ilow, ihigh;
            for(int i = 0; ; i++)
            {
                DateTime itime = iTime(symbol, (int)timeframe, i);
                if(itime < from)
                {
                    // break out once past from
                    break;
                }
                if(itime < to || itime == to)
                {
                    ilow = iLow(symbol, (int)timeframe, i);
                    ihigh = iHigh(symbol, (int)timeframe, i);
                    if(ilow<low)
                    {
                        low = ilow;
                    }
                    if(ihigh > high)
                    {
                        high = ihigh;
                    }
                }
            }
            return new KeyValuePair<double, double>(high, low);
        }

        private bool checkCandle(String symbol, TIMEFRAME timeframe)
        {
            try
            {
                bool newCandle = false;
                StrategyMetaData strategyMetaData = getStrategyMetaDataMap(symbol, timeframe);
                LocalDate localDate = getMarketLocalDate(symbol);

                // new day detected
                if (!localDate.Equals(strategyMetaData.getCurrentLocalDate()))
                {
                    strategyMetaData.setCurrentLocalDate(localDate);

                    // Get todays high/low:
                    /*
                    todaysHigh = iHigh(symbol, (int)TIMEFRAME.PERIOD_D1, 0);
                    todaysLow = iLow(symbol, (int)TIMEFRAME.PERIOD_D1, 0);

                    strategyMetaData.setSignalStartDateTime(DateUtil.addDateAndTime(strategyMetaData.getCurrentLocalDate(), signalStartTime));

                    if (signalStopTime.Equals(LocalTime.Midnight))
                    {
                        // If stop time midnight, set signal stop to following day
                        strategyMetaData.setSignalStopDateTime(DateUtil.addDateAndTime(strategyMetaData.getCurrentLocalDate().PlusDays(1), signalStopTime));
                    }
                    else
                    {
                        strategyMetaData.setSignalStopDateTime(DateUtil.addDateAndTime(strategyMetaData.getCurrentLocalDate(), signalStopTime));
                    }
                    */

                    LOG.Info("New Day Detected: " + strategyMetaData.getCurrentLocalDate());
                    //LOG.Debug("Market Time: " + getMarketTime(symbol));
                    //LOG.Debug("Market DateTime: " + getMarketDateTime(symbol));
                    //LOG.Debug("Local Date: " + localDate);
                    //LOG.Debug("Signal Start: " + strategyMetaData.getSignalStartDateTime());
                    //LOG.Debug("Signal Stop: " + strategyMetaData.getSignalStopDateTime());

                    // Get yesterdays high/low:
                    //yesterdaysHigh = iHigh(symbol, (int)TIMEFRAME.PERIOD_D1, 1);
                    //yesterdaysLow = iLow(symbol, (int)TIMEFRAME.PERIOD_D1, 1);

                    onNewDate(symbol, timeframe);
                }

                // new candle detected
                DateTime newCurrentCandle = LocalDateTime.FromDateTime(iTime(symbol, (int)timeframe, 0)).ToDateTimeUnspecified();
                if (!newCurrentCandle.Equals(strategyMetaData.getCurrentCandleDateTime()))
                {
                    strategyMetaData.setCurrentCandleDateTime(newCurrentCandle);

                    // Get distance to first candle of the day
                    int i = 0;
                    DateTime epoch = new DateTime(0);
                    for (DateTime itime = epoch; itime == epoch || (itime != epoch && itime > strategyMetaData.getCurrentLocalDate().AtMidnight().ToDateTimeUnspecified()); i++)
                    {
                        itime = iTime(symbol, (int)timeframe, i);
                    }
                    strategyMetaData.setCandleDistanceToDayStart(i);

                    //LOG.Debug("New Candle Detected: " + newCurrentCandle + " : distance from day start: " + strategyMetaData.getCandleDistanceToDayStart());

                    newCandle = true;
                    onNewCandle(symbol, timeframe);
                }

                if (evalOncePerCandle && !newCandle)
                {
                    return false;
                }
                return true;

            }
            catch (Exception e)
            {
                LOG.Error(e);    
                throw;
            }
        }

        /*
        private void closeOut(String symbol)
        {
            return;
            if(closeOutTime != null)
            {
                DateTime closeOutDateTime = DateUtil.addDateAndTime(getMarketLocalDate(symbol), closeOutTime);
                int slippage = 5;

                for (int i = 0; i < this.OrdersTotal(); i++)
                {
                    if (OrderSelect(i, (int)SELECTION_TYPE.SELECT_BY_POS, (int)SELECTION_POOL.MODE_TRADES) && OrderMagicNumber() == getMagicNumber(symbol))
                    {
                        // Close Out all trades after
                        if (getMarketDateTime(symbol) > closeOutDateTime)
                        {
                            LOG.Info(String.Format("Closing out all trades: current time [{0}] > close out time [{1}]",
                                                                getMarketDateTime(symbol), closeOutDateTime));
                            closeOutThisOrder(symbol);
                        }
                    }
                }
            }
        }

        */

        public void closeOutThisOrder(String symbol)
        {
            try
            {
                int slippage = 5;
                if (OrderType() == (int)TRADE_OPERATION.OP_BUY)
                {
                    OrderClose(OrderTicket(), OrderLots(), this.MarketInfo(symbol, (int)MARKET_INFO.MODE_BID), slippage, COLOR.Red);
                }
                else if (OrderType() == (int)TRADE_OPERATION.OP_SELL)
                {
                    OrderClose(OrderTicket(), OrderLots(), this.MarketInfo(symbol, (int)MARKET_INFO.MODE_ASK), slippage, COLOR.Red);
                }
            }
            catch (Exception e)
            {
                LOG.Error(e);                
                throw;
            }
        }

        public double pipToPoint(String symbol)
        {
            int digits = (int) MarketInfo(symbol, (int) MARKET_INFO.MODE_DIGITS);
            if (digits == 3 || digits == 5)
            {
                return Math.Round(10 * MarketInfo(symbol, (int)MARKET_INFO.MODE_TICKSIZE), digits);
            }
            else
            {
                return Math.Round(MarketInfo(symbol, (int)MARKET_INFO.MODE_TICKSIZE), digits);
            }
        }

        // Method to execute the trade
        public void executeTrade(String symbol, SignalResult signal)
        {
            try
            {
                TRADE_OPERATION op;
                double price, lots;
                int slippage = 5000;
                double stoploss = this.getStopLoss(symbol, signal);
                double takeprofit = this.getTakeProfit(symbol, signal);
                String comment = this.getComment(symbol);
                int magic = this.getMagicNumber(symbol);
                DateTime expiration = this.getExpiry(symbol, signal);
                COLOR arrowColor = COLOR.Aqua;

                double stopDistance;

                DateTime lastBuyOpen, lastSellOpen;
                bool openBuyOrder = false, openSellOrder = false, openBuyStopOrder = false, openSellStopOrder = false, openBuyLimitOrder = false, openSellLimitOrder = false;

                if (signal.getSignal() == SignalResult.BUYMARKET)
                {
                    op = TRADE_OPERATION.OP_BUY;
                }
                else if (signal.getSignal() == SignalResult.SELLMARKET)
                {
                    op = TRADE_OPERATION.OP_SELL;
                }
                else if (signal.getSignal() == SignalResult.BUYSTOP)
                {
                    op = TRADE_OPERATION.OP_BUYSTOP;
                }
                else if (signal.getSignal() == SignalResult.SELLSTOP)
                {
                    op = TRADE_OPERATION.OP_SELLSTOP;
                }
                else if (signal.getSignal() == SignalResult.BUYLIMIT)
                {
                    op = TRADE_OPERATION.OP_BUYLIMIT;
                }
                else if (signal.getSignal() == SignalResult.SELLLIMIT)
                {
                    op = TRADE_OPERATION.OP_SELLLIMIT;
                }
                else
                {
                    throw new Exception("Invalid Signal signal=" + signal);
                }

                //LOG.Debug("stopDistance: " + stopDistance);
                //LOG.Debug("price: " + price);
                //LOG.Debug("stoploss: " + stoploss);
                //LOG.Debug("takeprofit: " + takeprofit);


                // Check open trades on this symbol
                for (int i = 0; i < OrdersTotal(); i++)
                {
                    OrderSelect(i, (int)SELECTION_TYPE.SELECT_BY_POS, (int)SELECTION_POOL.MODE_TRADES);
                    if (OrderType() == (int)TRADE_OPERATION.OP_BUY && OrderSymbol().Equals(symbol) && OrderMagicNumber() == magic)
                    {
                        lastBuyOpen = OrderOpenTime();
                        openBuyOrder = true;
                        if (closeOnOpposingSignal && signal.getSignal() < 0)
                        {
                            closeOutThisOrder(symbol);
                        }
                    }
                    else if (OrderType() == (int)TRADE_OPERATION.OP_SELL && OrderSymbol().Equals(symbol) && OrderMagicNumber() == magic)
                    {
                        lastSellOpen = OrderOpenTime();
                        openSellOrder = true;
                        if (closeOnOpposingSignal && signal.getSignal() > 0)
                        {
                            closeOutThisOrder(symbol);
                        }

                    }
                    else if (OrderType() == (int)TRADE_OPERATION.OP_BUYSTOP && OrderSymbol().Equals(symbol) && OrderMagicNumber() == magic)
                    {
                        openBuyStopOrder = true;
                    }
                    else if (OrderType() == (int)TRADE_OPERATION.OP_SELLSTOP && OrderSymbol().Equals(symbol) && OrderMagicNumber() == magic)
                    {
                        openSellStopOrder = true;
                    }
                    else if (OrderType() == (int)TRADE_OPERATION.OP_BUYLIMIT && OrderSymbol().Equals(symbol) && OrderMagicNumber() == magic)
                    {
                        openBuyLimitOrder = true;
                    }
                    else if (OrderType() == (int)TRADE_OPERATION.OP_SELLLIMIT && OrderSymbol().Equals(symbol) && OrderMagicNumber() == magic)
                    {
                        openSellLimitOrder = true;
                    }
                }

                // Calculate lots
                double entryPrice = this.getEntryPrice(symbol, signal);

                if (signal.getSignal() > 0)
                {
                    stopDistance = entryPrice - stoploss;
                }
                else
                {
                    stopDistance = stoploss - entryPrice;
                }
                lots = this.getLotSize(symbol, stopDistance);


                if ((signal.getSignal() == SignalResult.BUYMARKET && !openBuyOrder) 
                        || (signal.getSignal() == SignalResult.SELLMARKET && !openSellOrder)
                        || (signal.getSignal() == SignalResult.BUYLIMIT && !openBuyLimitOrder && !openBuyOrder)
                        || (signal.getSignal() == SignalResult.SELLLIMIT && !openSellLimitOrder && !openSellOrder)
                        || (signal.getSignal() == SignalResult.BUYSTOP && !openBuyStopOrder && !openBuyOrder) 
                        || (signal.getSignal() == SignalResult.SELLSTOP && !openSellStopOrder && !openSellOrder))
                {
                    LOG.Info(String.Format("Executing Trade at " + DateUtil.FromUnixTime((long)MarketInfo(symbol, (int)MARKET_INFO.MODE_TIME)) +
                                            "\n\tsymbol:\t{0}" +
                                            "\n\top:\t\t{1}" +
                                            "\n\tlots:\t\t{2}" +
                                            "\n\tentryPrice:\t{3}" +
                                            "\n\tslippage:\t{4}" +
                                            "\n\tstoploss:\t{5}" +
                                            "\n\ttakeprofit:\t{6}" +
                                            "\n\tcomment:\t{7}" +
                                            "\n\tmagic:\t\t{8}" +
                                            "\n\texpiration:\t{9}" +
                                            "\n\tarrowColor:\t{0}", symbol, (int) op, lots, entryPrice, slippage, stoploss, takeprofit, comment, magic, expiration, arrowColor));

                    OrderSend(symbol, (int)op, lots, entryPrice, slippage, stoploss, takeprofit, comment, magic, expiration, arrowColor);
                }

            }
            catch (Exception e)
            {
                LOG.Error(e);
                throw;
            }
        }

        public override void OnTimer()
        {

        }

        public override void OnInit()
        {
            LOG.Debug("OnInit() called");
            try
            {
                init();
            }
            catch (Exception e)
            {
                LOG.Error(e);
                throw;
            }
        }

        public override void OnDeinit()
        {
            LOG.Debug("OnDeinit() called");
            try
            {
                destroy();
            }
            catch (Exception e)
            {
                LOG.Error(e);
                throw;
            }
        }

        public abstract void init();

        public abstract void destroy();

        // Abstract method to evaluate the current tick and check whether or not a signal exists
        public abstract SignalResult evaluate(String symbol);

        public abstract double getStopLoss(String symbol, SignalResult signal);

        public abstract double getTakeProfit(String symbol, SignalResult signal);

        public abstract double getEntryPrice(String symbol, SignalResult signal);

        public abstract DateTime getExpiry(String symbol, SignalResult signal);

        public abstract double getLotSize(String symbol, double stopDistance);

        public abstract int getMagicNumber(String symbol);

        public abstract String getComment(String symbol);

        public abstract bool isAsleep(String symbol);

        public abstract bool filter(String symbol);

        public abstract void onNewDate(String symbol, TIMEFRAME timeframe);

        public abstract void onNewCandle(String symbol, TIMEFRAME timeframe);

        // Non Abstract methods
        public double getStopEntry(String symbol, SignalResult signal)
        {
            return 0;
        }

        // Method to manage the trade
        public abstract void manageOpenTrades(String symbol, int ticket);

    }
}