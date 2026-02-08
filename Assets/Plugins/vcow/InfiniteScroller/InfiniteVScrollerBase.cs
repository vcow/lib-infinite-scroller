#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using R3;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.EventSystems;
using UnityEngine.Pool;
using UnityEngine.UI;
using VContainer;

namespace Plugins.vcow.InfiniteScroller
{
	[DisallowMultipleComponent, RequireComponent(typeof(RectTransform), typeof(RawImage))]
	public abstract class InfiniteVScrollerBase<T> : MonoBehaviour, IInitializePotentialDragHandler,
		IBeginDragHandler, IDragHandler, IEndDragHandler
	{
		[SerializeField] private InfiniteScrollerItemView<T> _itemViewPrefab = null!;
		[SerializeField, Space] private RectTransform _contentContainer = null!;
		[SerializeField] private bool _inertiaEnabled = true;
		[SerializeField, Range(0.001f, 1f)] private float _decelerationRate = 0.135f;

		[Inject] private readonly IInfiniteScrollerDataProvider<T> _dataProvider = null!;

		private readonly CompositeDisposable _disposables = new();
		private readonly Dictionary<int, float> _itemsSizeCache = new();
		private readonly HashSet<InfiniteScrollerItemView<T>> _visibleViews = new();

		private RectTransform _rectTransform = null!;
		private ObjectPool<InfiniteScrollerItemView<T>> _itemViewPool = null!;
		private float _lastScrollerSize;
		private bool _isDragging;

		private Vector2 _pointerPosition;
		private float _velocity;

		private (float min, float max) GetVisibleContentBounds() =>
			_visibleViews.Aggregate((min: float.MaxValue, max: float.MinValue),
				(acc, view) => (Mathf.Min(acc.min, view.rectTransform.anchoredPosition.y), Mathf.Max(acc.max,
					view.rectTransform.anchoredPosition.y + GetItemViewSize(view))),
				acc => acc.min > acc.max ? (0f, 0f) : acc);

		private InfiniteScrollerItemView<T>? GetTopItemView() =>
			_visibleViews.OrderByDescending(view => view.rectTransform.anchoredPosition.y)
				.FirstOrDefault();

		private InfiniteScrollerItemView<T>? GetBottomItemView() =>
			_visibleViews.OrderBy(view => view.rectTransform.anchoredPosition.y)
				.FirstOrDefault();

		private void OnValidate()
		{
			Assert.IsNotNull(_contentContainer, "ContentContainer must not be null!");
			Assert.IsNotNull(_itemViewPrefab, "ItemViewPrefab must not be null!");
		}

		private void Awake()
		{
			_rectTransform = (RectTransform)transform;

			_disposables.Add(_itemViewPool);
			_itemViewPool = new ObjectPool<InfiniteScrollerItemView<T>>(
				() =>
				{
					var item = Instantiate(_itemViewPrefab, _contentContainer);
					AlignTransform(item.GetComponent<RectTransform>());
					return item;
				},
				view =>
				{
					_visibleViews.Add(view);
					view.gameObject.SetActive(true);
				},
				view =>
				{
					_visibleViews.Remove(view);
					view.gameObject.SetActive(false);
				},
				view =>
				{
					_visibleViews.Remove(view);
					Destroy(view.gameObject);
				});
		}

		private void Start()
		{
			Assert.IsNotNull(_contentContainer);
			AlignTransform(_contentContainer);

			ApplyContent();

			_dataProvider.AddObservable
				.Subscribe(AddItemInBottom)
				.AddTo(_disposables);
			_dataProvider.RemoveObservable
				.Subscribe(_ => throw new NotImplementedException())
				.AddTo(_disposables);

			_lastScrollerSize = _rectTransform.rect.height;
		}

		private void OnDestroy()
		{
			_disposables.Dispose();
		}

		private void ApplyContent()
		{
			Assert.IsNotNull(_contentContainer);
			foreach (Transform child in _contentContainer)
			{
				var itemView = child.GetComponent<InfiniteScrollerItemView<T>>();
				if (itemView != null)
				{
					_itemViewPool.Release(itemView);
				}
			}

			Assert.IsFalse(_visibleViews.Any());
			_itemsSizeCache.Clear();
			var contentSize = 0f;

			_contentContainer.anchoredPosition = Vector2.zero;
			LayoutRebuilder.ForceRebuildLayoutImmediate(_rectTransform);

			var scrollRectHeight = _rectTransform.rect.height;
			var item = _dataProvider.GetLastItem();
			while (item.HasValue)
			{
				var itemView = GetAndInitializeItemView(item.Value.key, item.Value.data);
				itemView.rectTransform.anchoredPosition = new Vector2(0f, contentSize);
				contentSize += GetItemViewSize(itemView);

				if (contentSize >= scrollRectHeight)
				{
					break;
				}

				item = _dataProvider.GetPrevItem(item.Value.key);
			}
		}

		private void AddItemInBottom((int key, T data) itemData)
		{
			var bottomItemView = GetBottomItemView();
			InfiniteScrollerItemView<T> itemView;
			if (bottomItemView == null)
			{
				// first item
				itemView = GetAndInitializeItemView(itemData.key, itemData.data);
				itemView.rectTransform.anchoredPosition = Vector2.zero;
				return;
			}

			var prevItem = _dataProvider.GetPrevItem(itemData.key);
			if (!prevItem.HasValue)
			{
				Debug.LogError("[InfiniteVScroller] The sequence of elements in the list is broken.");
				return;
			}

			if (prevItem.Value.key != bottomItemView.Key)
			{
				// bottom is to fare from visible area
				return;
			}

			itemView = GetAndInitializeItemView(itemData.key, itemData.data);
			var bottomPosition = bottomItemView.rectTransform.anchoredPosition.y - GetItemViewSize(itemView);
			itemView.rectTransform.anchoredPosition = new Vector2(0f, bottomPosition);
			_contentContainer.anchoredPosition = new Vector2(0f, -bottomPosition);

			_velocity = 0f;
			_isDragging = false;

			UpdateVisibleContentAndCorrectOffset(0f);
		}

		private static void AlignTransform(RectTransform rectTransform)
		{
			rectTransform.anchorMin = new Vector2(0f, 0f);
			rectTransform.anchorMax = new Vector2(1f, 0f);
			rectTransform.pivot = new Vector2(0.5f, 0f);
			rectTransform.offsetMin = new Vector2(0f, rectTransform.offsetMin.y);
			rectTransform.offsetMax = new Vector2(0f, rectTransform.offsetMax.y);
			rectTransform.anchoredPosition = new Vector2(0f, 0f);
			rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, 100f);
			rectTransform.localRotation = Quaternion.identity;
			rectTransform.localScale = Vector3.one;
		}

		void IInitializePotentialDragHandler.OnInitializePotentialDrag(PointerEventData eventData)
		{
			_pointerPosition = eventData.position;
			_isDragging = false;
			_velocity = 0f;
		}

		void IBeginDragHandler.OnBeginDrag(PointerEventData eventData)
		{
			var (min, max) = GetVisibleContentBounds();
			_isDragging = _rectTransform.rect.height < max - min;
			if (_isDragging)
			{
				_velocity = 0f;
			}
		}

		void IDragHandler.OnDrag(PointerEventData eventData)
		{
			if (!_isDragging)
			{
				return;
			}

			if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
				    _contentContainer, _pointerPosition, null, out var p1) ||
			    !RectTransformUtility.ScreenPointToLocalPointInRectangle(
				    _contentContainer, eventData.position, null, out var p2))
			{
				Debug.LogError("[InfiniteVScroller] Failed to get scroll point.");
				return;
			}

			var pointsOffset = eventData.position - _pointerPosition;
			_pointerPosition = eventData.position;

			var offset = UpdateVisibleContentAndCorrectOffset(p2.y - p1.y);
			if (Mathf.Approximately(offset, 0f))
			{
				_velocity = 0f;
				return;
			}

			_contentContainer.anchoredPosition += new Vector2(0f, offset);

			var unscaledDeltaTime = Time.unscaledDeltaTime;
			if (unscaledDeltaTime > 0f)
			{
				_velocity = pointsOffset.y / unscaledDeltaTime;
			}
			else
			{
				_velocity = 0f;
			}
		}

		void IEndDragHandler.OnEndDrag(PointerEventData eventData)
		{
			if (!_isDragging)
			{
				return;
			}

			_isDragging = false;
			if (!_inertiaEnabled)
			{
				_velocity = 0f;
				return;
			}

			var pointsOffset = eventData.position - _pointerPosition;
			_pointerPosition = eventData.position;

			var unscaledDeltaTime = Time.unscaledDeltaTime;
			if (unscaledDeltaTime > 0f)
			{
				_velocity = pointsOffset.y / unscaledDeltaTime;
			}
			else
			{
				_velocity = 0f;
			}
		}

		private void Update()
		{
			var scrollerSize = _rectTransform.rect.height;
			if (!Mathf.Approximately(scrollerSize, _lastScrollerSize))
			{
				if (scrollerSize > _lastScrollerSize)
				{
					UpdateVisibleContentAndCorrectOffset(0f);
				}

				_lastScrollerSize = scrollerSize;
			}

			if (_isDragging || !_inertiaEnabled || Mathf.Approximately(_velocity, 0f))
			{
				return;
			}

			var deltaTime = Time.unscaledDeltaTime;
			if (deltaTime <= 0f)
			{
				return;
			}

			var position = _pointerPosition + new Vector2(0f, _velocity * deltaTime);
			if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
				    _contentContainer, _pointerPosition, null, out var p1) ||
			    !RectTransformUtility.ScreenPointToLocalPointInRectangle(
				    _contentContainer, position, null, out var p2))
			{
				Debug.LogError("[InfiniteVScroller] Failed to get scroll point.");
				return;
			}

			_pointerPosition = position;
			var offset = UpdateVisibleContentAndCorrectOffset(p2.y - p1.y);

			if (Mathf.Approximately(offset, 0f))
			{
				_velocity = 0f;
				return;
			}

			_contentContainer.anchoredPosition += new Vector2(0f, offset);

			_velocity *= Mathf.Pow(_decelerationRate, deltaTime);
			if (Mathf.Abs(_velocity) < 1f)
			{
				_velocity = 0f;
			}
		}

		private float UpdateVisibleContentAndCorrectOffset(float offset)
		{
			if (!_visibleViews.Any())
			{
				return 0f;
			}

			const float scrollRectMin = 0f;
			var scrollRectMax = _rectTransform.rect.height;
			var contentContainerPosition = _contentContainer.anchoredPosition.y;

			var topItemView = GetTopItemView()!;
			var bottomItemView = GetBottomItemView()!;

			var contentMax = topItemView.rectTransform.anchoredPosition.y + GetItemViewSize(topItemView);
			var contentMin = bottomItemView.rectTransform.anchoredPosition.y;

			while (scrollRectMax >= contentContainerPosition + contentMax + offset)
			{
				// leaving the upper limit
				var prevItemData = _dataProvider.GetPrevItem(topItemView.Key);
				if (prevItemData.HasValue)
				{
					var prevItemView = GetAndInitializeItemView(prevItemData.Value.key, prevItemData.Value.data);
					prevItemView.rectTransform.anchoredPosition = new Vector2(0f, contentMax);
					topItemView = prevItemView;
					contentMax += GetItemViewSize(prevItemView);
				}
				else
				{
					offset = scrollRectMax - contentContainerPosition - contentMax;
					break;
				}
			}

			while (scrollRectMin <= contentContainerPosition + contentMin + offset)
			{
				// leaving the lower limit
				var nextItemData = _dataProvider.GetNextItem(bottomItemView.Key);
				if (nextItemData.HasValue)
				{
					var nextItemView = GetAndInitializeItemView(nextItemData.Value.key, nextItemData.Value.data);
					bottomItemView = nextItemView;
					contentMin -= GetItemViewSize(nextItemView);
					nextItemView.rectTransform.anchoredPosition = new Vector2(0f, contentMin);
				}
				else
				{
					offset = scrollRectMin - contentContainerPosition - contentMin;
					break;
				}
			}

			float size;
			while (scrollRectMax < contentContainerPosition + contentMax + offset - (size = GetItemViewSize(topItemView)))
			{
				var nextItemData = _dataProvider.GetNextItem(topItemView.Key);
				if (!nextItemData.HasValue)
				{
					break;
				}

				_itemViewPool.Release(topItemView);
				contentMax -= size;

				if (!GetVisibleItemView(nextItemData.Value.key, out var nextItemView))
				{
					nextItemView = GetAndInitializeItemView(nextItemData.Value.key, nextItemData.Value.data);
					nextItemView.rectTransform.anchoredPosition = new Vector2(0f, contentMax - GetItemViewSize(nextItemView));
				}

				topItemView = nextItemView!;
			}

			while (scrollRectMin > contentContainerPosition + contentMin + offset + (size = GetItemViewSize(bottomItemView)))
			{
				var prevItemData = _dataProvider.GetPrevItem(bottomItemView.Key);
				if (!prevItemData.HasValue)
				{
					break;
				}

				_itemViewPool.Release(bottomItemView);
				contentMin += size;

				if (!GetVisibleItemView(prevItemData.Value.key, out var prevItemView))
				{
					prevItemView = GetAndInitializeItemView(prevItemData.Value.key, prevItemData.Value.data);
					prevItemView.rectTransform.anchoredPosition = new Vector2(0f, contentMin);
				}

				bottomItemView = prevItemView!;
			}

			return offset;
		}

		private bool GetVisibleItemView(int key, out InfiniteScrollerItemView<T>? itemView)
		{
			foreach (Transform child in _contentContainer)
			{
				itemView = child.GetComponent<InfiniteScrollerItemView<T>>();
				if (!itemView || !itemView.gameObject.activeSelf)
				{
					continue;
				}

				if (itemView.Key == key)
				{
					return true;
				}
			}

			itemView = null;
			return false;
		}

		private InfiniteScrollerItemView<T> GetAndInitializeItemView(int key, T data)
		{
			var itemView = _itemViewPool.Get();
			itemView.Initialize(key, data);
			return itemView;
		}

		private float GetItemViewSize(InfiniteScrollerItemView<T> itemView)
		{
			if (!_itemsSizeCache.TryGetValue(itemView.Key, out var size))
			{
				LayoutRebuilder.ForceRebuildLayoutImmediate(itemView.rectTransform);
				size = itemView.rectTransform.rect.height;
				_itemsSizeCache.Add(itemView.Key, size);
			}

			return size;
		}
	}
}