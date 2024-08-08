using System;
using System.Collections.Generic;
using System.Linq;
using GlobalEventAggregator;
using share.controller.game.events.core;
using share.controller.game.events.local.ProductSales;
using share.controller.GUI.events.ThreeProducts;
using share.controller.statistic;
using share.controller.statistic.eventDummies.core;
using share.manager;
using share.manager.Resource;
using share.model.events.local;
using share.model.events.local.core;
using share.model.events.serverEvents;
using share.model.user.events.core;
using share.model.user.events.local.core;
using share.utils;
using UnityEngine;

namespace share.controller.game.events.local.core
{
    public sealed class LocalEventsController : BaseEventsController, ISceneResourcesChecker {
        
        private List<BaseLocalEventModel> _events;
        
        public LocalEventsController() {
            CurrentSource = ResourceSource.Events;
        }

        public static bool CheckPriorityByProto(BaseLocalEventModel proto)
        {
            if (proto.IconGroupPriority != null) {
                return CheckPriority(proto.IconGroupPriority, proto.Id);
            }
            return true;
        }

        public static bool CheckPriority(IconGroupPriority iconGroupPriority, string id) {
            bool isHigherPriority = true;
            if (iconGroupPriority != null) {
                foreach (IActiveLocalEvent iActiveEvent in SystemController.UserData.newEvents.Active) {
                    if (iActiveEvent.GetProto() != null) {
                        if (iActiveEvent.GetProto().Id != id) {
                            if (!iActiveEvent.IsPaused() || iActiveEvent.IsPaused() && (iActiveEvent.IsBlockByScene() || iActiveEvent.IsDeactivatedForDailyLevel())) {
                                if (iActiveEvent.GetProto().IconGroupPriority != null) {
                                    if (iActiveEvent.GetProto().IconGroupPriority.placement == iconGroupPriority.placement) {
                                        if (iActiveEvent.GetProto().IconGroupPriority.priorityType == IconPriorityType.BlockToActivate) {
                                            isHigherPriority = false;
                                            break;
                                        }

                                        if (iActiveEvent.GetProto().IconGroupPriority.priority > iconGroupPriority.priority) {
                                            isHigherPriority = false;
                                            break;
                                        }
                                    }
                                }
                            }
                        } else if (iActiveEvent.IsBlockByScene() || iActiveEvent.IsDeactivatedForDailyLevel()) {
                            isHigherPriority = false;
                            break;
                        }
                    }
                }

                foreach (IServerActiveEvent iActiveEvent in SystemController.UserData.ServerEvents.Active) {
                    if (iActiveEvent.Proto != null && iActiveEvent.Proto.Id != id) {
                        if (iActiveEvent.Proto.IconGroupPriority != null) {
                            if (iActiveEvent.Proto.IconGroupPriority.placement == iconGroupPriority.placement) {
                                if (iActiveEvent.Proto.IconGroupPriority.priorityType == IconPriorityType.BlockToActivate) {
                                    isHigherPriority = false;
                                    break;
                                }

                                if (iActiveEvent.Proto.IconGroupPriority.priority > iconGroupPriority.priority) {
                                    isHigherPriority = false;
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            return isHigherPriority;
        }

        public override void DropAll() {
            base.DropAll();
            SystemController.UserData.newEvents = new Events();
        }
        
        public void CompleteEvent(IActiveLocalEvent localEvent, bool forever)
        {
            if (forever)
            {
                StatisticController.SendActionsEvent(StatisticEventActionsName.Event, StatisticEventActionsAction.Finish, localEvent.GetProto().Id);
                
                CompletedEventData completedEventData = new CompletedEventData
                {
                    StageId = localEvent.GetProto().Id,
                    Timestamp = localEvent.GetActivationDateTime()
                };
                
                SystemController.UserData.newEvents.Completed.Add(completedEventData);
                SystemController.UserData.newEvents.Active.Remove(localEvent);
            }
            
            EventAggregator.Invoke(new EventFinishEvent(localEvent.GetProto().Type.ToString()));

            ActiveEvents.Remove(localEvent.GetProto().Id);
        }

        public T GetEventProtoByType<T>(LocalEventType localEventType) where T : class
        {
            return SystemController.SystemModel.EventsModel.Events.FirstOrDefault(e => e.Type == localEventType) as T;
        }

        public void TryToStartEventById(string id)
        {
            Debug.Log("TryToStartEventById " + id);
            foreach (BaseLocalEventModel @event in _events)
            {
                if (@event.Id == id)
                {
                    try
                    {
                        foreach (IActiveLocalEvent activeEvent in SystemController.UserData.newEvents.Active)
                        {
                            if (activeEvent.GetProto() != null && activeEvent.GetProto().Id == @event.Id)
                            {
                                activeEvent.SetCooldownTimestamp(-1);
                            }
                        }

                        //@event.StagesDurationInHours += 1000;
                        var controller = CreateControllerForEvent(@event);
                        ActiveEvents.Add(@event.Id, controller);
                    }
                    catch (Exception e)
                    {
                        Log($"event by id {id} cant start! {e.Message}");
                    }
                }
            }
        }

        public async void Init() {
            if (VersionUtility.IsMetaOffVersion()) {
                return;
            }
            
            _events = SystemController.SystemModel.EventsModel.Events;

            CheckPacks();
            
            foreach (IActiveLocalEvent activeEvent in SystemController.UserData.newEvents.Active)
            {
                if (activeEvent.IsPaused()) continue;
                IEventController controller = CreateControllerForEvent(activeEvent.GetProto());
                await controller.CreateDataAndLoadGroups();
                ActiveEvents.Add(activeEvent.GetProto().Id, controller);
            }

            foreach (KeyValuePair<string,IEventController> eventController in ActiveEvents) {
                eventController.Value.Init();
            }
            
            SystemController.ViewManager.SceneShowing += OnSceneShowing;
            
            UpdateEvents();
            SendStartedEventStatistic();
        }

        private void CheckPacks()
        {
            bool needLoadAnyPack = false;
            foreach (IActiveLocalEvent activeEvent in SystemController.UserData.newEvents.Active)
            {
                bool isPacksLoaded = true;

                if (activeEvent.GetProto() != null && activeEvent.GetProto().Conditions.PacksId.Count > 0)
                {
                    isPacksLoaded = SystemController.ExpansionController.IsPacksDownloaded(activeEvent.GetProto().Conditions.PacksId);
                }

                if (!isPacksLoaded)
                {
                    needLoadAnyPack = true;
                }
                activeEvent.SetAvailable(isPacksLoaded);
            }

            if (needLoadAnyPack)
            {
                SystemController.ExpansionController.LoadedCallback += LoadedCallback;
            }
        }

        private void LoadedCallback(ResourceGroup exp)
        {
            SystemController.ExpansionController.LoadedCallback -= LoadedCallback;
            CheckPacks();
        }

        private bool _inUpdate;
        public override async void UpdateEvents() {
            if(_inUpdate) return;
            _inUpdate = true;

            try {
                List<IEventController> newEvents = new List<IEventController>();

                foreach (BaseLocalEventModel @event in _events)
                {
                    Log($"try to start event by id {@event.Id}");
                    if (IsBlocked(@event)) continue;
                    if (IsAllConditionsAreTrue(@event) == false) continue;

                    Log($"success start event by id {@event.Id}");
                    StatisticController.SendActionsEvent(StatisticEventActionsName.Event, StatisticEventActionsAction.Start, @event.Id);
                    IEventController controller = CreateControllerForEvent(@event);
                    
                    await controller.CreateDataAndLoadGroups();
                    EventAggregator.Invoke(new EventStartedEvent(@event.Type.ToString()));
                    ActiveEvents.Add(@event.Id, controller);
                    newEvents.Add(controller);
                }

                foreach (IEventController eventController in newEvents) {
                    eventController.Init();
                }
            } catch (Exception e) {
                Debug.Log(e.Message);
                _inUpdate = false;
            }

            _inUpdate = false;
        }

        private IEventController CreateControllerForEvent(BaseLocalEventModel @event)
        {
            switch (@event.GetEventType())
            {
                case LocalEventType.StartBoosters:
                    StartBoostersEventController startBoosters =
                        new StartBoostersEventController((StartBoostersEventModel) @event);
                    return startBoosters;
                case LocalEventType.Moneybox:
                    MoneyboxEventController moneybox = new MoneyboxEventController((MoneyboxEventModel) @event);
                    return moneybox;
                case LocalEventType.TreasureHunt:
                    TreasureHuntEventController hunt = new TreasureHuntEventController((TreasureHuntEventModel) @event);
                    return hunt;
                case LocalEventType.OneProduct:
                    OneProductSaleController oneProduct = new OneProductSaleController((GeneratedProductSaleModel) @event);
                    return oneProduct;
                case LocalEventType.Ruler:
                    RulerEventController ruler = new RulerEventController((RulerEventModel) @event);
                    return ruler;
                case LocalEventType.BankOffer:
                    BankEventController bank = new BankEventController((BankEventModel) @event);
                    return bank;
                case LocalEventType.TwoProduct:
                    TwoProductSaleController twoProduct = new TwoProductSaleController((GeneratedProductSaleModel) @event);
                    return twoProduct;
                case LocalEventType.Quit:
                    QuitEventController quit = new QuitEventController((QuitEventModel) @event);
                    return quit;
                case LocalEventType.BattlePass:
                    BattlePassEventController battlePass = new BattlePassEventController((BattlePassEventModel) @event);
                    return battlePass;
                case LocalEventType.FunnyDucks:
                    FunnyDucksEventController funnyDucks = new FunnyDucksEventController((FunnyDucksEventModel) @event);
                    return funnyDucks;
                case LocalEventType.SkillEvent:
                    SkillEventController skillEventController = new SkillEventController((SkillEventModel) @event);
                    return skillEventController;
                case LocalEventType.EndlessSale:
                    EndlessSaleController endlessSaleController = new EndlessSaleController((EndlessSaleModel) @event);
                    return endlessSaleController;
                case LocalEventType.ThreeProduct:
                    ThreeProductSaleController threeProduct = new ThreeProductSaleController((ThreeProductSaleModel) @event);
                    return threeProduct;
                case LocalEventType.ClanOffer:
                    ClanOfferSaleController clanOffer = new ClanOfferSaleController((GeneratedProductSaleModel) @event);
                    return clanOffer;
                case LocalEventType.UpSale:
                    UpSaleController upSale = new UpSaleController((ProductSaleModel) @event);
                    return upSale;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private bool IsBlocked(BaseLocalEventModel @event) {
            Log($"IsBlocked {@event.Id} 0");
            if (ActiveEvents.ContainsKey(@event.Id)) return true;

            Log($"IsBlocked {@event.Id} 1");
            foreach (IActiveLocalEvent activeEvent in SystemController.UserData.newEvents.Active)
            {
                if (@event.IsRepeatable && activeEvent.GetProto() != null && activeEvent.GetProto().Id == @event.Id && activeEvent.IsPaused())
                {
                    long currentTime = activeEvent.GetProto().Conditions.IsByServerTime() ? SystemController.ServerController.GetServerTimeInMilliSeconds().ServerTime :
                        SystemController.ServerController.GetServerTimeInMilliSeconds().LocalTime;
                    long cooldownTime = activeEvent.GetCooldownTimestamp();
                    if (currentTime < cooldownTime)
                    {
                        return true;
                    }

                    activeEvent.SetCooldownTimestamp(-1);
                }
            }
            
            /*Log($"IsBlocked {@event.Id} 2");
            foreach (IActiveLocalEvent activeEvent in SystemController.UserData.newEvents.Active)
            {
                if (activeEvent.GetProto() != null && activeEvent.GetProto().Id != @event.Id && activeEvent.GetProto().Type == @event.Type)
                {
                    if (!activeEvent.IsPaused())
                    {
                        return true;
                    }
                }
            }*/

            Log($"IsBlocked {@event.Id} 3");
            foreach (var completedEvent in SystemController.UserData.newEvents.Completed)
            {
                if (completedEvent.StageId == @event.Id)
                {
                    return true;
                }
            } 

            return false;
        }
        
        private bool IsAllConditionsAreTrue(BaseLocalEventModel @event) {
            Log($"IsAllConditionsAreTrue {@event.Id} 0");
            if (!@event.IsAvailable) return false;
            Log($"IsAllConditionsAreTrue {@event.Id} 1");
            if (@event.IconGroupPriority != null && @event.IconGroupPriority.priorityType == IconPriorityType.BlockToActivate) {
                if (!CheckPriorityByProto(@event)) {
                    return false;
                }
            }
            
            Log($"IsAllConditionsAreTrue {@event.Id} 2");
            if (@event.Conditions == null) return true;
            
            Log($"IsAllConditionsAreTrue {@event.Id} 3");
            if (!string.IsNullOrEmpty(@event.Conditions.CompletedQuestId))
            {
                if (!SystemController.UserData.quests.IsQuestCompleted(@event.Conditions.CompletedQuestId))
                {
                    return false;
                }
            }

            Log($"IsAllConditionsAreTrue {@event.Id} 4");
            if (@event.Conditions.CompletedEvent != null)
            {
                bool containsId = false;
                foreach (CompletedEventData complete in SystemController.UserData.newEvents.Completed)
                {
                    if (complete.StageId == @event.Conditions.CompletedEvent.stageId)
                    {
                        containsId = true;
                
                        long currentTime = @event.Conditions.GetCurrentTime();
                
                        if (currentTime < complete.Timestamp + @event.Conditions.CompletedEvent.timeOffsetHour * TimeUtility.MillisecondsInHour)
                        {
                            return false;  
                        }
                
                        break;
                    }
                }

                if (!containsId) return false;
            }

            Log($"IsAllConditionsAreTrue {@event.Id} 5");
            if (@event.Conditions.PacksId != null && @event.Conditions.PacksId.Count > 0)
            {
                if (!SystemController.ExpansionController.IsPacksDownloaded(@event.Conditions.PacksId))
                {
                    return false;
                }
            }

            Log($"IsAllConditionsAreTrue {@event.Id} 6");
            if (@event.Conditions.RepeatableLevelLogic != null) {
                Log($"MaxCompletedMatch3Level = {SystemController.UserData.MaxCompletedMatch3Level} lastCollectiblesProductLaunchLevel = {SystemController.UserData.newEvents.lastCollectiblesProductLaunchLevel}");
                if (SystemController.UserData.MaxCompletedMatch3Level == SystemController.UserData.newEvents.lastCollectiblesProductLaunchLevel) {
                    return false;
                }
                if (!@event.Conditions.RepeatableLevelLogic.CheckConditions(SystemController.UserData.MaxCompletedMatch3Level)) {
                    return false;
                }
            } 
            else if (@event.Conditions.CompletedLevel > -1)
            {
                if (SystemController.UserData.MaxCompletedMatch3Level < @event.Conditions.CompletedLevel)
                {
                    return false;
                }
            }
            
            Log($"IsAllConditionsAreTrue {@event.Id} 7");
            if (@event.Conditions.LevelLimit > -1)
            {
                Log($"MaxCompletedMatch3Level = {SystemController.UserData.MaxCompletedMatch3Level} LevelLimit = {@event.Conditions.LevelLimit}");
                if (SystemController.UserData.MaxCompletedMatch3Level > @event.Conditions.LevelLimit)
                {
                    return false;
                }
            }
            
            Log($"IsAllConditionsAreTrue {@event.Id} 8");
            if (@event.Conditions.PurchasesLimit > -1)
            {
                if (SystemController.UserData.statisticData.purchases.Count > @event.Conditions.PurchasesLimit)
                {
                    return false;
                }
            }
            
            Log($"IsAllConditionsAreTrue {@event.Id} 9");
            if (@event.Conditions.MetaOpeningCount > -1)
            {
                if (MetaSceneShowingCount < @event.Conditions.MetaOpeningCount)
                {
                    return false;
                }
            }

            Log($"IsAllConditionsAreTrue {@event.Id} 10");
            if (@event.Conditions.AvailableLevelCount > -1)
            {
                if (SystemController.UserData.MaxCompletedMatch3Level + @event.Conditions.AvailableLevelCount > SystemController.SystemModel.Match3Levels.Quantity)
                {
                    return false;
                }
            }

            Log($"IsAllConditionsAreTrue {@event.Id} 11");
            if (@event.Conditions.NeedAnyCollectibles)
            {
                if (!SystemController.GameController.CollectiblesController.CheckCondition())
                {
                    return false;
                }
            }

            Log($"IsAllConditionsAreTrue {@event.Id} 12");
            if (@event.Conditions.HaveActiveBattlePass) {
                bool haveBattlePass = false;
                foreach (KeyValuePair<string, IEventController> activeEvent in ActiveEvents)
                {
                    if (activeEvent.Value is BattlePassEventController) {
                        haveBattlePass = true;
                        break;
                    }
                }

                if (!haveBattlePass) {
                    return false;
                }
            }

            Log($"IsAllConditionsAreTrue {@event.Id} 13");
            if (@event.Conditions.InClan) {
                if (!SystemController.GameController.ClansController.IsUserInClan())
                {
                    return false;
                }
            }
            
            Log($"IsAllConditionsAreTrue {@event.Id} 14");
            if (@event.Conditions.IsByServerTime() && !CheckServerTimestamp(@event))
            {
                return false;
            }

            return true;
        }

        private bool CheckServerTimestamp(BaseLocalEventModel @event)
        {
            Log($"CheckServerTimestamp {@event.Id} 0");
            if (!SystemController.ServerController.GetServerTimeInMilliSeconds().IsSync)
            {
                return false;
            }

            Log($"CheckServerTimestamp {@event.Id} 1");
            if (@event.IsRepeatable || @event.RepeatDelayInHours != -1)
            {
                Log($"CheckServerTimestamp {@event.Id} 2");
                long deltaMs = @event.Conditions.GetCurrentTime() - @event.Conditions.StartServerTimestamp;
                float deltaHours = deltaMs / (float) TimeUtility.MillisecondsInHour;
                float modulo = deltaHours % @event.RepeatDelayInHours;
                if (modulo > 0 && modulo < @event.DurationInHours - @event.MinDurationInHours)
                {
                    return true;
                }
            }
            else
            {
                Log($"CheckServerTimestamp {@event.Id} 3");
                if (@event.Conditions.GetCurrentTime() > @event.Conditions.StartServerTimestamp)
                {
                    Log($"CheckServerTimestamp {@event.Id} 4");
                    if (@event.Conditions.GetCurrentTime() < @event.Conditions.StartServerTimestamp + 
                        @event.DurationInHours * TimeUtility.MillisecondsInHour - @event.MinDurationInHours * TimeUtility.MillisecondsInHour)
                    {
                        return true;
                    } 
                }
            }

            Log($"CheckServerTimestamp {@event.Id} 5");
            return false;
        }

        public List<IEventController> GetActiveEvents() {
            return ActiveEvents.Select(x => x.Value).ToList();
        }

        private void Log(string str) {
#if !UNITY_EDITOR
            Debug.Log(str);
#endif
        }
    }
}
