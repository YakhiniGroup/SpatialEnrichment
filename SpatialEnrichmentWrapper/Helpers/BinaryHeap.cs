using System.Collections;
using System.Collections.Generic;

namespace ResearchCommonLib.DataStructures
{
    public enum HeapType
    {
        MinHeap,
        MaxHeap
    }

    public class MinHeap<T> : BinaryHeapBase<T>
    {
        public MinHeap(IEnumerable<T> elements = null, int maxSize = int.MaxValue, IComparer<T> comparer = null)
            : base(HeapType.MinHeap, elements, maxSize, comparer)
        {
            
        }

        public T Min
        {
            get { return this.Root; }
        }

        public void DeleteMin()
        {
            this.DeleteRoot();
        }

        public T PopMin()
        {
            return this.PopRoot();
        } 
    }

    public class MaxHeap<T> : BinaryHeapBase<T>
    {
        public MaxHeap(IEnumerable<T> elements = null, int maxSize = int.MaxValue, IComparer<T> comparer = null)
            : base(HeapType.MaxHeap, elements, maxSize, comparer)
        {
            
        }

        public T Max
        {
            get { return this.Root; }
        }

        public void DeleteMax()
        {
            this.DeleteRoot();
        }

        public T PopMax()
        {
            return this.PopRoot();
        } 
    }

    public abstract class BinaryHeapBase<T> : IEnumerable<T>
    {
        private readonly List<T> _elements;

        private readonly HeapType _heapType;

        private readonly object _heapLocker;

        private readonly IComparer<T> _comparer;

        private readonly int _maxSize;

        public int Size
        {
            get
            {
                lock (_heapLocker)
                {
                    return this._elements.Count;
                }
            }
        }

        protected T Root
        {
            get
            {
                lock (_heapLocker)
                {
                    return this._elements[0];
                }
            }
        }

        protected BinaryHeapBase(HeapType type, IEnumerable<T> elements = null, int maxSize = int.MaxValue, IComparer<T> comparer = null)
        {
            this._heapType = type;
            this._comparer = comparer ?? Comparer<T>.Default;
            this._elements = new List<T>();
            this._heapLocker = new object();
            this._maxSize = maxSize;

            if (elements != null)
            {
                foreach (var element in elements)
                {
                    this.Insert(element);
                }
            }
        }

        public void Insert(T item)
        {
            lock (this._heapLocker)
            {
                var flag = this._heapType == HeapType.MinHeap;

                if (this._elements.Count == this._maxSize)
                {
                    if ((this._comparer.Compare(item, this.Root) < 0) ^ flag)
                    {
                        this.DeleteRoot();
                    }
                    else
                    {
                        return;
                    }
                }

                this._elements.Add(item);

                var i = this._elements.Count - 1;

                while (i > 0)
                {
                    if ((this._comparer.Compare(this._elements[i], this._elements[(i - 1) / 2]) > 0) ^ flag)
                    {
                        var temp = this._elements[i];
                        this._elements[i] = this._elements[(i - 1) / 2];
                        this._elements[(i - 1) / 2] = temp;
                        i = (i - 1) / 2;
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        protected void DeleteRoot()
        {
            lock (this._heapLocker)
            {
                var i = this._elements.Count - 1;

                this._elements[0] = this._elements[i];
                this._elements.RemoveAt(i);

                i = 0;

                var flag = _heapType == HeapType.MinHeap;

                while (true)
                {
                    var leftInd = 2 * i + 1;
                    var rightInd = 2 * i + 2;
                    var largest = i;

                    if (leftInd < this._elements.Count)
                    {
                        if ((this._comparer.Compare(this._elements[leftInd], this._elements[largest]) > 0) ^ flag)
                        {
                            largest = leftInd;                            
                        }
                    }

                    if (rightInd < this._elements.Count)
                    {
                        if ((this._comparer.Compare(this._elements[rightInd], this._elements[largest]) > 0) ^ flag)
                        {
                            largest = rightInd;
                        }
                    }

                    if (largest != i)
                    {
                        var temp = this._elements[largest];
                        this._elements[largest] = this._elements[i];
                        this._elements[i] = temp;
                        i = largest;
                    }
                    else
                        break;
                }
            }
        }

        protected T PopRoot()
        {
            lock (this._heapLocker)
            {
                var result = this._elements[0];

                DeleteRoot();

                return result;
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            return this._elements.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this._elements.GetEnumerator();
        }
    }
}
