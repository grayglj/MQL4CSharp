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

using log4net.Appender;
using MQL4CSharp.Base;
using MQL4CSharp.Base.Common;
using MQL4CSharp.Base.Enums;

namespace MQL4CSharp.UserDefined.Signal
{
    public class MACross : BaseSignal
    {
        private int maPeriodFast = 13;
        private int maPeriodSlow = 48;
        private MA_METHOD methodFast = MA_METHOD.MODE_EMA;
        private MA_METHOD methodSlow = MA_METHOD.MODE_EMA;
        private int maShift = 0;

        public MACross(BaseStrategy strategy) : base(strategy)
        {

        }

        public MACross(BaseStrategy strategy, int maPeriodFast, int maPeriodSlow, MA_METHOD methodFast, MA_METHOD methodSlow, int maShift) : this(strategy)
        {
            this.maPeriodFast = maPeriodFast;
            this.maPeriodSlow = maPeriodSlow;
            this.methodFast = methodFast;
            this.methodSlow = methodSlow;
            this.maShift = maShift;
        }

        public override SignalResult evaluate(string symbol, TIMEFRAME timeframe)
        {
            double maFast1 = strategy.iMA(symbol, (int)timeframe, maPeriodFast, maShift, (int)methodFast, (int)APPLIED_PRICE.PRICE_CLOSE, 1);
            double maFast2 = strategy.iMA(symbol, (int)timeframe, maPeriodFast, maShift, (int)methodFast, (int)APPLIED_PRICE.PRICE_CLOSE, 2);
            double maSlow1 = strategy.iMA(symbol, (int)timeframe, maPeriodSlow, maShift, (int)methodSlow, (int)APPLIED_PRICE.PRICE_CLOSE, 1);
            double maSlow2 = strategy.iMA(symbol, (int)timeframe, maPeriodSlow, maShift, (int)methodSlow, (int)APPLIED_PRICE.PRICE_CLOSE, 2);

            if (maFast1 < maSlow1 && maFast2 > maSlow2)
            {
                //strategy.LOG.Info("Signal Short: " + strategy.iTime(symbol, (int)timeframe, 0));
                return SignalResult.newSELLMARKET();
            }
            else if (maFast1 > maSlow1 && maFast2 < maSlow2)
            {
                //strategy.LOG.Info("Signal Long: " + strategy.iTime(symbol, (int)timeframe, 0));
                return SignalResult.newBUYMARKET();
            }
            else
            {
                //strategy.LOG.Info("Signal Neutral: " + strategy.iTime(symbol, (int)timeframe, 0));
                return SignalResult.newNEUTRAL();
            }
        }
    }
}
