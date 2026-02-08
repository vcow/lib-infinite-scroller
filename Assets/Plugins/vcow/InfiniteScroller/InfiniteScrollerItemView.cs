using UnityEngine;

namespace Plugins.vcow.InfiniteScroller
{
	[DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
	public abstract class InfiniteScrollerItemView<T> : MonoBehaviour
	{
		public RectTransform rectTransform { get; private set; }

		public int Key { get; private set; }

		protected virtual void Awake()
		{
			rectTransform = (RectTransform)transform;
		}

		public void Initialize(int key, T data)
		{
			Key = key;
			DoInitialize(data);
		}

		protected abstract void DoInitialize(T data);
	}
}