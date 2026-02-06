using UnityEngine;

namespace InfiniteScroller
{
	[DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
	public abstract class InfiniteScrollerItemView : MonoBehaviour
	{
		public RectTransform rectTransform { get; private set; }

		public int Key { get; private set; }

		protected virtual void Awake()
		{
			rectTransform = (RectTransform)transform;
		}

		public void Initialize(int key, object data)
		{
			Key = key;
			DoInitialize(data);
		}

		protected abstract void DoInitialize(object data);
	}
}