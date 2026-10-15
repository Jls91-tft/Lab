using System;

namespace CepaCero.AI
{
    /// <summary>Binary min-heap of (priority, value) ints. Reused between builds: no allocations once warmed up.</summary>
    internal sealed class MinHeap
    {
        private int[] _priorities;
        private int[] _values;

        public MinHeap(int capacity)
        {
            _priorities = new int[Math.Max(16, capacity)];
            _values = new int[_priorities.Length];
        }

        public int Count { get; private set; }

        public void Clear() => Count = 0;

        public void Push(int priority, int value)
        {
            if (Count == _priorities.Length)
            {
                Array.Resize(ref _priorities, Count * 2);
                Array.Resize(ref _values, Count * 2);
            }
            int i = Count++;
            while (i > 0)
            {
                int parent = (i - 1) >> 1;
                if (_priorities[parent] <= priority)
                {
                    break;
                }
                _priorities[i] = _priorities[parent];
                _values[i] = _values[parent];
                i = parent;
            }
            _priorities[i] = priority;
            _values[i] = value;
        }

        public int Pop(out int priority)
        {
            priority = _priorities[0];
            int result = _values[0];
            int lastPriority = _priorities[--Count];
            int lastValue = _values[Count];
            int i = 0;
            while (true)
            {
                int child = 2 * i + 1;
                if (child >= Count)
                {
                    break;
                }
                if (child + 1 < Count && _priorities[child + 1] < _priorities[child])
                {
                    child++;
                }
                if (_priorities[child] >= lastPriority)
                {
                    break;
                }
                _priorities[i] = _priorities[child];
                _values[i] = _values[child];
                i = child;
            }
            _priorities[i] = lastPriority;
            _values[i] = lastValue;
            return result;
        }
    }
}
