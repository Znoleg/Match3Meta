using System.Collections.Generic;
using System.Threading.Tasks;
using DG.Tweening;
using I2.Loc;
using share.controller.game.events.local.ProductSales;
using share.controller.game.events.local.ProductSales.ProductSounds;
using share.controller.GUI.events.OneProduct;
using share.controller.GUI.Windows.Base;
using share.controller.statistic.eventDummies.core;
using share.manager;
using share.model.bank;
using share.model.events.local;
using share.model.rewards;
using share.model.user.events.local;
using share.utils;
using Sound;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace share.controller.GUI.events {
	public abstract class ProductSaleWindowBase<TSaleEventController, TProductSalePresenter> : BaseUIController<string>, IProductSaleWindow, IWindowClosedCallbackReceiver where TSaleEventController : ProductSaleControllerBase where TProductSalePresenter : ProductSalePresenterBase {
		[SerializeField] private BaseView _view;
		[SerializeField] private Button _closeWindowButton;
		[SerializeField] private Button _additionalCloseButton;
		[SerializeField] private TProductSalePresenter[] _salePresenters;
		[SerializeField] private ProductSoundPlayerBase _soundPlayer;
		[SerializeField] private TextMeshProUGUI _timer;
		[SerializeField] private RectTransform _characterStatic;
		
		private ProductSaleModel _model;
		private ProductSaleData _data;
		private TProductSalePresenter _currentRealCurrencyBuyPresenter;
		
		protected IReadOnlyList<TProductSalePresenter> SalePresenters => _salePresenters;
		protected string EventId { get; private set; }
		protected BaseView View => _view;

		protected override void HandleArguments(string argument) {
			EventId = argument;
			if (_view != null) {
				_view.Init();
			}
			
			_closeWindowButton.onClick.RemoveAllListeners();
			_closeWindowButton.onClick.AddListener(CloseWindow);

			if (_additionalCloseButton != null) {
				_additionalCloseButton.onClick.RemoveAllListeners();
				_additionalCloseButton.onClick.AddListener(CloseWindow);
			}
			
			_soundPlayer?.Play();
			SystemController.ViewManager.Show(UiPanel.BlockingBackground);
			InitPresenters();
			InitRewards();

			GetSaleController().SendStatistic(GetSaleController().StatisticsSendName, StatisticEventActionsAction.Show);
		}

		public override Task Hide() {
			GetSaleController().SendStatistic(GetSaleController().StatisticsSendName, StatisticEventActionsAction.Hide);
			return base.Hide();
		}

		public override void MakeShowAnimation(Sequence sequence) {
			base.MakeShowAnimation(sequence);
			
			if (_characterStatic != null) {
				sequence.Insert(0f, ReadyToUseAnimations.DoCharacterHorizontalSlide(_characterStatic, delay: 0f));
			}
		}

		public override void MakeHideAnimation(Sequence sequence) {
			base.MakeHideAnimation(sequence);
			
			if (_characterStatic != null) {
				sequence.Insert(0f, ReadyToUseAnimations.DoCharacterHorizontalSlideBack(_characterStatic, delay: 0f, duration: 0.3f, customOutOfCanvas: -2050));
			}
		}

		public override SoundManager.AudioClipType GetSoundOnopen() {
			return _soundPlayer != null ? SoundManager.AudioClipType.None : base.GetSoundOnopen();
		}

		public ProductSaleControllerBase GetSaleControllerBase() {
			return GetSaleController();
		}

		private TSaleEventController _controller;
		protected TSaleEventController GetSaleController() {
			if (_controller != null) {
				return _controller;
			}
			if (string.IsNullOrEmpty(EventId)) {
				_controller = SystemController.GameController.LocalEventsController.GetEventController<TSaleEventController>();
			} else {
				_controller = SystemController.GameController.LocalEventsController.GetEventControllerById<TSaleEventController>(EventId);
			}


			return _controller;
		}

		protected void HideWindow() {
			SystemController.ViewManager.Hide(GetSaleController().GetMainWindow());
		}
		
		protected void CloseWindow() {
			GetSaleController().SendStatistic(GetSaleController().StatisticsSendName, StatisticEventActionsAction.Close);

			if (GetModel().CompleteOnClose) {
				SystemController.ViewManager.Show<BasicAcceptWindowController, BasicSaleAcceptWindowArgs>(Window.StartedSaleAcceptWindow,
					withHide: GetSaleController().GetMainWindow(),
					parameter: new BasicSaleAcceptWindowArgs() {
						OnYesTap = GetSaleController().Complete,
						OnNoTap = () => GetSaleController().TryShowSaleWindow(),
						StatisticsId = EventId
					});
			} else {
				HideWindow();
			}
		}
		
		protected ProductSaleModel GetModel() {
			return _model ??= GetSaleController().GetProto<ProductSaleModel>();
		}

		protected ProductSaleData GetData() {
			return _data ??= GetSaleController().GetData<ProductSaleData>();
		}

		private void InitPresenters() {
			for (int presenterIndex = 0; presenterIndex < _salePresenters.Length; presenterIndex++) {
				TProductSalePresenter salePresenter = _salePresenters[presenterIndex];
				bool isTaken = GetData().IsGoodsPurchased[presenterIndex];
				
				BankGoodsData presenterBankData = GetModel().Goods.BankGoodsDatas[presenterIndex];
				ICardBehaviour cardBehaviour = GetCardBehaviour(salePresenter, presenterIndex, isTaken);
				
				InitPresenter(cardBehaviour, presenterBankData, salePresenter, presenterIndex, isTaken);
			}
		}

		protected virtual void InitPresenter(ICardBehaviour cardBehaviour, BankGoodsData presenterBankData, TProductSalePresenter salePresenter, int presenterIndex, bool isTaken) {
			ProductSalePresenterArgs args = new() {
				IsTaken = isTaken,
				PackLoaded = cardBehaviour.CanBePurchased,
				HasInternet = cardBehaviour.HasInternet,
				GoodsData = presenterBankData,
				OnBuy = cardBehaviour.Buy,
				OnNoInternet = ShowNoInternet,
			};
			
			salePresenter.Init(args);
		}

		protected abstract ICardBehaviour GetCardBehaviour(ProductSalePresenterBase salePresenter, int presenterIndex, bool isTaken);
		
		private void InitRewards() {
			for (int goodsIndex = 0; goodsIndex < GetModel().Goods.BankGoodsDatas.Length; goodsIndex++) {
				InitPresenterRewards(goodsIndex);
			}
		}

		private void InitPresenterRewards(int goodsIndex) {
			BankGoodsData currentGoods = GetModel().Goods.BankGoodsDatas[goodsIndex];
			List<IReward> rewards = GetPresenterRewards(currentGoods);
			ProductSalePresenterBase rewardsOwner = _salePresenters[goodsIndex];
			foreach (IReward reward in rewards) {
				if (reward.Type == RewardType.money) continue;
				if (!reward.IsView) continue;
				rewardsOwner.CloneRewardInto(reward);
			}
		}

		protected virtual List<IReward> GetPresenterRewards(BankGoodsData currentGoods) {
			List<IReward> rewards = null;
			if (currentGoods.saleType == SaleType.Free) {
				rewards = currentGoods.freeCurrencyOffer.RewardModel.rewards;
			} else if (currentGoods.saleType == SaleType.HardCurrencyBuy) {
				rewards = currentGoods.hardCurrencyOffer.RewardModel.rewards;
			} else {
				rewards = currentGoods.bankGood.RewardModel.rewards;
			}

			return rewards;
		}
		
		public void ShowNoInternet() {
			SystemController.ViewManager.Show<NoInternetWindowController, NoInternetWindowParameter>(Window.NoInternet, new NoInternetWindowParameter(Window.Empty, this, null, NoInternetCallback));
			SystemController.ViewManager.Hide(GetModel().MainWindow);
		}

		private void NoInternetCallback() {
			SystemController.BankController.Update();
			SystemController.ViewManager.Show(GetModel().MainWindow);
		}

		public void OnWindowClosed(Window window, bool wasWindowActionSuccessful) {
			if (window == Window.NoInternet) {
				SystemController.ViewManager.Show(GetModel().MainWindow);
			}
		}
		
		private void Update() {
			if (_timer == null) return;
			
			long remainingTime = -1;
			if (GetSaleController() != null) {
				remainingTime = GetSaleController().GetRemainingTime();
			}
			if (remainingTime >= 0) {
				_timer.text = TimeUtility.FormatMillisecondsToTwoTimeValues(remainingTime);
			} else {
				_timer.text = ScriptLocalization.EventTimerEnd;
			}
		}
	}
	
	public interface ICardBehaviour {
		bool CanBePurchased { get; }
		bool HasInternet { get; }
		void Buy();
	}

	public interface IProductSaleWindow {
		ProductSaleControllerBase GetSaleControllerBase();
		void ShowNoInternet();
	}
}