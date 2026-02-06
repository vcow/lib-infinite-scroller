using System.Collections.Generic;
using R3;

namespace InfiniteScroller
{
	public interface IInfiniteScrollerDataProvider
	{
		IEnumerable<(int key, object data)> Items { get; }
		Observable<(int key, object data)> AddObservable { get; }
		Observable<int> RemoveObservable { get; }
		int Add(object data);
		bool Remove(int key);
		(int key, object data)? GetPrevItem(int key);
		(int key, object data)? GetNextItem(int key);
	}
}