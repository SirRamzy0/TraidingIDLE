using UnityEngine;

namespace TraidingIDLE.UI.Charts
{
    [System.Serializable]
    public sealed class PriceHistoryBuffer
    {
        [SerializeField, Min(2)] private int capacity = 50;

        private float[] _values = new float[50];
        private int _count;
        private int _start;

        public int Capacity => capacity;
        public int Count => _count;

        public void SetCapacity(int newCapacity)
        {
            newCapacity = Mathf.Max(2, newCapacity);
            if (newCapacity == capacity)
                return;

            capacity = newCapacity;
            var newArr = new float[capacity];

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

        public void Push(float v)
        {
            EnsureArray();

            if (_count < capacity)
            {
                _values[(_start + _count) % capacity] = v;
                _count++;
                return;
            }

            _values[_start] = v;
            _start = (_start + 1) % capacity;
        }

        public float this[int index]
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
                var v = this[i];
                if (v < min) min = v;
                if (v > max) max = v;
            }

            return true;
        }

        private void EnsureArray()
        {
            if (_values == null || _values.Length != capacity)
                _values = new float[capacity];
        }
    }
}

