void CloseOpenOrder(string sp)
{ 
   int slipPage=StrToInteger(sp);
   int total = OrdersTotal();
   
  for(int i=total-1;i>=0;i--)
  {
    OrderSelect(i, SELECT_BY_POS);
    int type   = OrderType();

    bool result = false;
    
    if(type==OP_BUY){
      result = OrderClose( OrderTicket(), OrderLots(), MarketInfo(OrderSymbol(), MODE_BID), slipPage, Red );
    }
    else if(type==OP_SELL){
      result = OrderClose( OrderTicket(), OrderLots(), MarketInfo(OrderSymbol(), MODE_ASK), slipPage, Red );
    }else{
       result = OrderDelete(OrderTicket());
    }
    
    if(result == false)
    {
      Alert("Order " , OrderTicket() , " failed to close. Error:" , GetLastError() );
      Sleep(3000);
    }  
  }
}