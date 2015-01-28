using System;
using System.ComponentModel;

namespace MCantale.Helpers
{
   #region class ShortNameAttribute

   /// <summary>
   /// This is a custom attribute called "ShortName". We can add this to 
   /// enumerated lists and then query it later...
   /// 
   /// enum Wibble
   /// {
   ///    [ShortName("A")] MyValue0,
   ///    [ShortName("B")] MyValue1,
   ///    ...
   /// }
   /// 
   /// Console.Write(Wibble.MyValue0.ShortName()); // output is "A"
   /// </summary>
   /// 
   public class ShortNameAttribute : Attribute
   {
      private string _ShortName;
      public string ShortName
      {
         get { return _ShortName; }
         set { _ShortName = value; }
      }

      public ShortNameAttribute(string value)
      {
         this.ShortName = value;
      }
   }

   #endregion

   #region class CustomAttributes

   public static class CustomAttributes
   {
      public static string GetCustomDescription(object objEnum)
      {
         var fi = objEnum.GetType().GetField(objEnum.ToString());
         var attributes = (DescriptionAttribute[])fi.GetCustomAttributes(typeof(DescriptionAttribute), false);
         return (attributes.Length > 0) ? attributes[0].Description : objEnum.ToString();
      }

      public static string Description(this Enum value)
      {
         return GetCustomDescription(value);
      }

      public static string GetCustomShortName(object objEnum)
      {
         var fi = objEnum.GetType().GetField(objEnum.ToString());
         var attributes = (ShortNameAttribute[])fi.GetCustomAttributes(typeof(ShortNameAttribute), false);
         return (attributes.Length > 0) ? attributes[0].ShortName : objEnum.ToString();
      }

      public static string ShortName(this Enum value)
      {
         return GetCustomShortName(value);
      }
   }

   #endregion

   #region class InvalidStateException

   public class InvalidStateException : Exception
   {
      public override string Message
      {
         get
         {
            return "Connection was in an invalid state for the operation!";
         }
      }
   }

   #endregion
}
