/*
 * Copyright Lamont Granquist, Sebastien Gaggini and the MechJeb contributors
 * SPDX-License-Identifier: LicenseRef-PD-hp OR Unlicense OR CC0-1.0 OR 0BSD OR MIT-0 OR MIT OR LGPL-2.1+
 */

/*
 * ---- PORTED VERBATIM into DragonScreen from MechJebLib/Utils/DictOfLists.cs ----
 * Per docs/MECHJEBLIB_PORT.md. A dictionary whose values are lists, auto-created on first access -
 * SimVessel keeps its per-stage part/engine/RCS lists in these. Copied as-is.
 */
using System.Collections.Generic;

namespace MechJebLib.Utils
{
    public class DictOfLists<TKey, TValue>
    {
        private readonly Dictionary<TKey, List<TValue>> _dict;

        public DictOfLists(int capacity)
        {
            _dict = new Dictionary<TKey, List<TValue>>(capacity);
        }

        public List<TValue> this[TKey key]
        {
            get
            {
                if (_dict.TryGetValue(key, out List<TValue> val))
                    return val;

                _dict.Add(key, val = new List<TValue>());
                return val;
            }
        }

        public void Clear()
        {
            // careful:  not every value in this dict is always valid and we don't clear them out here.
            // not implementing IDictionary and the ability to iterate over Keys is deliberate.
            foreach (List<TValue> list in _dict.Values)
                list.Clear();
        }

        public int Count(TKey key) => _dict[key].Count;
    }
}
