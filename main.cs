using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using cAlgo.API;
using cAlgo.API.Collections;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;


using System.Globalization;


namespace cAlgo
{

    /// <summary>
    /// <para><b>cTrader Guru | Extensios</b></para>
    /// <para>A group of generic extensions that make the developer's life easier</para>
    /// </summary>
    public static class Extensions
    {
    
        #region DateTime

        /// <param name="Culture">Localization of double value</param>
        /// <returns>double : Time representation in double format (example : 10:34:07 = 10,34)</returns>
        public static double ToDouble(this DateTime thisDateTime, string Culture = "en-EN")
        {

            string nowHour = (thisDateTime.Hour < 10) ? string.Format("0{0}", thisDateTime.Hour) : string.Format("{0}", thisDateTime.Hour);
            string nowMinute = (thisDateTime.Minute < 10) ? string.Format("0{0}", thisDateTime.Minute) : string.Format("{0}", thisDateTime.Minute);

            return string.Format("{0}.{1}", nowHour, nowMinute).ToDouble(Culture);

        }

        #endregion
    
    
        #region String

        /// <param name="Culture">Localization of double value</param>
        /// <returns>double : Time representation in double format (example : "10:34:07" = 10,34)</returns>
        public static double ToDouble(this string thisString, string Culture = "en-EN")
        {
            var culture = CultureInfo.GetCultureInfo(Culture);
            return double.Parse(thisString.Replace(',', '.').ToString(CultureInfo.InvariantCulture), culture);

        }

        #endregion





        #region Positions
        
        /// <param name="label">Settings label value (filters open positions made only by this bot)</param>
        /// <returns>bool : if there is an open position from this bot returns <b>true</b></returns>
        public static bool Count(this Positions thisPos, string label) {
            var pos = thisPos.Find(label);
            if(pos != null) {
                return true;
            }
            return false;
        }

        #endregion

        #region Symbol
        
        /// <param name="AccountBalance">Account.balance value</param>
        /// <param name="RiskPercentage">% of capital to risk per trade</param>
        /// <param name="_StopLoss">SL difference from Stoploss & Entry</param>
        /// <returns>double : Lot size for Forex/Stocks</returns>
        public static double CalculateLotSize(this Symbol thisSymbol, double AccountBalance, double RiskPercentage, double _StopLoss) {
            var amount_to_risk_per_trade = AccountBalance * (RiskPercentage / 100);
            var  PipScale = thisSymbol.PipValue;
            var trade_volume   = amount_to_risk_per_trade / (_StopLoss * PipScale);
            var truncation_factor   = thisSymbol.LotSize * PipScale * 100;
            var trade_volume_truncated = ( (int) (trade_volume / truncation_factor)) * truncation_factor;
            
            return thisSymbol.TickSize == 0.01 ? thisSymbol.NormalizeVolumeInUnits(trade_volume) : thisSymbol.NormalizeVolumeInUnits(trade_volume_truncated); 
        }
        
        /// <param name="tradeSize">SL difference from Stoploss,Entry Or Atr value or Any simular</param>
        /// <param name="StopLossMultiplier">Value to multiply the stoploss with default in settings = 1</param>
        /// <returns>double : stoploss size</returns>
        public static double CalculateStopLoss(this Symbol thisSymbol, double tradeSize, double StopLossMultiplier) {
            return (tradeSize * (thisSymbol.TickSize / thisSymbol.PipSize * Math.Pow(10, thisSymbol.Digits))) * StopLossMultiplier;
        }
        
        /// <param name="tradeSize">SL difference from Stoploss,Entry Or Atr value or Any simular</param>
        /// <param name="StopLossMultiplier">Value to multiply the stoploss with default in settings = 1<</param>
        /// <param name="TakeProfit">TP 2 = 2 tp 1 sl</param>
        /// <returns>double : takeprofit size</returns>
        public static double CalcTakeProfit(this Symbol thisSymbol, double tradeSize, double StopLossMultiplier, double TakeProfit) {
            var atrInPips = tradeSize * (thisSymbol.TickSize / thisSymbol.PipSize * Math.Pow(10, thisSymbol.Digits));
            return (atrInPips * StopLossMultiplier) * TakeProfit;
        }

        #endregion

        #region Other
        public static double TimeFrameMinute(this TimeFrame tf, Action Stop, Action<string> Print)
        {
            if(!tf.Name.StartsWith("Minute"))
            {   
                Print("ERROR HAVE TO USE NORMAL MINUTE TIMEFRAMES");
                Stop();
            }
            
            string timeframe = tf.Name.Remove(0, 6);
            return Convert.ToDouble(timeframe) / 100;
        }

        #endregion

    }
    
}



public struct Strategy
{
    public bool canTrade = false;
    public List<double> barsInRange = new List<double>();
    public double longEntry = 0.0;
    public double shortEntry = 0.0;
    
    public bool StateCanTrade = true;
    public bool StateGetBarRange = false;
    public bool StateWaitForEntry = false;
    public bool StateClosePositions = false;
    
    
    
    public void CanTrade(double serverTime, double startTime, double endTime)
    {
        if(!this.StateCanTrade) 
        {
            return;
        }
        if(serverTime >= startTime && serverTime <= endTime)
        {
            this.StateCanTrade = false;
            this.StateGetBarRange = true;
            return;
        }
        
        return;
    }
    
    public void GetBarRange(Bars bars, Action<string, double, Color> HorizantalLine, double timeframeMinutes)
    {
        // if the bot is still waiting for entry time
        if(!this.StateGetBarRange)
        {
            return;
        }
        
        this.barsInRange.Clear();
        this.longEntry = 0.0;
        this.shortEntry = 0.0;
        
        // 2.7 is 4.5 hours if 1 = 0.6 like an hour
        double hoursback = 2.7;
        int i = bars.Count - 1;
        while(hoursback > 0)
        {
            barsInRange.Add(bars.HighPrices[i]);
            barsInRange.Add(bars.LowPrices[i]);
        
            i -= 1;
            hoursback -= timeframeMinutes;
        }
        
        this.longEntry = barsInRange.Max();
        this.shortEntry = barsInRange.Min();

        HorizantalLine("L", this.longEntry, Color.Pink);
        HorizantalLine("S", this.shortEntry, Color.Pink);

        this.StateGetBarRange = false;
        this.StateWaitForEntry = true;
    }
    
    public void ClosePositions(double serverTime, PendingOrders orders, Positions positions, string label, Action<string> Print, double closeTrades)
    {
        if(!StateClosePositions)
        {
            return;
        }
        Print("Time: " + serverTime);
        
        if(serverTime == 23.30)
        {
            foreach(PendingOrder order in orders) 
            {
                if(order.Label == label)
                {
                    order.Cancel();
                }
            }
        }
        else if(serverTime == closeTrades)
        {
            for (var i = 0; i < positions.Count; i++)
            {
                var pos = positions.Find(label);
                if(pos != null) {
                    pos.Close();
                }
            }
            this.StateClosePositions = false;
            this.StateCanTrade = true;
            
        }
    }

            




    public static Strategy NewStrategy()
    {
        return new Strategy();
    }
}


namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.EasternStandardTime, AccessRights = AccessRights.None)]
    public class NewBotTemplateStocksForexv11 : Robot
    {
        #region Settings

        #region Trade Settings
        [Parameter("Position Label", DefaultValue = "BOT", Group = "Trade Settings")]
        public string PosLabel { get; set; }


        #endregion

        #region Risk Settings
        [Parameter("Lots", DefaultValue = 0.5, MinValue = 0.1, Step = 0.1, Group = "Risk Settings")]
        public double Risk_Lots { get; set; }
        
        [Parameter("Stoploss", DefaultValue = 150, MinValue = 10, Step = 1, Group = "Risk Settings")]
        public double Risk_StopLoss { get; set; }

        #endregion
        
        #region Strategy Settings
        [Parameter("Can trade from (21.30 = 21:30)", DefaultValue = 21.30, MinValue = 1, Step = 0.1, Group = "Strategy")]
        public double StartTradeWait { get; set; }
        
        [Parameter("Can trade to (23.30 = 23:30)", DefaultValue = 23.30, MinValue = 10, Step = 1, Group = "Strategy")]
        public double EndTradeWait { get; set; }
        
        [Parameter("Close trades at (16.50 = 16:50)", DefaultValue = 16.50, MaxValue = 16.55, MinValue = 0, Step = 1, Group = "Strategy")]
        public double CloseTrades { get; set; }

        #endregion


        #endregion

        #region Built in Functions
        protected override void OnStart()
        {
        
            Positions.Opened += CloseOrders;
            
            timeFrameMinute = TimeFrame.TimeFrameMinute(Stop, PrintStr);
            Print("TFF: " + timeFrameMinute);
        }

        protected override void OnTick()
        {
            _STRATEGY.ClosePositions(Server.Time.ToDouble(), PendingOrders, Positions, PosLabel, PrintStr, CloseTrades);  
        }
        
        protected override void OnBar() 
        {
            _STRATEGY.CanTrade(Server.Time.ToDouble(), StartTradeWait, EndTradeWait);
            _STRATEGY.GetBarRange(Bars, DrawHorizantalLine, timeFrameMinute);
            if(_STRATEGY.StateWaitForEntry)
            {
                ExecuteTrade(TradeType.Sell, _STRATEGY.shortEntry);
                ExecuteTrade(TradeType.Buy, _STRATEGY.longEntry);
                _STRATEGY.StateWaitForEntry = false;
                _STRATEGY.StateClosePositions = true;
            }
        }
        #endregion

        #region Variables
        public double timeFrameMinute;
        public Strategy _STRATEGY = Strategy.NewStrategy();
        #endregion


        #region Misc functions
        public void PrintStr(string text)
        {
            Print(text);
        }
        
        private int RandomNum()
        {
            return new Random().Next(0, 1000000);
        }
        
        public void DrawHorizantalLine(string id, double position, Color col)
        {
            Chart.DrawHorizontalLine(id, position, col);
            
        }

        
        public void CloseOrders(PositionOpenedEventArgs pos)
        {
            var allOrders = PendingOrders.ToArray();
            foreach(var order in allOrders)
            {
                if(order.Label == PosLabel)
                {
                    order.Cancel();
                }
            }
        }
        
        
        #endregion



        #region Trade 
        public void ExecuteTrade(TradeType tradetype, double entryPrice) {
            PlaceStopLimitOrder(tradetype, Symbol.Name, Symbol.NormalizeVolumeInUnits(Risk_Lots * 100000), entryPrice, 20, PosLabel, Risk_StopLoss, Risk_StopLoss * 10);
        }


        #endregion



    }
}