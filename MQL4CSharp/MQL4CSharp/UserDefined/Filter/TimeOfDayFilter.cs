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

using MQL4CSharp.Base;
using MQL4CSharp.Base.Common;
using System;
using mqlsharp.Util;
using MQL4CSharp.Base.Enums;
using NodaTime;

namespace MQL4CSharp.UserDefined.Filter
{
    public class TimeOfDayFilter : BaseFilter
    {
        private LocalTime timeStart;
        private LocalTime timeStop;
        private BaseStrategy strategy;

        /**
         *
         * @param strategy
         * @param timeStart : eg "07:30"
         * @param timeStop : eg "14:00"
         */

        public TimeOfDayFilter(BaseStrategy strategy, LocalTime timeStart, LocalTime timeStop) : base(strategy)
        {
            this.strategy = strategy;
            this.timeStart = timeStart;
            this.timeStop = timeStop;
        }

        public override bool filter(String symbol, TIMEFRAME timeframe)
        {
            DateTime currentMarketTime = DateUtil.FromUnixTime((long) strategy.MarketInfo(symbol, (int) MARKET_INFO.MODE_TIME));
            DateTime startTrading = DateUtil.getDateFromCurrentAnd24HRTime(currentMarketTime, timeStart);
            DateTime stopTrading = DateUtil.getDateFromCurrentAnd24HRTime(currentMarketTime, timeStop);

            // Trade Window
            if (currentMarketTime >= startTrading && currentMarketTime <= stopTrading)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
