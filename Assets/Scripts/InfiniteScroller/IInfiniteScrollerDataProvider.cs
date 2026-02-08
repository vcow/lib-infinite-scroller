#nullable enable

using System.Collections.Generic;
using R3;

namespace InfiniteScroller
{
	public interface IInfiniteScrollerDataProvider<T>
	{
		IEnumerable<(int key, T data)> Items { get; }
		Observable<(int key, T data)> AddObservable { get; }
		Observable<int> RemoveObservable { get; }
		int Add(T data);
		bool Remove(int key);
		(int key, T data)? GetPrevItem(int key);
		(int key, T data)? GetNextItem(int key);
		(int key, T data)? GetFirstItem();
		(int key, T data)? GetLastItem();
	}
}