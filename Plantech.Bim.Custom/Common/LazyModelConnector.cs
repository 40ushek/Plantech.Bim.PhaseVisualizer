using System;
using Tekla.Structures.Model;

namespace Plantech.Bim.Custom.Common;

internal static class LazyModelConnector
{
    private static readonly Lazy<Model> LazyModel = new(() => new Model());

    public static Model ModelInstance => LazyModel.Value;
}
