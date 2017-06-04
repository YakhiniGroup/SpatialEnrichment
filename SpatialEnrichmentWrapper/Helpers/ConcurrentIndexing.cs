using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Xml.Serialization;

namespace SpatialEnrichment.Helpers
{
    public class ConcurrentIndexing<T> : IEnumerable<KeyValuePair<long, T>>
    {
        [NonSerialized, XmlIgnore, SoapIgnore]
        private ConcurrentDictionary<T, long> map;
        [NonSerialized, XmlIgnore, SoapIgnore]    
        private ConcurrentDictionary<long, T> invmap;
        private long idx = -1; //Zero based indexer

        public ConcurrentIndexing()
        {
            map = new ConcurrentDictionary<T, long>();
            invmap = new ConcurrentDictionary<long, T>();
        }

        public void LoadFromDictionary(Dictionary<T, long> dict)
        {
            foreach (var l in dict)
            {
                map.AddOrUpdate(l.Key, t => l.Value, (a, b) => l.Value);
                invmap.AddOrUpdate(l.Value, t => l.Key, (a, b) => l.Key);
            }
        }

        public long GetOrAdd(T item)
        {
            var key = map.GetOrAdd(item, IncreaseIdx);
            invmap.AddOrUpdate(key, t => item, (a, b) => item);
            return key;
        }

        public long IncreaseIdx()
        {
            return Interlocked.Increment(ref idx);
        }

        private long IncreaseIdx(T item)
        {
            return Interlocked.Increment(ref idx);
        }

        public long this[T obj]
        {
            get { return map[obj]; }
            set { map.AddOrUpdate(obj, t => value, (a, b) => value); }
        }

        public T this[long index]
        {
            get { return invmap[index]; }
        }

        public IEnumerator<KeyValuePair<long, T>> GetEnumerator()
        {
            return invmap.OrderBy(t => t.Key).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(T item)
        {
            GetOrAdd(item);
        }

/*
        protected ConcurrentIndexing(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
                throw new ArgumentNullException("info");
            idx = (long)info.GetValue("idx", typeof(long));
            var tmap = (Dictionary<T, long>)info.GetValue("map", typeof(Dictionary<T, long>));
            var tinvmap = (Dictionary<long, T>)info.GetValue("invmap", typeof(Dictionary<long, T>));
            map = new ConcurrentDictionary<T, long>();
            invmap = new ConcurrentDictionary<long, T>();
        }


        public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
                throw new ArgumentNullException("info");
            info.AddValue("idx", idx, typeof(long));
            info.AddValue("map", map.ToDictionary(t=>t.Key,t=>t.Value), typeof(Dictionary<T, long>));
            info.AddValue("invmap", invmap.ToDictionary(t => t.Key, t => t.Value), typeof(Dictionary<long, T>));
        }
*/

        public void ClearKeepIndex()
        {
            map.Clear();
            invmap.Clear();
        }

        public bool ContainsKey(long key)
        {
            return invmap.ContainsKey(key);
        }
    }
}

