using System;
using Plantech.Bim.Custom.Common;
using Tekla.Structures.Drawing;

namespace Plantech.Bim.Custom.Plugins;

internal abstract class CustomBase
{
    public static readonly Lazy<DrawingHandler> _lazyDrawingHandler =
        new(() => new DrawingHandler());

    protected static Tekla.Structures.Model.Model _modelInstance => LazyModelConnector.ModelInstance;
    protected static DrawingHandler _drawingHandler => _lazyDrawingHandler.Value;
}
