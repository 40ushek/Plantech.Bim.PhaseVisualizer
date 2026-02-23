using System;
using Tekla.Structures.Model;

namespace Plantech.Bim.PhaseVisualizer.Common
{
    public static class LazyModelConnector
    {
        private static readonly Lazy<Model> _lazyModel = new(() => new());
        public static Model ModelInstance => _lazyModel.Value;

        private static readonly Lazy<Tekla.Structures.Model.UI.ModelObjectSelector> _lazySelector =
            new(() => new Tekla.Structures.Model.UI.ModelObjectSelector());
        public static Tekla.Structures.Model.UI.ModelObjectSelector SelectorInstance => _lazySelector.Value;
    }
}
