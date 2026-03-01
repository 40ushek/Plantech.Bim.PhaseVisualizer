using System;
using Tekla.Structures.Dialog;
using System.Reflection;


namespace Plantech.Bim.PhaseVisualizer.Plugins;

internal static class PluginWindowBaseExtensions
{
    public static void DisableDefaultStyle(this WindowBase windowBase)
    {
        Type typeFromHandle = typeof(WindowBase);
        if (typeFromHandle != null)
        {
            FieldInfo field = typeFromHandle.GetField("useDefaultStyle", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(windowBase, false);
            }
        }
    }
    public static void EnableDefaultStyle(this WindowBase windowBase)
    {
        Type typeFromHandle = typeof(WindowBase);
        if (typeFromHandle != null)
        {
            FieldInfo field = typeFromHandle.GetField("useDefaultStyle", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(windowBase, true);
            }
        }
    }
}
