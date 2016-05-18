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

using System;
using System.Runtime.CompilerServices;
using MQL4CSharp.Base.Enums;

namespace MQL4CSharp.Base.Common
{
    public class SignalResult
    {
        public static int SELLLIMIT = -3;
        public static int SELLSTOP = -2;
        public static int SELLMARKET = -1;
        public static int NEUTRAL = 0;
        public static int BUYMARKET = 1;
        public static int BUYSTOP = 2;
        public static int BUYLIMIT = 3;

        public static int signalToTradeOp(int signal)
        {
            if (signal == SELLMARKET)
                return (int)TRADE_OPERATION.OP_SELL;
            else if (signal == BUYMARKET)
                return (int)TRADE_OPERATION.OP_BUY;
            else if (signal == SELLSTOP)
                return (int)TRADE_OPERATION.OP_SELLSTOP;
            else if (signal == BUYSTOP)
                return (int)TRADE_OPERATION.OP_BUYSTOP;
            else if (signal == SELLLIMIT)
                return (int)TRADE_OPERATION.OP_SELLLIMIT;
            else if (signal == BUYLIMIT)
                return (int)TRADE_OPERATION.OP_BUYLIMIT;
            return -1;
        }

        private int signal;

        private SignalInfo signalInfo;

        public SignalResult(int signal, SignalInfo signalInfo)
        {
            this.signal = signal;
            this.signalInfo = signalInfo;
        }

        public bool isNotNeutral()
        {
            return signal != NEUTRAL;
        }

        public SignalResult(int signal) : this (signal, new SignalInfo())
        {
        }

        public SignalResult() : this(0)
        {
        }

        public int getSignal()
        {
            return signal;
        }

        public void setSignal(int signal)
        {
            this.signal = signal;
        }

        public SignalInfo getSignalInfo()
        {
            return signalInfo;
        }

        public void setSignalInfo(SignalInfo signalInfo)
        {
            this.signalInfo = signalInfo;
        }


        public static SignalResult newSELLLIMIT()
        {
            return new SignalResult(SELLLIMIT);
        }
        public static SignalResult newSELLSTOP()
        {
            return new SignalResult(SELLSTOP);
        }
        public static SignalResult newSELLMARKET()
        {
            return new SignalResult(SELLMARKET);
        }
        public static SignalResult newNEUTRAL()
        {
            return new SignalResult(NEUTRAL);
        }
        public static SignalResult newBUYMARKET()
        {
            return new SignalResult(BUYMARKET);
        }
        public static SignalResult newBUYSTOP()
        {
            return new SignalResult(BUYSTOP);
        }
        public static SignalResult newBUYLIMIT()
        {
            return new SignalResult(BUYLIMIT);
        }


        public override string ToString()
        {
            return String.Format("signal={0}, signalInfo={1}", signal, signalInfo.ToString());
        }

    }
}
