using Content.Server.Popups;
using Content.Shared._Art.Hookah;
using Content.Shared.Containers.ItemSlots;

namespace Content.Server._Art.Hookah;

public sealed class HookahPipeSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly PopupSystem _popup = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<HookahPipeComponent>();
        while (query.MoveNext(out var uid, out var pipe))
        {
            if (pipe.AttachedTo is not { } hookahUid)
            {
                pipe.AttachedTo = null;
                Dirty(uid, pipe);
                continue;
            }

            var pipePos = _transform.GetMapCoordinates(uid);
            var hookahPos = _transform.GetMapCoordinates(hookahUid);

            if (pipePos.InRange(hookahPos, pipe.Range))
                continue;

            if (!TryComp(hookahUid, out HookahComponent? hookah))
                continue;

            if (_itemSlots.TryGetSlot(hookahUid, hookah.PipeSlotId, out var slot) &&
                slot.Item is { } current && current != uid)
            {
                _itemSlots.TryEjectToHands(hookahUid, slot, null, excludeUserAudio: true);
            }

            if (_itemSlots.TryInsert(hookahUid, hookah.PipeSlotId, uid, null, excludeUserAudio: true))
                _popup.PopupEntity(Loc.GetString("fork-hookah-pipe-snap-back"), hookahUid);
        }
    }
}