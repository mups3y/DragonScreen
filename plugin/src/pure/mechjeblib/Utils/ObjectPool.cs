/*
 * Copyright Lamont Granquist, Sebastien Gaggini and the MechJeb contributors
 * SPDX-License-Identifier: LicenseRef-PD-hp OR Unlicense OR CC0-1.0 OR 0BSD OR MIT-0 OR MIT OR LGPL-2.1+
 */

/*
 * ---- PORTED VERBATIM into DragonScreen from MechJebLib/Utils/ObjectPool.cs ----
 * Per docs/MECHJEBLIB_PORT.md: taken as-is (59 lines) so H1's `_pool` works exactly as MechJeb's.
 * KSP is single-threaded, so `UseGlobal` stays true (the global ConcurrentBag path); the thread-local
 * branch is inert here but kept so the file is byte-faithful to the source and the PSG port can reuse
 * it unchanged. No hand edits - this is flight-adjacent numerical machinery and hand edits are where
 * this project's regressions come from.
 */
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace MechJebLib.Utils
{
    public class ObjectPoolBase
    {
        // Tests flip this to false (via [assembly: TestFramework]) so each thread
        // gets its own pool — keeps allocation-counting tests from contaminating
        // each other when xunit runs them in parallel.
        internal static bool UseGlobal = true;
    }

    public class ObjectPool<T> : ObjectPoolBase where T : class
    {
        private readonly Func<T> _create;
        private readonly Action<T> _reset;

        private readonly ConcurrentBag<T> _globalPool = new ConcurrentBag<T>();
        private readonly ThreadLocal<Stack<T>> _localPool = new ThreadLocal<Stack<T>>(() => new Stack<T>());

        public ObjectPool(Func<T> create, Action<T> reset)
        {
            _create = create;
            _reset = reset;
        }

        public T Borrow()
        {
            if (UseGlobal)
            {
                if (_globalPool.TryTake(out T item)) return item;
            }
            else
            {
                Stack<T> stack = _localPool.Value;
                if (stack.Count > 0) return stack.Pop();
            }

            return _create();
        }

        public void Release(T item)
        {
            _reset(item);
            if (UseGlobal)
                _globalPool.Add(item);
            else
                _localPool.Value.Push(item);
        }
    }
}
