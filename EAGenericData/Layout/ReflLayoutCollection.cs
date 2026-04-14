using System.Collections.Generic;

namespace EAGenericData.Layout
{
    public class ReflLayoutCollection : SortedList<ReflLayoutHash, ReflLayout>
    {
        public void TryAddLayout(ReflLayout layout)
        {
            if (!ContainsKey(layout.LayoutHash))
            {
                Add(layout.LayoutHash, layout);
            }
        }
    }
}