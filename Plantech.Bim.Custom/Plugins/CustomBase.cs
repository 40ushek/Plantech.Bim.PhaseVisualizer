using System;
using Tekla.Structures.Drawing;
using Tekla.Structures.Model;

namespace Plantech.Bim.Custom.Plugins;

internal abstract class CustomBase
{
    public static readonly Lazy<DrawingHandler> _lazyDrawingHandler =
        new(() => new DrawingHandler());
    private static readonly Lazy<Model> _lazyModel =
        new(() => new Model());
    protected static Model _modelInstance => _lazyModel.Value;
    protected static DrawingHandler _drawingHandler => _lazyDrawingHandler.Value;
}
