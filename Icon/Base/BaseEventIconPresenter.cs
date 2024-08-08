using System;
using System.Linq;
using System.Threading.Tasks;
using DG.Tweening;
using I2.Loc;
using JetBrains.Annotations;
using share.controller.game.events.core;
using share.controller.game.HUDMagicTime;
using share.controller.game.tutorial;
using share.controller.GUI;
using share.utils;
using Spine;
using Spine.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Events {
	public abstract class BaseEventIconPresenter : BaseUIController, ITutorialWindowTarget {
		[Header("Additional functionality (Can be null)")] 
		[SerializeField] protected TextMeshProUGUI _timer = default;
		[SerializeField] protected Image _icon = default;
		[SerializeField] protected SkeletonGraphic _iconAnimation;
		[SerializeField] private Notificator _notificator;

		private EventsIconsPanelController _iconParent;
		private bool _rewardTextIsSet;
		
		protected bool IsInited { get; private set; }
		protected virtual bool DestroyOnTimesUp => true;
		protected virtual bool NoTimeLeft => GetTimeLeft() <= 0;
		protected bool NotificatorWillBeUpdatedByMT {
			get {
				HUDMagicTimeController hudMT = SystemController.GameController.HUDMagicTimeController;
				bool notificatorWillBeUpdatedByMT = NeedsHudProgressAnimation() && (hudMT.WillPlayOnThisScene || hudMT.IsPlaying);
				return notificatorWillBeUpdatedByMT;
			}
		}
		protected virtual bool HasTimer => _timer != null;
		private bool HasNotificator => _notificator != null;
		private bool HasAnimation => _iconAnimation != null;
		
		/// <summary>
		/// Обязательно вызываем base, если переопределяется!
		/// </summary>
		protected virtual void Update() => UpdateTimer();
		
		protected virtual void OnDestroy() {
			_iconParent = null;
		}

		public void UpdateNotificatorState() {
			if (!HasNotificator) {
				return;
			}

			INotificatorStateProvider notificatorStateProvider;
			NotificatorState notificatorState;
			int count = 0;
			try {
				notificatorStateProvider = GetNotificatorStateProvider();
				notificatorState = notificatorStateProvider.GetNotificatorState(out count);
			} catch (Exception e) {
				_notificator.SetNotificatorState(NotificatorState.Disabled, 0);
				Debug.LogError($"Ошибка нотификатора {e.Message} в {gameObject.name}.");
				return;
			}

			bool hasUnseenRewards = HasUnseenRewards();
			if (hasUnseenRewards) {
				DoUnseenRewardsTimerLogic();
			}
			
			_notificator.SetNotificatorState(notificatorState, count);
			notificatorStateProvider.PreviousNotificatorState = (notificatorState, count);
		}

		public void DelayedEnable(float animationInterval) {
			if (!HasAnimation) return;
			
			_canvasGroup.alpha = 0.3f;
			Sequence sequence = DOTween.Sequence();

			sequence.AppendInterval(animationInterval);
			sequence.AppendCallback(() => {
				gameObject.SetActive(true);
				_canvasGroup.DOFade(1, 0.5f);
				if(_iconAnimation.Skeleton.Data.Animations.FirstOrDefault(x=>x.Name == "animation") != null) {
					_iconAnimation.AnimationState.SetAnimation(0, "animation", false);
				}
			});
		}
		
		public void SetParent(EventsIconsPanelController iconsPanelController) {
			_iconParent = iconsPanelController;
		}

		protected void InitInternal() {
			if (HasNotificator) {
				if (NotificatorWillBeUpdatedByMT) {
					INotificatorStateProvider notificatorStateProvider = GetNotificatorStateProvider();
					(NotificatorState state, int count) notificatorState = notificatorStateProvider.PreviousNotificatorState;
					_notificator.SetNotificatorState(notificatorState.state, notificatorState.count);
					notificatorStateProvider.PreviousNotificatorState = notificatorState;
				} else {
					UpdateNotificatorState();
				}
			}
				
			UpdateTimer();

			if (HasAnimation) {
				gameObject.SetActive(false);
			}

			IsInited = true;
		}

		protected virtual bool NeedsHudProgressAnimation() {
			return false;
		}
		
		/// <summary>
		/// Чаще всего переопределять не нужно, лучше переопределить метод <see cref="GetRemainingServerTimeProvider"/>
		/// </summary>
		protected virtual long GetTimeLeft() {
			return GetRemainingServerTimeProvider().GetRemainingTime();
		}

		/// <summary>
		/// Переопределяем, если используется таймер
		/// </summary>
		protected virtual IRemainingTimeProvider GetRemainingServerTimeProvider() {
			Debug.LogError($"Не реализован GetRemainingServerTimeProvider для {name}. Реализуйте / Уберите вызов base / Не используйте таймер.");
			return null;
		}

		/// <summary>
		/// Переопределяем, если используется нотификатор
		/// </summary>
		protected virtual INotificatorStateProvider GetNotificatorStateProvider() {
			Debug.LogError($"Не реализован GetNotificatorStateProvider для {name}. Реализуйте / Уберите вызов base / Не используйте нотификатор.");
			return null;
		}

		protected virtual bool HasUnseenRewards() {
			return false;
		}

		protected async Task HideIcon() {
			_iconParent.RemoveIcon(this);
			await Hide();
			Destroy(gameObject);
		}

		protected virtual void SetTimerValueText(long remainingTime) {
			_timer.text = TimeUtility.FormatMillisecondsToTwoTimeValues(remainingTime);
		}

		protected virtual void SetTimerEndText() {
			_timer.text = ScriptLocalization.EventTimerEnd;
		}

		protected void SetTimerRewardText() {
			_rewardTextIsSet = true;
			string timerRewardText = ScriptLocalization.clan_cascading_icon_open;
			Vector3 punchForce = Vector3.one * 0.4f;
			ReadyToUseAnimations.DoPunchTextChange(DOTween.Sequence(), _timer, punchForce, timerRewardText, 0f, 0.75f);
		}

		protected virtual void DestroyIconOnTimeEnd() {
			if (SystemController.ViewManager.CurrentSceneIsMeta) {
				Destroy(gameObject);
			}
		}

		private void UpdateTimer() {
			if (!HasTimer) return;
			if (!IsInited) return;

			long remainingTime = GetTimeLeft();
			
			if (NoTimeLeft) {
				TryDestroy();
				if (!_rewardTextIsSet) {
					SetTimerEndText();
				}
			} else if (!_rewardTextIsSet) {
				SetTimerValueText(remainingTime);
			}
		}

		private void TryDestroy() {
			if (DestroyOnTimesUp) {
				DestroyIconOnTimeEnd();
			}
		}

		private void DoUnseenRewardsTimerLogic() {
			if (!_rewardTextIsSet) { // Если уже стоит текст OPEN, не имеет смысла обновлять
				SetTimerRewardText();
			}
		}
		
		public GameObject GetTutorialElement(string elementName) {
			return gameObject;
		}
		
		public static float GetLongestDuration([CanBeNull] Sequence sequence, [CanBeNull] TrackEntry trackEntry) {
			float sequenceLength = sequence?.Duration() ?? 0f;
			float trackLength = trackEntry?.AnimationEnd ?? 0f;
			return Mathf.Max(sequenceLength, trackLength);
		}

		public static void SetTimeScale([CanBeNull] Sequence sequence, [CanBeNull] TrackEntry trackEntry, float timeScale) {
			if (sequence != null && sequence.IsPlaying()) {
				sequence.timeScale = timeScale;
			}

			if (trackEntry != null && !trackEntry.IsComplete) {
				trackEntry.TimeScale = timeScale;
			}
		} 
	}
}