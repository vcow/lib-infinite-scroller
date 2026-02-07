using System;
using System.Collections.Generic;
using System.Linq;
using R3;

namespace InfiniteScroller
{
	public abstract class ChatDataProviderBase<T> : IInfiniteScrollerDataProvider<T>, IDisposable
	{
		private struct ItemData
		{
			public int Index;
			public T Data;
		}

		private readonly CompositeDisposable _disposables = new();
		private readonly Subject<(int key, T data)> _addObservable;
		private readonly Subject<int> _removeObservable;

		private readonly Dictionary<int, LinkedListNode<ItemData>> _nodeDictionary = new();
		private readonly LinkedList<ItemData> _items = new();

		private int _nextIndex;

		public IEnumerable<(int key, T data)> Items => _items.Select(i => (i.Index, i.Data));
		public Observable<(int key, T data)> AddObservable => _addObservable;
		public Observable<int> RemoveObservable => _removeObservable;

		protected ChatDataProviderBase()
		{
			_addObservable = new Subject<(int key, T data)>().AddTo(_disposables);
			_removeObservable = new Subject<int>().AddTo(_disposables);
		}

		public virtual void Dispose()
		{
			_nodeDictionary.Clear();
			_items.Clear();
			_disposables.Dispose();
		}

		public int Add(T data)
		{
			var index = _nextIndex++;
			var node = _items.AddLast(new ItemData { Index = index, Data = data });
			_nodeDictionary.Add(index, node);
			_addObservable.OnNext((index, data));
			return index;
		}

		public bool Remove(int key)
		{
			if (!_nodeDictionary.TryGetValue(key, out var node))
			{
				return false;
			}

			_items.Remove(node);
			_removeObservable.OnNext(key);
			return true;
		}

		public (int key, T data)? GetPrevItem(int key)
		{
			if (!_nodeDictionary.TryGetValue(key, out var node))
			{
				return null;
			}

			var prev = node.Previous;
			return prev != null ? (prev.Value.Index, prev.Value.Data) : null;
		}

		public (int key, T data)? GetNextItem(int key)
		{
			if (!_nodeDictionary.TryGetValue(key, out var node))
			{
				return null;
			}

			var next = node.Next;
			return next != null ? (next.Value.Index, next.Value.Data) : null;
		}
	}
}