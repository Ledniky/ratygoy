using Content.Server.Atmos.EntitySystems;
using Content.Server.DoAfter;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Popups;
using Content.Shared._Art.Hookah;
using Content.Shared.Atmos;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Nutrition.EntitySystems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;

namespace Content.Server._Art.Hookah;

public sealed class HookahSystem : EntitySystem
{
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly DoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
	[Dependency] private readonly IngestionSystem _ingestion = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HookahComponent, EntInsertedIntoContainerMessage>(OnPipeInserted);
        SubscribeLocalEvent<HookahComponent, EntRemovedFromContainerMessage>(OnPipeRemoved);
        SubscribeLocalEvent<HookahComponent, EntityTerminatingEvent>(OnHookahTerminating);

        SubscribeLocalEvent<HookahPipeComponent, AfterInteractEvent>(OnPipeUsed);
        SubscribeLocalEvent<HookahPipeComponent, HookahPuffDoAfterEvent>(OnPuffDoAfter);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<HookahPipeComponent>();
        while (query.MoveNext(out var uid, out var pipe))
        {
            if (pipe.AttachedTo is not { } hookahUid)
                continue;

            if (!Exists(hookahUid) || !TryComp(hookahUid, out HookahComponent? hookah))
            {
                pipe.AttachedTo = null;
                Dirty(uid, pipe);
                continue;
            }

            var pipePos = _transform.GetMapCoordinates(uid);
            var hookahPos = _transform.GetMapCoordinates(hookahUid);

            if (pipePos.InRange(hookahPos, pipe.Range))
                continue;

            SnapBack(uid, pipe, hookahUid, hookah);
        }
    }

    private void OnPipeInserted(Entity<HookahComponent> hookah, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != hookah.Comp.PipeSlotId)
            return;

        if (!TryComp(args.Entity, out HookahPipeComponent? pipe))
            return;

        pipe.AttachedTo = null;
        Dirty(args.Entity, pipe);
    }

    private void OnPipeRemoved(Entity<HookahComponent> hookah, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != hookah.Comp.PipeSlotId)
            return;

        if (!TryComp(args.Entity, out HookahPipeComponent? pipe))
            return;

        pipe.AttachedTo = hookah.Owner;
        Dirty(args.Entity, pipe);
    }

    private void OnHookahTerminating(Entity<HookahComponent> hookah, ref EntityTerminatingEvent args)
    {
        var query = EntityQueryEnumerator<HookahPipeComponent>();
        while (query.MoveNext(out var uid, out var pipe))
        {
            if (pipe.AttachedTo != hookah.Owner)
                continue;

            pipe.AttachedTo = null;
            Dirty(uid, pipe);
        }
    }

    private void OnPipeUsed(Entity<HookahPipeComponent> pipe, ref AfterInteractEvent args)
    {
        if (pipe.Comp.AttachedTo is not { } hookahUid || !TryComp(hookahUid, out HookahComponent? hookah))
            return;

        if (!args.CanReach
            || args.Target is not { } target
            || !_solutionContainer.TryGetRefillableSolution(hookahUid, out _, out var solution)
            || !HasComp<BloodstreamComponent>(target)
            || !_ingestion.HasMouthAvailable(args.User, target))
        {
            return;
        }

        foreach (var reagent in solution.Contents)
        {
            if (reagent.Reagent.Prototype != hookah.SolutionNeeded)
            {
                _explosion.QueueExplosion(hookahUid, "Default", hookah.ExplosionIntensity, 0.5f, 3, canCreateVacuum: false);
                Del(hookahUid);
                args.Handled = true;
                return;
            }
        }

        var doAfter = new DoAfterArgs(EntityManager, args.User, pipe.Comp.Delay, new HookahPuffDoAfterEvent(), pipe.Owner, target: target, used: pipe.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
        };

        _doAfterSystem.TryStartDoAfter(doAfter);
        args.Handled = true;
    }

    private void OnPuffDoAfter(Entity<HookahPipeComponent> pipe, ref HookahPuffDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Args.Target is not { } target)
            return;

        if (pipe.Comp.AttachedTo is not { } hookahUid || !TryComp(hookahUid, out HookahComponent? hookah))
            return;

        if (!_solutionContainer.TryGetSolution((hookahUid, null), hookah.SolutionName, out var solEnt, out var solution))
            return;

        if (solution.Contents.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("fork-hookah-empty"), hookahUid, args.User);
            return;
        }

        var environment = _atmos.GetContainingMixture(target, true, true);
        if (environment == null)
            return;

        _damageable.TryChangeDamage(target, hookah.Damage, true);

        var taken = _solutionContainer.SplitSolution(solEnt.Value, hookah.PuffAmount);

        var merger = new GasMixture(1) { Temperature = taken.Temperature };
        merger.SetMoles(hookah.GasType, taken.Volume.Float() / hookah.ReductionFactor);
        _atmos.Merge(environment, merger);

        _audio.PlayPvs(hookah.PuffSound, hookahUid);

        args.Handled = true;
    }

    private void SnapBack(EntityUid pipeUid, HookahPipeComponent pipe, EntityUid hookahUid, HookahComponent hookah)
    {
        if (!_itemSlots.TryGetSlot(hookahUid, hookah.PipeSlotId, out var slot))
            return;

        if (!_itemSlots.TryInsert(hookahUid, slot, pipeUid, null, excludeUserAudio: true))
        {
            _itemSlots.SetLock(hookahUid, slot, false);
            _itemSlots.TryInsert(hookahUid, slot, pipeUid, null, excludeUserAudio: true);
        }

        pipe.AttachedTo = null;
        Dirty(pipeUid, pipe);

        _popup.PopupEntity(Loc.GetString("fork-hookah-pipe-snap-back"), hookahUid);
    }
}