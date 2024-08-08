using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using I2.Loc;
using share.controller.core;
using share.controller.game;
using share.controller.GUI.View.Base;
using share.model.bank;
using share.model.events.local;
using share.model.rewards;
using TMPro;
using UI;
using UI.Extensions;
using UI.Flare;
using UI.Rewards;
using UI.Rewards.Presenter;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace share.controller.GUI.events {
	public abstract class ProductSalePresenterBase : MonoBehaviour {
		[SerializeField] private RewardItemPresenter _rewardItemPrefab;
		[SerializeField] private Transform _rewardsPanel;
		protected RewardItemPresenter RewardItemPrefab => _rewardItemPrefab;
		
		public abstract void Init(ProductSalePresenterArgs args);
		
		public virtual RewardItemPresenter CloneRewardInto(IReward reward, bool useSmallVersion = true) {
			GameController gameController = BaseController.SystemController.GameController;
			RewardItemPresenter instance = _rewardItemPrefab.Clone(gameController.RewardItemDatabase, gameController.RewardSpriteProvider, _rewardsPanel, reward, useSmallVersion);
			instance.SetTooltipVisual();
			return instance;
		}

		public abstract void OnRealCurrencyBuyStart();
		public abstract void OnRealCurrencyBuyEnd();

		public abstract Sequence GetOpenSequence(float buttonsStart = 0.05f, float buttonsDuration = 0.4f, float buttonTextsStart = 0.1f, float buttonTextsDuration = 0.6f, float stickerStart = 0.5f, float stickerDuration = 0.5f);
		public abstract bool CanFlare { get; }
		public abstract bool TryGetFlaresSequence(out Sequence _);
	}

	public struct ProductSalePresenterArgs {
		public bool IsTaken;
		public bool PackLoaded;
		public bool HasInternet;

		public BankGoodsData GoodsData;

		public UnityAction OnBuy;
		public UnityAction OnNoInternet;
	}
	
	public class ProductSalePresenter : ProductSalePresenterBase {
		[SerializeField] private GameObject _takenStateObject;
		[SerializeField] private Button _buyButton;
		[SerializeField] private Button _noInetButton;
		[SerializeField] private Button _additionalBuyButton;
		[SerializeField] private Button _additionalNoInetButton;
		[SerializeField] private TextMeshProUGUI _priceText;
		[SerializeField] private TextMeshProUGUI _coinsText;
		[SerializeField] private TextMeshProUGUI _discountText;
		[SerializeField] private GameObject _saleCloud;
		[SerializeField] private bool _needCoinsPrefix = true;
		[SerializeField] private GridByCenterLayoutGroup _gridLayout;
		[SerializeField, Tooltip("Ставить ли визуал GridLayout как в тултипах")] private bool _gridTooltipVisual;

		[Header("Animation elements")] 
		[SerializeField] private RectTransform[] _animButtons;
		[SerializeField] private RectTransform[] _animButtonTexts;
		[SerializeField] private CanvasGroup _animSale;
		[SerializeField] private UIFlare _chestFlare;
		[SerializeField] private UIFlare _saleFlare;
		[SerializeField] private UIFlare _buyButtonFlare;
		[SerializeField] private UIFlare _noInetButtonFlare;
		
		public override bool CanFlare => !IsTaken;
		
		public bool IsTaken { get; private set; }
		protected GameObject SaleCloud => _saleCloud;
		protected Button BuyButton => _buyButton;
		protected Button NoInetButton => _noInetButton;
		protected GridByCenterLayoutGroup GridLayout => _gridLayout;
		protected TextMeshProUGUI PriceTextMesh => _priceText;
		protected TextMeshProUGUI CoinsTextMesh => _coinsText;
		protected TextMeshProUGUI DiscountTextMesh => _discountText;

		public override void Init(ProductSalePresenterArgs args) {
			IsTaken = args.IsTaken;

			InitButtons(args.OnBuy, args.OnNoInternet, args.HasInternet);
			InitVisual();
			InitTexts(args.GoodsData);
			InitRewardGrid();
		}

		public override void OnRealCurrencyBuyStart() {
			SetBuyButtonInteractable(false);
			SetInetButtonInteractable(false);
		}

		public override void OnRealCurrencyBuyEnd() {
			SetBuyButtonInteractable(true);
			SetInetButtonInteractable(true);
		}

		public override Sequence GetOpenSequence(float buttonsStart = 0.05f, float buttonsDuration = 0.4f, float buttonTextsStart = 0.1f, float buttonTextsDuration = 0.6f, float stickerStart = 0.5f, float stickerDuration = 0.5f) {
			Sequence sequence = DOTween.Sequence();

			foreach (RectTransform rectTransform in _animButtons) {
				sequence.Insert(buttonsStart, rectTransform.DOScale(Vector3.one, buttonsDuration).SetEase(Ease.OutBack));
			}

			foreach (RectTransform rectTransform in _animButtonTexts) {
				sequence.Insert(buttonTextsStart, rectTransform.DOScale(Vector3.one, buttonTextsDuration).SetEase(Ease.OutBack));
			}

			sequence.Insert(stickerStart, _animSale.DOFade(1f, stickerDuration / 2.5f).SetEase(Ease.Linear));
			sequence.Insert(stickerStart, _animSale.transform.DOScale(Vector3.one, stickerDuration).SetEase(Ease.OutBack));

			return sequence;
		}

		public static string GetMoneyStrValue(MoneyRewardItem moneyReward) {
			return moneyReward == null ? "" : moneyReward.Value.ToString();
		}

		public override bool TryGetFlaresSequence(out Sequence sequence) {
			sequence = null;
			if (!CanFlare) {
				return false;
			}

			UIFlare activeBtnFlare = _buyButton.gameObject.activeInHierarchy ? _buyButtonFlare : _noInetButtonFlare;
			IEnumerable<UIFlare> flares = new[] {_chestFlare, _saleFlare, activeBtnFlare};

			flares = flares.Where(flare => flare != null);

			sequence = BaseWindowView.GetFlaresSequence(flares, moveDelayDelta: 0.25f, moveTime: 0.55f);
			return true;
		}

		protected void SetBuyButtonInteractable(bool interact) {
			_buyButton.interactable = interact;
			if (_additionalBuyButton != null) {
				_additionalBuyButton.interactable = interact;
			}
		}

		protected void SetInetButtonInteractable(bool interact) {
			_noInetButton.interactable = interact;
			if (_additionalNoInetButton != null) {
				_additionalNoInetButton.interactable = interact;
			}
		}

		protected virtual void InitTexts(BankGoodsData bankGoodsData) {
			BankGoods presenterBankGoods = bankGoodsData.bankGood;
			MoneyRewardItem moneyReward = presenterBankGoods?.RewardModel.GetRewardByType<MoneyRewardItem>(RewardType.money);
			
			string coinsText = GetMoneyStrValue(moneyReward);
			
			_coinsText.text = _needCoinsPrefix ? string.Format(_coinsText.text, coinsText) : coinsText;
			_priceText.text  = IsTaken ? ScriptLocalization.SaleWindow_button_sold : $"{presenterBankGoods?.Price:0.##} {presenterBankGoods?.Currency}";
			
			string discountText = $"{bankGoodsData.Discount}%";
			if (!string.IsNullOrEmpty(discountText) && !IsTaken) {
				_discountText.text = discountText;	
				_saleCloud.gameObject.SetActive(true);
			}
		}

		protected virtual void InitButtons(UnityAction onBuyButtonClick, UnityAction onNoInetButtonClick, bool hasInternet) {
			_buyButton.onClick.RemoveAllListeners();
			_buyButton.onClick.AddListener(onBuyButtonClick);
			_noInetButton.onClick.AddListener(onNoInetButtonClick);
			
			_buyButton.gameObject.SetActive(hasInternet);
			_noInetButton.gameObject.SetActive(!hasInternet);

			if (_additionalBuyButton != null) {
				_additionalBuyButton.onClick.RemoveAllListeners();
				_additionalBuyButton.onClick.AddListener(onBuyButtonClick);
				_additionalBuyButton.gameObject.SetActive(hasInternet);
			}

			if (_additionalNoInetButton != null) {
				_additionalNoInetButton.onClick.AddListener(onNoInetButtonClick);
				_additionalNoInetButton.gameObject.SetActive(!hasInternet);
			}
		}

		protected virtual void SetSaleTaken() {
			_saleCloud.SetActive(false);
			_buyButtonFlare.StopMoving();
			_buyButtonFlare.enabled = false;
			_buyButton.interactable = false;
			_takenStateObject.SetActive(true);

			if (_additionalBuyButton != null) {
				_additionalBuyButton.interactable = false;
			}
		}

		protected string GetCoinsPrefix(string coinsValue) {
			return $"<sprite=0>{coinsValue}";
		}
		
		private void InitVisual() {
			if (IsTaken) {
				SetSaleTaken();
			}

			foreach (RectTransform button in _animButtons) button.localScale = Vector3.zero;
			foreach (RectTransform text in _animButtonTexts) text.localScale = Vector3.zero;

			_animSale.transform.localScale = Vector3.one * 1.4f;
			_animSale.alpha = 0f;
		}

		private void InitRewardGrid() {
			if (_gridTooltipVisual) {
				_gridLayout.SetRewardContainerVisual();
			}
		}
	}
}