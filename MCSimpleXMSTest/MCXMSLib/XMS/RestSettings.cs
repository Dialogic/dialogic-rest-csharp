
namespace MCantale.XMS
{
   #region class RestSettings

   /// <summary>
   /// RestSettings implements the "singleton" pattern. This means that 
   /// there can only ever be ONE object. It cannot be instantiated directly
   /// and you must use the public static member "Instance" for access like
   /// this:
   /// 
   /// RestSettings.Instance.AppID = "...";
   /// 
   /// Attempting to instantiate the class will result in a compilation error.
   /// </summary>
   /// 
   public sealed class RestSettings
   {
      #region Fields

      public static RestSettings Instance
      {
         get { return Nested.instance; }
      }

      public string ServerIP   { get; set; }
      public int    ServerPort { get; set; }
      public string AppID      { get; set; }

      #region class Nested

      class Nested
      {
         static Nested()
         {
         }

         internal static readonly RestSettings instance = new RestSettings();
      }

      #endregion

      #endregion

      RestSettings()
      {
         this.ServerIP   = "";
         this.ServerPort = 81;
         this.AppID      = "app";
      }
   }

   #endregion
}
