using UnityEngine;

namespace TraidingIDLE.UI.Charts
{
    [System.Serializable]
    public sealed class CandleHistoryBuffer
    {
        [System.Serializable]
        public struct Candle
        {
            public float open;
            public float high;
            public float low;
            public float close;
        }

        [SerializeField, Min(1)] private int capacity = 49;

        private Candle[] _values = new Candle[49];
        private int _count;
        private int _start;

        public int Capacity => capacity;
        public int Count => _count;

        public void SetCapacity(int newCapacity)
        {
            newCapacity = Mathf.Max(1, newCapacity);
            if (newCapacity == capacity)
                return;

            capacity = newCapacity;
            var newArr = new Candle[capacity];

            var copy = Mathf.Min(_count, capacity);
            for (var i = 0; i < copy; i++)
                newArr[i] = this[_count - copy + i];

            _values = newArr;
            _count = copy;
            _start = 0;
        }

        public void Clear()
        {
            _count = 0;
            _start = 0;
        }

        public void Push(Candle c)
        {
            EnsureArray();

            if (_count < capacity)
            {
                _values[(_start + _count) % capacity] = c;
                _count++;
                return;
            }

            _values[_start] = c;
            _start = (_start + 1) % capacity;
        }

        public int CopyTo(Candle[] destination)
        {
            if (destination == null)
                return 0;

            var copy = Mathf.Min(_count, destination.Length);
            for (var i = 0; i < copy; i++)
                destination[i] = this[i];

            return copy;
        }

        public Candle this[int index]
        {
            get
            {
                if (index < 0 || index >= _count)
                    throw new System.ArgumentOutOfRangeException(nameof(index));
                return _values[(_start + index) % capacity];
            }
        }

        public bool TryGetMinMax(out float min, out float max)
        {
            if (_count <= 0)
            {
                min = 0f;
                max = 0f;
                return false;
            }

            min = float.PositiveInfinity;
            max = float.NegativeInfinity;

            for (var i = 0; i < _count; i++)
            {
                var c = this[i];
                if (c.low < min) min = c.low;
                if (c.high > max) max = c.high;
            }

            return true;
        }

        private void EnsureArray()
        {
            if (_values == null || _values.Length != capacity)
                _values = new Candle[capacity];
        }
    }
}
