using share.controller.statistic.eventDummies.core;
using share.model.bank;
using share.model.events.local;

namespace share.controller.GUI.events {
	public readonly struct RealCurrencyCardBehaviour : ICardBehaviour {
		private readonly IProductSaleWindow _productSaleWindow;
		private readonly int _presenterIndex;
		private readonly bool _isTaken;
		private readonly ProductSalePresenterBase _presenter;

		public RealCurrencyCardBehaviour(ProductSalePresenterBase presenter, IProductSaleWindow productSaleWindow, int presenterIndex, bool isTaken) {
			_presenter = presenter;
			_productSaleWindow = productSaleWindow;
			_presenterIndex = presenterIndex;
			_isTaken = isTaken;
		}

		public bool HasInternet => GetProto().Goods.BankGoodsDatas[_presenterIndex].bankGood.IsAvailable();
		public bool CanBePurchased => !_isTaken && HasInternet;

		public void Buy() {
			_productSaleWindow.GetSaleControllerBase().OnRealCurrencySuccessBuy += OnBuyEnd;
			_productSaleWindow.GetSaleControllerBase().OnRealCurrencyFailBuy += OnBuyEnd;
			TryRealCurrencyBuy(_presenterIndex);
		}

		private void TryRealCurrencyBuy(int presenterIndex) {
			BankGoods bankGoodToBuy = GetProto().Goods.BankGoodsDatas[presenterIndex].bankGood;
			if (bankGoodToBuy.IsAvailable()) {
				_presenter.OnRealCurrencyBuyStart();
				_productSaleWindow.GetSaleControllerBase().SendStatistic(_productSaleWindow.GetSaleControllerBase().StatisticsSendName, StatisticEventActionsAction.Buy, bankGoodToBuy.Id, bankGoodToBuy.PackId, bankGoodToBuy.Currency, bankGoodToBuy.StatisticPrice);
				_productSaleWindow.GetSaleControllerBase().BuyForRealCurrency(bankGoodToBuy, presenterIndex);
			} else {
				_productSaleWindow.ShowNoInternet();
			}
		}

		private void OnBuyEnd() {
			_productSaleWindow.GetSaleControllerBase().OnRealCurrencySuccessBuy -= OnBuyEnd;
			_productSaleWindow.GetSaleControllerBase().OnRealCurrencyFailBuy -= OnBuyEnd;
			_presenter.OnRealCurrencyBuyEnd();
		}
	
		private ProductSaleModel GetProto() => _productSaleWindow.GetSaleControllerBase().GetProto<ProductSaleModel>();
	}
}