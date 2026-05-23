// DataBuffer.cs — Rolling buffer for data smoothing and interpolation.
// Reduces NetworkTables jitter by averaging N recent samples.

using System.Collections.Generic;
using UnityEngine;

namespace SoccerBot
{
    /// <summary>Simple ring buffer with average/lerp for Vector3 values.</summary>
    public class DataBuffer
    {
        private readonly Queue<Vector3> _buffer;
        private readonly int _capacity;

        public DataBuffer(int capacity = 5)
        {
            _capacity = Mathf.Max(1, capacity);
            _buffer = new Queue<Vector3>(_capacity);
        }

        /// <summary>Add a new sample. Oldest is dropped if full.</summary>
        public void Push(Vector3 value)
        {
            _buffer.Enqueue(value);
            while (_buffer.Count > _capacity)
            {
                _buffer.Dequeue();
            }
        }

        /// <summary>Returns the arithmetic mean of buffered samples.</summary>
        public Vector3 GetAverage()
        {
            if (_buffer.Count == 0) return Vector3.zero;

            Vector3 sum = Vector3.zero;
            foreach (var v in _buffer)
            {
                sum += v;
            }
            return sum / _buffer.Count;
        }

        /// <summary>Returns the most recent sample.</summary>
        public Vector3 GetLatest()
        {
            if (_buffer.Count == 0) return Vector3.zero;
            var arr = _buffer.ToArray();
            return arr[arr.Length - 1];
        }

        /// <summary>Clears the buffer.</summary>
        public void Clear()
        {
            _buffer.Clear();
        }

        public int Count => _buffer.Count;
    }
}
