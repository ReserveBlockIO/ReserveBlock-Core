using System.Collections.Concurrent;

namespace ReserveBlockCore.Data
{
	public class ConcurrentMultiDictionary<K1, K2, V>
	{
		private ConcurrentDictionary<K1, (K2, V)> Dict1 = new ConcurrentDictionary<K1, (K2, V)>();
		private ConcurrentDictionary<K2, (K1, V)> Dict2 = new ConcurrentDictionary<K2, (K1, V)>();
		private object WriteLock = new object();
		private bool UseDict2 = false;

		public V this[(K1, K2) key]
		{
			set {
				lock (WriteLock)
				{
					if(Dict1.TryGetValue(key.Item1, out var Out1))
                    {
						var Comparer = EqualityComparer<K2>.Default;
						if (!Comparer.Equals(Out1.Item1, key.Item2))
							Dict2.TryRemove(Out1.Item1, out _);
					}

					if (Dict2.TryGetValue(key.Item2, out var Out2))
					{
						var Comparer = EqualityComparer<K1>.Default;
						if (!Comparer.Equals(Out2.Item1, key.Item1))
						{
							UseDict2 = true;
							Dict1.TryRemove(Out2.Item1, out _);
						}
					}

					Dict1[key.Item1] = (key.Item2, value);
					Dict2[key.Item2] = (key.Item1, value);
					UseDict2 = false;
				}
			}
		}
		public bool TryRemoveFromKey1(K1 key, out (K2, V) KeyValue)
		{
			lock (WriteLock)
			{
				if (Dict1.TryRemove(key, out var Out1))
				{
					KeyValue = Out1;
					(K2 key2, _) = Out1;
					Dict2.TryRemove(key2, out var Out2);
					return true;
				}
			}

			KeyValue = default;
			return false;
		}

		public bool TryRemoveFromKey2(K2 key, out (K1, V) KeyValue)
		{
			lock (WriteLock)
			{
				if (Dict2.TryRemove(key, out var Out1))
				{
					KeyValue = Out1;
					(K1 key2, _) = Out1;
					Dict1.TryRemove(key2, out var Out2);
					return true;
				}
			}

			KeyValue = default;
			return false;
		}

		public bool TryGetFromKey1(K1 key1, out (K2 Key2, V Value) value)
		{
			if (Dict1.TryGetValue(key1, out var Out))
			{
				value = Out;
				return true;
			}
			value = default;
			return false;
		}

		public bool TryGetFromKey2(K2 key2, out (K1 Key1, V Value) value)
		{
			if (Dict2.TryGetValue(key2, out var Out))
			{
				value = Out;
				return true;
			}
			value = default;
			return false;
		}

		public (K1 key1, K2 key2, V value)[] ToArray()
		{
			return UseDict2 ? Dict2.Select(x => (x.Value.Item1, x.Key, x.Value.Item2)).ToArray() :
				Dict1.Select(x => (x.Key, x.Value.Item1, x.Value.Item2)).ToArray();
		}

		public (K1, K2)[] Keys { get { return UseDict2 ? Dict2.Select(x => (x.Value.Item1, x.Key)).ToArray() :
					Dict1.Select(x => (x.Key, x.Value.Item1)).ToArray(); }
        }

        public V[] Values { get { return UseDict2 ? Dict2.Values.Select(x => x.Item2).ToArray() : 
					Dict1.Values.Select(x => x.Item2).ToArray(); } }

        public int Count { get { return UseDict2 ? Dict2.Count : Dict1.Count; } }
	}
}
