using System;

namespace StoneMaster.Corel.Corel
{
    internal static class LayerService
    {
        public static global::Corel.Interop.VGCore.Layer GetOrCreateLayer(
            global::Corel.Interop.VGCore.Layers layers,
            global::Corel.Interop.VGCore.Page page,
            string name,
            bool clearExisting)
        {
            global::Corel.Interop.VGCore.Layer layer = null;
            for (int i = 1; i <= layers.Count; i++)
            {
                global::Corel.Interop.VGCore.Layer candidate = layers[i];
                if (string.Equals((string)candidate.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    layer = candidate;
                    break;
                }
            }

            if (layer == null)
                layer = page.CreateLayer(name);

            if (clearExisting)
            {
                try
                {
                    while (layer.Shapes.Count > 0)
                        layer.Shapes[1].Delete();
                }
                catch { }
            }

            return layer;
        }
    }
}