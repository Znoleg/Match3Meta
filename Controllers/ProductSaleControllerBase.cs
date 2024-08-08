using System;
using System.Linq;
using share.controller.game.events.local.core;
using share.controller.GUI;
using share.controller.statistic;
using share.controller.statistic.eventDummies.core;
using share.model.bank;
using share.model.events.local;
using share.model.user.events.local;
using UnityEngine;

namespace share.controller.game.events.local.ProductSales {
	public abstract class ProductSaleControllerBase : BaseLocalEventController<ProductSaleData, ProductSaleModel>, INotificatorStateProvider {
		public Action OnRealCurrencyFailBuy;
		public Action OnRealCurrencySuccessBuy;

		protected bool _needToShowFirstWindow = true;
		protected bool _needToShowWindowAgain = true;
		protected bool _needToComplete = false;

		public abstract string StatisticsSendName { get; }
		public bool AllIsPurchased => GetPurchasedCount() == Data.IsGoodsPurchased.Count;
		protected virtual bool AlwaysShowWindowAfterBuy => false;
		protected abstract Priority WindowPriority { get; }

		protected ProductSaleControllerBase(ProductSaleModel model) : base(model) { }

		public override void Init() {
			base.Init();
			if (Proto.CompleteOnClose && Data.AlreadyShownFirstWindow) {
				_needToComplete = true;
			} else {
				Data.WindowIsReady = false;
				Data.SuccessBuy -= SuccessBuyHandler;
				Data.SuccessBuy += SuccessBuyHandler;
				if (Proto.Goods != null && Proto.Goods.BankGoodsDatas != null) {
					Data.SetGoodsCount(Proto.Goods.BankGoodsDatas.Length);

					foreach (BankGoodsData bankGood in Proto.Goods.BankGoodsDatas) {
						if (bankGood.bankGood != null) {
							SystemController.BankController.RegisterBankProduct(bankGood.bankGood);
							SystemController.BankController.TryAddNewProduct(bankGood.bankGood);
						}
					}
				}
			}
		}

		public int GetPurchasedCount() {
			return Data.IsGoodsPurchased.Count(purchased => purchased);
		}

		public void ClearActionsOnWindowShow() {
			ActionSystemController.RemoveAction(WindowPriority);
		}
		
		public void MarkGoodsPurchased(int goodsIndex) {
			Data.IsGoodsPurchased[goodsIndex] = true;
		}

		public virtual void BuyForRealCurrency(BankGoods currentGoods, int saleIndex, Action callback = null) {
			SystemController.BankController.SuccessPurchasing += SuccessBuy;
			SystemController.BankController.FailPurchasing += FailBuy;
			SystemController.BankController.BuyConsumable(currentGoods, callback);
		}

		public void AfterSuccessPurchase() {
			if (AlwaysShowWindowAfterBuy || !AllIsPurchased) { 
				Data.NeedToShowWindow = true;
				_needToShowWindowAgain = true;
			} else if (Proto.CompleteAfterAllGoodsIsPurchased) {
				Complete();
			}
		}

		public void Complete() {
			SystemController.BankController.SuccessPurchasing -= SuccessBuy;
			SystemController.BankController.FailPurchasing -= FailBuy;
			Data.SuccessBuy -= SuccessBuyHandler;
			CompleteEvent(!Proto.IsRepeatable);
		}

		public override void Dispose() {
			if (Proto != null && Proto.Goods != null && Proto.Goods.BankGoodsDatas != null) {
				foreach (BankGoodsData bankGood in Proto.Goods.BankGoodsDatas) {
					if(bankGood.saleType == SaleType.Free || bankGood.saleType == SaleType.HardCurrencyBuy) continue;
					SystemController.BankController.RemoveBankProduct(bankGood.bankGood);
				}
			}

			Data.SuccessBuy -= SuccessBuyHandler;
			SystemController.BankController.SuccessPurchasing -= SuccessBuy;
			SystemController.BankController.FailPurchasing -= FailBuy;
			base.Dispose();
		}

		public bool WindowIsReady() {
			if (Data.WindowIsReady) {
				return Data.WindowIsReady;
			}

			Data.WindowIsReady = AllProductsReady();
			return Data.WindowIsReady;
		}

		public (NotificatorState state, int count) PreviousNotificatorState { get; set; }

		public NotificatorState GetNotificatorState(out int count) {
			count = default;
			
			if (ActionSystemController.HasAction(WindowPriority)) {
				return NotificatorState.ActivitySmall;
			}

			return NotificatorState.Disabled;
		}
		
		public virtual void TryShowSaleWindow(object _ = null) {
			if (Data.IsPaused()) {
				_needToShowWindowAgain = true;
				return;
			}

			Data.AlreadyShownFirstWindow = true;

			ShowSaleWindow();
		}

		public void SendStatistic(string name, string action, string str2 = "", string str3 = "", string str4 = "", int int3 = 0) {
			if (string.IsNullOrEmpty(str2) && Proto.Goods != null && Proto.Goods.BankGoodsDatas != null) {
				for (int i = 0; i < Proto.Goods.BankGoodsDatas.Length; i++) {
					BankGoodsData goodsData = Proto.Goods.BankGoodsDatas[i];
					if (goodsData.saleType == SaleType.Free) {
						str2 += $"{goodsData.freeCurrencyOffer.Id}, ";
					}
					else if (goodsData.saleType == SaleType.HardCurrencyBuy) {
						str2 += $"{goodsData.hardCurrencyOffer.Id}, ";
					} else {
						str2 += $"{goodsData.bankGood.Id}, ";
					}
				}

				if (str2 != null && str2.Length > 2) {
					str2 = str2.Substring(0, str2.Length - 2);
				}
			}

			if (string.IsNullOrEmpty(str3) && Proto.Goods != null && Proto.Goods.BankGoodsDatas != null) {
				for (int i = 0; i < Proto.Goods.BankGoodsDatas.Length; i++) {
					BankGoodsData goodsData = Proto.Goods.BankGoodsDatas[i];
					if (goodsData.saleType == SaleType.Free || goodsData.saleType == SaleType.HardCurrencyBuy) continue;
					str3 += $"{goodsData.bankGood.PackId}, ";
				}

				if (str3 != null && str3.Length > 2) {
					str3 = str3.Substring(0, str3.Length - 2);
				}
			}

			int int1 = Data.IsGoodsPurchased.Count(isBought => isBought == true);
			StatisticController.SendActionsEvent(name, action, int1, (int) GetRemainingTime(), int3, Proto.Id, str2, str3, str4);
		}
		
		protected bool AllProductsReady() {
			bool allReady = true;
			if (Proto.Goods != null && Proto.Goods.BankGoodsDatas != null) {
				
				foreach (BankGoodsData bankGood in Proto.Goods.BankGoodsDatas) {
					if(bankGood.saleType == SaleType.Free || bankGood.saleType == SaleType.HardCurrencyBuy) continue;
					if (!SystemController.BankController.ProductReady(bankGood.bankGood)) {
						allReady = false;
						break;
					}
				}
			}

			return allReady;
		}

		protected override void Update() {
			base.Update();

			if (_needToComplete) {
				Complete();
			} else {
				TryShowSaleWindowByData();
			}
		}

		protected override async void TimesUp() {
			base.TimesUp();

			if (SystemController.ViewManager.IsShowing(Proto.MainWindow)) {
				await SystemController.ViewManager.AsyncHide(Proto.MainWindow);
			}

			SendStatistic(StatisticsSendName, StatisticEventActionsAction.TimesUp);

			ActionSystemController.RemoveAction(WindowPriority);
			ActionSystemController.RemoveAction(Priority.MultipleSaleWindowAfterPurchase);
			Complete();
		}
		
		protected virtual void ShowSaleWindow() { }
		
		protected override ProductSaleData NewDataCreator(ProductSaleModel model) => new ProductSaleData(model);

		private void AddTryShowSaleWindowAction() {
			ActionSystemController.AddAction(Priority.MultipleSaleWindowAfterPurchase, TryShowSaleWindow, null, false);
		}
		
		private void TryShowSaleWindowByData() {
			if (!SystemController.ViewManager.CurrentSceneIsMeta) return;
			
			if (!Data.AlreadyShownFirstWindow && _needToShowFirstWindow && WindowIsReady()) {
				_needToShowFirstWindow = false;
				ActionSystemController.AddAction(WindowPriority, TryShowSaleWindow, null);
			} else if (Data.NeedToShowWindow && _needToShowWindowAgain && WindowIsReady()) {
				_needToShowWindowAgain = false;
				AddTryShowSaleWindowAction();
			}
		}

		private async void SuccessBuyHandler(string packId) {
			Debug.Log($"product sale buy success{Proto.Id}");
			
			if (Proto.Goods != null && Proto.Goods.BankGoodsDatas != null) {
				for (int i = 0; i < Proto.Goods.BankGoodsDatas.Length; i++) {
					BankGoodsData goodsData = Proto.Goods.BankGoodsDatas[i];
					if (goodsData.bankGood != null && goodsData.bankGood.PackId == packId) {
						MarkGoodsPurchased(i);
						break;
					}
				}
			}

			await SystemController.ViewManager.AsyncHide(Proto.MainWindow);

			AfterSuccessPurchase();
		}
		
		private void SuccessBuy() {
			Debug.Log($"{Proto.Id} buy success");
			SystemController.BankController.SuccessPurchasing -= SuccessBuy;
			SystemController.BankController.FailPurchasing -= FailBuy;
			
			OnRealCurrencySuccessBuy?.Invoke();
		}

		private void FailBuy() {
			Debug.Log($"{Proto.Id} buy fail");
			SystemController.BankController.SuccessPurchasing -= SuccessBuy;
			SystemController.BankController.FailPurchasing -= FailBuy;

			OnRealCurrencyFailBuy?.Invoke();
		}
	}
}