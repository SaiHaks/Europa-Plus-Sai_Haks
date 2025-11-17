using System.Linq;
using Content.Shared.ActionBlocker;
using Content.Shared.Administration.Components;
using Content.Shared.Administration.Logs;
using Content.Shared.CombatMode;
using Content.Shared.Database;
using Content.Shared._EinsteinEngines.Flight;
using Content.Shared.DoAfter;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Item;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Rejuvenate;
using Content.Shared.Stunnable;
using Content.Shared.Timing;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;
using PullableComponent = Content.Shared.Movement.Pulling.Components.PullableComponent;

namespace Content.Shared._Europa.Soulbreakers
{
    public sealed class SoulbreakerCollarSystem : EntitySystem
    {
        [Dependency] private readonly INetManager _net = default!;
        [Dependency] private readonly ISharedAdminLogManager _adminLog = default!;
        [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
        [Dependency] private readonly SharedAudioSystem _audio = default!;
        [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
        [Dependency] private readonly SharedHandsSystem _hands = default!;
        [Dependency] private readonly SharedVirtualItemSystem _virtualItem = default!;
        [Dependency] private readonly SharedInteractionSystem _interaction = default!;
        [Dependency] private readonly SharedPopupSystem _popup = default!;
        [Dependency] private readonly UseDelaySystem _delay = default!;
        [Dependency] private readonly InventorySystem _inventory = default!;
        [Dependency] private readonly MovementSpeedModifierSystem _speedModifier = null!;
        [Dependency] private readonly SharedCombatModeSystem _combat = default!;
        [Dependency] private readonly SharedStunSystem _stun = default!;

        private const string CollarSlot = "neck";
        private const float ModifiedSpeed = 0.1f;
        private static readonly SoundSpecifier CollarSound = new SoundCollectionSpecifier("SoulbreakerCollar", new AudioParams().WithVariation(0.1f));
        private static readonly SoundSpecifier CollarRemovedSound = new SoundCollectionSpecifier("SoulbreakerCollarRemoved", new AudioParams().WithVariation(0.1f));

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<UnEnslaveAttemptEvent>(OnUnEnslaveAttempt);

            // Enslaved component events
            SubscribeLocalEvent<SoulbreakerEnslavedComponent, ComponentShutdown>(OnEnslavedShutdown);
            SubscribeLocalEvent<SoulbreakerEnslavedComponent, RejuvenateEvent>(OnRejuvenate);
            SubscribeLocalEvent<SoulbreakerEnslavedComponent, RefreshMovementSpeedModifiersEvent>(OnMovementSpeedModify);
            SubscribeLocalEvent<SoulbreakerEnslavedComponent, UpdateCanMoveEvent>(OnMoveAttempt);
            SubscribeLocalEvent<SoulbreakerEnslavedComponent, IsUnequippingTargetAttemptEvent>(OnUnequipAttempt);
            SubscribeLocalEvent<SoulbreakerEnslavedComponent, GetVerbsEvent<Verb>>(AddUnEnslaveVerb);
            SubscribeLocalEvent<SoulbreakerEnslavedComponent, PullStartedMessage>(OnPullChanged);
            SubscribeLocalEvent<SoulbreakerEnslavedComponent, PullStoppedMessage>(OnPullChanged);
            SubscribeLocalEvent<SoulbreakerEnslavedComponent, DropAttemptEvent>(OnActionAttempt);
            SubscribeLocalEvent<SoulbreakerEnslavedComponent, PickupAttemptEvent>(OnActionAttempt);
            SubscribeLocalEvent<SoulbreakerEnslavedComponent, AttackAttemptEvent>(OnActionAttempt);
            SubscribeLocalEvent<SoulbreakerEnslavedComponent, UseAttemptEvent>(OnActionAttempt);
            SubscribeLocalEvent<SoulbreakerEnslavedComponent, InteractionAttemptEvent>(OnInteractionAttempt);

            SubscribeLocalEvent<SoulbreakerEnslavableComponent, IsEquippingAttemptEvent>(OnEquipAttempt);
            SubscribeLocalEvent<SoulbreakerEnslavableComponent, RemoveCollarDoAfterEvent>(OnRemoveCollarDoAfter);

            // Collar component events
            SubscribeLocalEvent<SoulbreakerCollarComponent, AfterInteractEvent>(OnCollarAfterInteract);
            SubscribeLocalEvent<SoulbreakerCollarComponent, MeleeHitEvent>(OnCollarMeleeHit);
            SubscribeLocalEvent<SoulbreakerCollarComponent, AddCollarDoAfterEvent>(OnAddCollarDoAfter);
            SubscribeLocalEvent<SoulbreakerCollarComponent, VirtualItemDeletedEvent>(OnCollarVirtualItemDeleted);
        }

        #region Enslaved Component Handlers

        private void OnEnslavedShutdown(EntityUid uid, SoulbreakerEnslavedComponent component, ComponentShutdown args)
        {
            RemoveCollarFromSlot(uid);
            RemCompDeferred<SoulbreakerEnslavedComponent>(uid);
            _speedModifier.RefreshMovementSpeedModifiers(uid);
        }

        private void OnMovementSpeedModify(EntityUid uid, SoulbreakerEnslavedComponent component, RefreshMovementSpeedModifiersEvent args)
        {
            args.ModifySpeed(ModifiedSpeed, ModifiedSpeed);
        }

        private void OnInteractionAttempt(Entity<SoulbreakerEnslavedComponent> ent, ref InteractionAttemptEvent args)
        {
            args.Cancelled = true;
        }

        private void OnRejuvenate(EntityUid uid, SoulbreakerEnslavedComponent component, RejuvenateEvent args)
        {
            if (!args.Uncuff)
                return;

            RemoveCollarFromSlot(uid);

            RemCompDeferred<SoulbreakerEnslavedComponent>(uid);
            _speedModifier.RefreshMovementSpeedModifiers(uid);
        }

        private void OnPullChanged(EntityUid uid, SoulbreakerEnslavedComponent component, PullMessage args)
        {
            _actionBlocker.UpdateCanMove(uid);
        }

        private void OnMoveAttempt(EntityUid uid, SoulbreakerEnslavedComponent component, UpdateCanMoveEvent args)
        {
            if (TryComp(uid, out PullableComponent? pullable) && pullable.BeingPulled)
                args.Cancel();
        }

        private void AddUnEnslaveVerb(EntityUid uid, SoulbreakerEnslavedComponent component, GetVerbsEvent<Verb> args)
        {
            if (!args.CanAccess || args.Hands == null || !args.CanInteract)
                return;

            if (!HasComp<SoulbreakerCollarAuthorizedComponent>(args.User))
                return;

            if (!HasComp<SoulbreakerEnslavedComponent>(args.Target))
                return;

            if (!_inventory.TryGetSlotEntity(uid, CollarSlot, out var collar) || !HasComp<SoulbreakerCollarComponent>(collar))
                return;

            var verb = new Verb
            {
                Act = () => TryUnEnslave(uid, args.User, collar),
                DoContactInteraction = true,
                Icon = new SpriteSpecifier.Rsi(new("/Textures/_Europa/Objects/Devices/collar.rsi"), "icon"),
                Text = Loc.GetString("unenslave-verb-get-data-text")
            };

            args.Verbs.Add(verb);
        }

        private void OnRemoveCollarDoAfter(EntityUid uid, SoulbreakerEnslavableComponent component, RemoveCollarDoAfterEvent args)
        {
            if (args.Handled || args.Args.Target is not { } target || args.Args.Used is not { } used)
                return;

            args.Handled = true;

            if (args.Cancelled)
            {
                _popup.PopupClient(Loc.GetString("soulbreaker-collar-remove-collar-fail-message"), args.Args.User, args.Args.User);
                return;
            }

            UnEnslave(target, args.Args.User, used, component);
        }

        private void OnActionAttempt(EntityUid uid, SoulbreakerEnslavedComponent component, CancellableEntityEventArgs args)
        {
            args.Cancel();
            _popup.PopupClient(Loc.GetString("soulbreaker-collar-block-action"), uid, uid);
        }

        private void OnEquipAttempt(EntityUid uid, SoulbreakerEnslavableComponent component, IsEquippingAttemptEvent args)
        {
            if (!HasComp<SoulbreakerCollarComponent>(args.Equipment))
                return;

            if (!HasComp<SoulbreakerCollarAuthorizedComponent>(args.Equipee))
            {
                args.Cancel();
                _popup.PopupClient(Loc.GetString("soulbreaker-collar-authorization-error-equip"), uid, uid);
            }
        }

        private void OnUnequipAttempt(EntityUid uid, SoulbreakerEnslavedComponent component, IsUnequippingTargetAttemptEvent args)
        {
            if (!HasComp<SoulbreakerCollarComponent>(args.Equipment))
                return;

            if (!HasComp<SoulbreakerCollarAuthorizedComponent>(args.Unequipee))
            {
                args.Cancel();
                _popup.PopupClient(Loc.GetString("soulbreaker-collar-authorization-error-unequip"), uid, uid);
            }
        }

        #endregion

        #region Collar Component Handlers

        private void OnCollarAfterInteract(EntityUid uid, SoulbreakerCollarComponent component, AfterInteractEvent args)
        {
            if (args.Target is not { Valid: true } target)
                return;

            if (!args.CanReach)
            {
                _popup.PopupClient(Loc.GetString("soulbreaker-collar-too-far-away-error"), args.User, args.User);
                return;
            }

            if (_combat.IsInCombatMode(args.User))
            {
                args.Handled = true;
                return;
            }

            args.Handled = TryEnslave(args.User, target, uid, component);
        }

        private void OnCollarMeleeHit(EntityUid uid, SoulbreakerCollarComponent component, MeleeHitEvent args)
        {
            if (!args.HitEntities.Any())
                return;

            TryEnslave(args.User, args.HitEntities.First(), uid, component);
        }

        private void OnAddCollarDoAfter(EntityUid uid, SoulbreakerCollarComponent component, AddCollarDoAfterEvent args)
        {
            if (args.Handled || args.Cancelled || args.Args.Target is not { } target)
                return;

            args.Handled = true;

            if (HasComp<SoulbreakerEnslavedComponent>(target))
                return;

            var user = args.Args.User;

            if (TryAddCollar(target, user, uid))
            {
                HandleSuccessfulEnslavement(user, target, uid);
            }
            else
            {
                HandleFailedEnslavement(user, target);
            }
        }

        private void OnCollarVirtualItemDeleted(EntityUid uid, SoulbreakerCollarComponent component, VirtualItemDeletedEvent args)
        {
            UnEnslave(args.User, null, uid, collarComponent: component);
        }

        #endregion

        #region Helper Methods

        private void RemoveCollarFromSlot(EntityUid uid)
        {
            if (_inventory.TryGetSlotEntity(uid, CollarSlot, out var collar) && HasComp<SoulbreakerCollarComponent>(collar))
                _inventory.DropSlotContents(uid, CollarSlot);
        }

        private void UpdateHeldItems(EntityUid uid, EntityUid collar, SoulbreakerEnslavableComponent? component = null)
        {
            if (!Resolve(uid, ref component) || !TryComp<HandsComponent>(uid, out var hands))
                return;

            foreach (var hand in _hands.EnumerateHands((uid, hands)))
            {
                if (_hands.TryGetHeldItem((uid, hands), hand, out var held) && HasComp<UnremoveableComponent>(held))
                    continue;

                _hands.DoDrop(uid, hand);

                if (_virtualItem.TrySpawnVirtualItemInHand(collar, uid, out var virtItem))
                    EnsureComp<UnremoveableComponent>(virtItem.Value);
            }
        }

        private bool TryAddCollar(EntityUid target,
            EntityUid user,
            EntityUid collar,
            SoulbreakerEnslavableComponent? enslavableComponent = null,
            SoulbreakerCollarComponent? collarComponent = null)
        {
            if (!Resolve(target, ref enslavableComponent, false) || !Resolve(collar, ref collarComponent) ||
                !_interaction.InRangeUnobstructed(collar, target))
            {
                return false;
            }

            _hands.TryDrop(user, collar);
            _inventory.TryEquip(target, collar, CollarSlot, force: true);

            UpdateHeldItems(target, collar, enslavableComponent);
            return true;
        }

        private void HandleSuccessfulEnslavement(EntityUid user, EntityUid target, EntityUid collar)
        {
            _stun.KnockdownOrStun(target, TimeSpan.FromMinutes(3), true);
            AddComp<SoulbreakerEnslavedComponent>(target);
            _speedModifier.RefreshMovementSpeedModifiers(target);
            _audio.PlayPredicted(CollarSound, collar, user);

            var popupText = user == target
                ? "soulbreaker-collar-enslave-self-observer-success-message"
                : "soulbreaker-collar-enslave-observer-success-message";

            _popup.PopupEntity(Loc.GetString(popupText,
                ("user", Identity.Name(user, EntityManager)),
                ("target", Identity.Name(target, EntityManager))),
                target,
                GetObserverFilter(target, user),
                true);

            if (target == user)
            {
                _popup.PopupClient(Loc.GetString("soulbreaker-collar-enslave-self-success-message"), user, user);
                _adminLog.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(user):player} has enslaved himself");
            }
            else
            {
                _popup.PopupClient(Loc.GetString("soulbreaker-collar-enslave-other-success-message",
                    ("otherName", Identity.Name(target, EntityManager, user))),
                    user,
                    user);
                _popup.PopupClient(Loc.GetString("soulbreaker-collar-enslave-by-other-success-message",
                    ("otherName", Identity.Name(user, EntityManager, target))),
                    target,
                    target);
                _adminLog.Add(LogType.Action, LogImpact.High, $"{ToPrettyString(user):player} has enslaved {ToPrettyString(target):player}");
            }
        }

        private void HandleFailedEnslavement(EntityUid user, EntityUid target)
        {
            if (target == user)
            {
                _popup.PopupClient(Loc.GetString("soulbreaker-collar-enslave-interrupt-self-message"), user, user);
            }
            else
            {
                _popup.PopupClient(Loc.GetString("soulbreaker-collar-enslave-interrupt-message",
                    ("targetName", Identity.Name(target, EntityManager, user))),
                    user,
                    user);
                _popup.PopupClient(Loc.GetString("soulbreaker-collar-enslave-interrupt-other-message",
                    ("otherName", Identity.Name(user, EntityManager, target)),
                    ("otherEnt", user)),
                    target,
                    target);
            }
        }

        private Filter GetObserverFilter(EntityUid target, EntityUid user)
        {
            return Filter.Pvs(target, entityManager: EntityManager)
                .RemoveWhere(e => e.AttachedEntity == target || e.AttachedEntity == user);
        }

        #endregion

        #region private API

        private bool TryEnslave(EntityUid user, EntityUid target, EntityUid collar, SoulbreakerCollarComponent? collarComponent = null)
        {
            if (!Resolve(collar, ref collarComponent))
                return false;

            if (!HasComp<SoulbreakerEnslavableComponent>(target))
                return false;

            if (!HasComp<SoulbreakerCollarAuthorizedComponent>(user))
                return false;

            if (user == target)
            {
                _popup.PopupClient(Loc.GetString("soulbreaker-collar-cannot-enslave-themself"), user, user);
                return false;
            }

            if (HasComp<SoulbreakerCollarProtectionComponent>(target))
            {
                _popup.PopupClient(Loc.GetString("soulbreaker-collar-protection-reason",
                    ("identity", Identity.Name(target, EntityManager, user))),
                    user,
                    user);
                return false;
            }

            if (!_hands.CanDrop(user, collar))
            {
                _popup.PopupClient(Loc.GetString("soulbreaker-collar-protection-reason",
                    ("target", Identity.Name(target, EntityManager, user))),
                    user,
                    user);
                return false;
            }

            if (TryComp<FlightComponent>(target, out var flight) && flight.On)
            {
                _popup.PopupClient(Loc.GetString("soulbreaker-collar-target-flying-error",
                    ("targetName", Identity.Name(target, EntityManager, user))),
                    user,
                    user);
                return true;
            }

            var enslavingTime = GetEnslavingTime(target, TimeSpan.FromSeconds(3)); //clothing.EquipDelay

            var doAfterEventArgs = new DoAfterArgs(EntityManager, user, enslavingTime, new AddCollarDoAfterEvent(), collar, target, collar)
            {
                BreakOnMove = true,
                BreakOnWeightlessMove = false,
                BreakOnDamage = false,
                NeedHand = true,
                DistanceThreshold = 1f
            };

            if (!_doAfter.TryStartDoAfter(doAfterEventArgs))
                return true;

            ShowEnslavingPopup(user, target);
            return true;
        }

        private TimeSpan GetEnslavingTime(EntityUid target, TimeSpan baseTime)
        {
            if (HasComp<DisarmProneComponent>(target))
                return TimeSpan.Zero;

            if (HasComp<StunnedComponent>(target))
                return baseTime.Divide(2);

            return baseTime;
        }

        private void ShowEnslavingPopup(EntityUid user, EntityUid target)
        {
            var popupText = user == target
                ? "soulbreaker-collar-start-enslaving-self-observer"
                : "soulbreaker-collar-start-enslaving-observer";

            _popup.PopupEntity(Loc.GetString(popupText,
                    ("user", Identity.Name(user, EntityManager)),
                    ("target", Identity.Entity(target, EntityManager))),
                target,
                GetObserverFilter(target, user),
                true);

            if (target == user)
            {
                _popup.PopupClient(Loc.GetString("soulbreaker-collar-target-self"), user, user);
            }
            else
            {
                _popup.PopupClient(Loc.GetString("soulbreaker-collar-start-enslaving-target-message",
                    ("targetName", Identity.Name(target, EntityManager, user))),
                    user,
                    user);
                _popup.PopupEntity(Loc.GetString("soulbreaker-collar-start-enslaving-by-other-message",
                    ("otherName", Identity.Name(user, EntityManager, target))),
                    target,
                    target);
            }
        }

        private void TryUnEnslave(
            EntityUid target,
            EntityUid user,
            EntityUid? collarToRemove = null,
            SoulbreakerEnslavableComponent? enslavedComponent = null,
            SoulbreakerCollarComponent? collarComponent = null)
        {
            if (!Resolve(target, ref enslavedComponent))
            {
                Log.Warning("TryUnEnslave called with invalid parameters");
                return;
            }

            if (collarToRemove == null && _inventory.TryGetSlotEntity(target, CollarSlot, out var neckItem))
                collarToRemove = neckItem;

            if (collarToRemove == null || !Resolve(collarToRemove.Value, ref collarComponent))
                return;

            var attempt = new UnEnslaveAttemptEvent(user, target);
            RaiseLocalEvent(user, ref attempt, true);

            if (attempt.Cancelled)
                return;

            if (user != target && !_interaction.InRangeUnobstructed(user, target))
            {
                _popup.PopupClient(Loc.GetString("soulbreaker-collar-cannot-remove-collar-too-far-message"), user, user);
                return;
            }

            var unenslaveTime = GetUnEnslaveDuration(collarComponent);

            if (user == target && !TryResetUseDelay(collarToRemove.Value))
                return;

            StartUnenslaveDoAfter(user, target, collarToRemove.Value, unenslaveTime);
        }

        private TimeSpan GetUnEnslaveDuration(SoulbreakerCollarComponent? component)
        {
            return component?.UnEnslavingTime ?? TimeSpan.Zero;
        }

        private bool TryResetUseDelay(EntityUid collar)
        {
            return TryComp(collar, out UseDelayComponent? useDelay) &&
                   _delay.TryResetDelay((collar, useDelay), true);
        }

        private void StartUnenslaveDoAfter(EntityUid user, EntityUid target, EntityUid collar, TimeSpan duration)
        {
            var doAfterEventArgs = new DoAfterArgs(EntityManager, user, duration, new RemoveCollarDoAfterEvent(), target, target, collar)
            {
                BreakOnMove = true,
                BreakOnWeightlessMove = false,
                BreakOnDamage = true,
                NeedHand = true,
                RequireCanInteract = false,
                DistanceThreshold = 1f
            };

            if (!_doAfter.TryStartDoAfter(doAfterEventArgs))
                return;

            _adminLog.Add(LogType.Action, LogImpact.High, $"{ToPrettyString(user):player} is trying to unenslave {ToPrettyString(target):subject}");
            ShowUnenslavingPopup(user, target);
        }

        private void ShowUnenslavingPopup(EntityUid user, EntityUid target)
        {
            var popupText = user == target
                ? "soulbreaker-collar-start-unenslaving-self-observer"
                : "soulbreaker-collar-start-unenslaving-observer";

            _popup.PopupEntity(Loc.GetString(popupText,
                    ("user", Identity.Name(user, EntityManager)),
                    ("target", Identity.Entity(target, EntityManager))),
                target,
                GetObserverFilter(target, user),
                true);

            if (target == user)
            {
                _popup.PopupClient(Loc.GetString("soulbreaker-collar-start-unenslaving-self"), user, user);
            }
            else
            {
                _popup.PopupClient(Loc.GetString("soulbreaker-collar-start-unenslaving-target-message",
                    ("targetName", Identity.Name(target, EntityManager, user))),
                    user,
                    user);
                _popup.PopupEntity(Loc.GetString("soulbreaker-collar-start-unenslaving-by-other-message",
                    ("otherName", Identity.Name(user, EntityManager, target))),
                    target,
                    target);
            }
        }

        private void UnEnslave(EntityUid target,
            EntityUid? user,
            EntityUid collar,
            SoulbreakerEnslavableComponent? enslavableComponent = null,
            SoulbreakerCollarComponent? collarComponent = null)
        {
            if (!Resolve(target, ref enslavableComponent) || !Resolve(collar, ref collarComponent) ||
                TerminatingOrDeleted(collar) || TerminatingOrDeleted(target))
                return;

            if (user != null)
            {
                var attempt = new UnEnslaveAttemptEvent(user.Value, target);
                RaiseLocalEvent(user.Value, ref attempt);
                if (attempt.Cancelled)
                    return;
            }

            RemCompDeferred<SoulbreakerEnslavedComponent>(target);

            _speedModifier.RefreshMovementSpeedModifiers(target);
            _audio.PlayPredicted(CollarRemovedSound, target, user);
            RemoveCollarFromSlot(target);

            if (_net.IsServer && user != null)
                _hands.PickupOrDrop(user, collar);

            _stun.KnockdownOrStun(target, TimeSpan.FromSeconds(3), true);

            HandlePostUnenslaveEffects(target, user);
            LogUnenslaveAction(target, user);
        }

        private void HandlePostUnenslaveEffects(EntityUid target, EntityUid? user)
        {
            var shoved = false;

            if (user != null && _combat.IsInCombatMode(user) && target != user)
            {
                var eventArgs = new DisarmedEvent(target, user.Value, 1f);
                RaiseLocalEvent(target, ref eventArgs);
                shoved = true;
            }

            if (user != null)
            {
                var message = shoved
                    ? "soulbreaker-collar-remove-collar-push-success-message"
                    : "soulbreaker-collar-remove-collar-success-message";

                _popup.PopupClient(Loc.GetString(message,
                    ("otherName", Identity.Name(user.Value, EntityManager, user))),
                    user.Value,
                    user.Value);
            }

            if (target != user && user != null)
            {
                _popup.PopupEntity(Loc.GetString("soulbreaker-collar-remove-collar-by-other-success-message",
                    ("otherName", Identity.Name(user.Value, EntityManager, user))),
                    target,
                    target);
            }
        }

        private void LogUnenslaveAction(EntityUid target, EntityUid? user)
        {
            if (user != null)
            {
                var message = target == user
                    ? $"{ToPrettyString(user):player} has successfully uneslaved themselves"
                    : $"{ToPrettyString(user):player} has successfully uneslaved {ToPrettyString(target):player}";

                _adminLog.Add(LogType.Action, LogImpact.High, $"{message}");
            }
        }

        #endregion

        #region Event Handlers

        private void OnUnEnslaveAttempt(ref UnEnslaveAttemptEvent args)
        {
            if (args.Cancelled || !Exists(args.User) || Deleted(args.User))
            {
                args.Cancelled = true;
                return;
            }

            if (!HasComp<SoulbreakerCollarAuthorizedComponent>(args.User))
                args.Cancelled = true;

            if (args.Cancelled)
                _popup.PopupClient(Loc.GetString("soulbreaker-collar-cannot-interact-message"), args.Target, args.User);
        }

        #endregion
    }

    [Serializable, NetSerializable]
    public sealed partial class RemoveCollarDoAfterEvent : SimpleDoAfterEvent;

    [Serializable, NetSerializable]
    public sealed partial class AddCollarDoAfterEvent : SimpleDoAfterEvent;

    [ByRefEvent]
    public record struct UnEnslaveAttemptEvent(EntityUid User, EntityUid Target)
    {
        public readonly EntityUid User = User;
        public readonly EntityUid Target = Target;
        public bool Cancelled = false;
    }
}
