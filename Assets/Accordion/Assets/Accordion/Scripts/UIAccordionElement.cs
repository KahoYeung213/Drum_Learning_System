using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI.Tweens;
using UnityEngine.EventSystems;
using System;
using System.Collections;

namespace UnityEngine.UI
{
	[RequireComponent(typeof(RectTransform)), RequireComponent(typeof(LayoutElement))]
	public class UIAccordionElement : Toggle {

		[SerializeField] private float m_MinHeight = 18f;
		[SerializeField] private bool m_AutoMinHeightFromHeader = true;
		[SerializeField] private RectTransform m_HeaderTransform;
		[SerializeField] private float m_HeaderPadding = 6f;
		[SerializeField] private bool m_EnableHoverColor = false;
		[SerializeField] private Color m_HoverColor = Color.white;
		[SerializeField] private Graphic m_HoverTargetGraphic;
		
		private UIAccordion m_Accordion;
		private RectTransform m_RectTransform;
		private LayoutElement m_LayoutElement;
		private Color m_DefaultGraphicColor = Color.white;
		private bool m_HasDefaultGraphicColor = false;
		
		[NonSerialized]
		private readonly TweenRunner<FloatTween> m_FloatTweenRunner;
		
		protected UIAccordionElement()
		{
			if (this.m_FloatTweenRunner == null)
				this.m_FloatTweenRunner = new TweenRunner<FloatTween>();
			
			this.m_FloatTweenRunner.Init(this);
		}
		
		protected override void Awake()
		{
			base.Awake();
			base.transition = Transition.None;
			base.toggleTransition = ToggleTransition.None;
			this.m_Accordion = this.gameObject.GetComponentInParent<UIAccordion>();
			this.m_RectTransform = this.transform as RectTransform;
			this.m_LayoutElement = this.gameObject.GetComponent<LayoutElement>();
			if (this.m_HoverTargetGraphic == null)
			{
				this.m_HoverTargetGraphic = this.targetGraphic;
			}
			if (this.m_HoverTargetGraphic != null)
			{
				this.m_DefaultGraphicColor = this.m_HoverTargetGraphic.color;
				this.m_HasDefaultGraphicColor = true;
			}
			if (this.m_HeaderTransform == null)
			{
				Transform headerCandidate = this.transform.Find("Title");
				if (headerCandidate != null)
				{
					this.m_HeaderTransform = headerCandidate as RectTransform;
				}
			}
			this.onValueChanged.AddListener(OnValueChanged);
		}

		protected override void OnDisable()
		{
			this.RestoreDefaultHoverColor();
			base.OnDisable();
		}
		
		protected new void OnValidate()
		{
			if (this.group == null)
			{
				ToggleGroup tg = this.GetComponentInParent<ToggleGroup>();
				
				if (tg != null)
				{
					this.group = tg;
				}
			}
			
			LayoutElement le = this.gameObject.GetComponent<LayoutElement>();
			float collapsedHeight = this.GetCollapsedHeight();
			
			if (le != null)
			{
				if (this.isOn)
				{
					le.preferredHeight = -1f;
				}
				else
				{
					le.preferredHeight = collapsedHeight;
				}
			}
		}
		
		public void OnValueChanged(bool state)
		{
			if (this.m_LayoutElement == null)
				return;
			
			UIAccordion.Transition transition = (this.m_Accordion != null) ? this.m_Accordion.transition : UIAccordion.Transition.Instant;
			
			if (transition == UIAccordion.Transition.Instant)
			{
				if (state)
				{
					this.m_LayoutElement.preferredHeight = -1f;
				}
				else
				{
					this.m_LayoutElement.preferredHeight = this.GetCollapsedHeight();
				}
			}
			else if (transition == UIAccordion.Transition.Tween)
			{
				if (state)
				{
					this.StartTween(this.GetCollapsedHeight(), this.GetExpandedHeight());
				}
				else
				{
					this.StartTween(this.m_RectTransform.rect.height, this.GetCollapsedHeight());
				}
			}
		}

		protected float GetCollapsedHeight()
		{
			float collapsedHeight = this.m_MinHeight;
			if (!this.m_AutoMinHeightFromHeader || this.m_HeaderTransform == null)
				return collapsedHeight;

			float headerPreferred = LayoutUtility.GetPreferredHeight(this.m_HeaderTransform);
			if (headerPreferred <= 0f)
				headerPreferred = this.m_HeaderTransform.rect.height;

			return Mathf.Max(collapsedHeight, headerPreferred + this.m_HeaderPadding);
		}
		
		protected float GetExpandedHeight()
		{
			if (this.m_LayoutElement == null)
				return this.m_MinHeight;
			
			float originalPrefH = this.m_LayoutElement.preferredHeight;
			this.m_LayoutElement.preferredHeight = -1f;
			float h = LayoutUtility.GetPreferredHeight(this.m_RectTransform);
			this.m_LayoutElement.preferredHeight = originalPrefH;
			
			return h;
		}
		
		protected void StartTween(float startFloat, float targetFloat)
		{
			float duration = (this.m_Accordion != null) ? this.m_Accordion.transitionDuration : 0.3f;
			
			FloatTween info = new FloatTween
			{
				duration = duration,
				startFloat = startFloat,
				targetFloat = targetFloat
			};
			info.AddOnChangedCallback(SetHeight);
			info.ignoreTimeScale = true;
			this.m_FloatTweenRunner.StartTween(info);
		}
		
		protected void SetHeight(float height)
		{
			if (this.m_LayoutElement == null)
				return;
				
			this.m_LayoutElement.preferredHeight = height;
		}

		public override void OnPointerEnter(PointerEventData eventData)
		{
			base.OnPointerEnter(eventData);

			if (!this.m_EnableHoverColor)
				return;

			if (this.m_HoverTargetGraphic == null)
				return;

			this.m_HoverTargetGraphic.color = this.m_HoverColor;
		}

		public override void OnPointerExit(PointerEventData eventData)
		{
			base.OnPointerExit(eventData);
			this.RestoreDefaultHoverColor();
		}

		private void RestoreDefaultHoverColor()
		{
			if (!this.m_EnableHoverColor)
				return;

			if (this.m_HoverTargetGraphic == null || !this.m_HasDefaultGraphicColor)
				return;

			this.m_HoverTargetGraphic.color = this.m_DefaultGraphicColor;
		}
	}
}