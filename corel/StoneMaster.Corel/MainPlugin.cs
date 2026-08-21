using System;
using StoneMaster.Corel.Services;

namespace StoneMaster.Corel
{
    public sealed class MainPlugin
    {
        internal static readonly Corel.CorelDrawService Corel = new Corel.CorelDrawService();
        internal static readonly PythonEngineService Engine = new PythonEngineService();
        internal static readonly SettingsService Settings = new SettingsService();

        public static void Attach(object application)
        {
            Corel.Attach(application);
        }
    }
}