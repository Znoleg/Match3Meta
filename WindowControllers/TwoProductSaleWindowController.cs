using DG.Tweening;
using share.controller.game.events.local.ProductSales;
using share.controller.GUI.events.OneProduct;
using Spine.Unity;
using UnityEngine;

namespace share.controller.GUI.events.TwoProduct {
    public class TwoProductSaleWindowController : ProductSaleWindowBase<TwoProductSaleController, ProductSalePresenterBase> {
        [SerializeField] private SkeletonGraphic[] _animations;
        [SerializeField] private bool _invertAnimationOrder = default;
        
        protected override ICardBehaviour GetCardBehaviour(ProductSalePresenterBase salePresenter, int presenterIndex, bool isTaken) {
            return new RealCurrencyCardBehaviour(salePresenter, this, presenterIndex, isTaken);
        }

        public override void MakeShowAnimation(Sequence sequence) {
            base.MakeShowAnimation(sequence);
            OneProductSaleWindowController.MakeStandartSaleWindowShowAnimation(sequence, View, SalePresenters, _animations, _invertAnimationOrder);
        }
        
        public override void MakeHideAnimation(Sequence sequence) {
            base.MakeHideAnimation(sequence);
            OneProductSaleWindowController.MakeStandartSaleWindowHideAnimation(sequence, View, _animations);
        }
    }
}