string CloseOpenOrder(string opendingOrderList,string pendingOrderList)
{
   //StringSeparator
   string opendingList[];
   string pendingList[];
   
   if(opendingOrderList!=""){
      StringSplit(opendingOrderList,StringSeparator,opendingList);
   }
   if(pendingOrderList!=""){
      StringSplit(pendingOrderList,StringSeparator,pendingList);
   }
   
   int oArraySize=ArraySize(opendingList);
   if(oArraySize>0)
   {
      //关闭打开的订单
      
   }
   
   int pArraySize=ArraySize(pendingList);
   
   if(pArraySize>0){
      //关闭挂起的订单
   }
   
   return oArraySize+"IIIMMM"+pArraySize;
}