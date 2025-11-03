using System;

namespace Kiosk.Utils
{
    public class Define
    {
        public static string FIREBASE_API_KEY => App.Configuration["Firebase:FIREBASE_API_KEY"];
        public static string FIREBASE_AUTH_DOMAIN => App.Configuration["Firebase:FIREBASE_AUTH_DOMAIN"];
        public static string FIREBASE_DATABASE_URL => App.Configuration["Firebase:FIREBASE_DATABASE_URL"];
        public static string AUTH_EMAIL => App.Configuration["Firebase:AUTH_EMAIL"];
        public static string AUTH_PW => App.Configuration["Firebase:AUTH_PW"];
        public static string RTDB_IV => App.Configuration["Firebase:RTDB_IV"];
        public static string RTDB_SECRET_KEY => App.Configuration["Firebase:RTDB_SECRET_KEY"] ?? string.Empty;
        public static string authKey => App.Configuration["Keys:authKey"];
        public static string PUBLIC_KEY => App.Configuration["Keys:PublicKey"];
        public static string SERVER_API => App.Configuration["SERVER_API"];
        public static string APP_VER => App.Configuration["AppVer"] ?? "1";
        public static string UUID => "";
    }
}
