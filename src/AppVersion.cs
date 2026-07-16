using System;
using System.Reflection;

namespace Sprocket
{
    /// <summary>Single source of truth for the running version — reads AssemblyInfo.cs's
    /// AssemblyVersion so the footer label and the update checker never drift out of sync
    /// with each other or with a hand-typed string.</summary>
    internal static class AppVersion
    {
        public static readonly Version Current = Assembly.GetExecutingAssembly().GetName().Version;

        /// <summary>"3.0.0" — major.minor.build; the unused 4th (revision) field is dropped.</summary>
        public static readonly string Display =
            Current.Major + "." + Current.Minor + "." + Current.Build;
    }
}
